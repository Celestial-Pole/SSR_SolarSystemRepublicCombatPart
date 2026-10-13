using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen;

//按原始关键帧和切线采样后坐与井盖，不依赖 Animator 或引擎动画空接口。
internal sealed class SsrAnimationClip
{
    internal string name;
    public static implicit operator bool(SsrAnimationClip clip) => clip is not null;

    //独立曲线文件的根结构。
    private sealed class ClipData
    {
        public string name { get; set; }
        public float length { get; set; }
        public Track[] tracks { get; set; }
    }

    //单个节点的一组位置、旋转、缩放或欧拉曲线。
    private sealed class Track
    {
        public string path { get; set; }
        public string kind { get; set; }
        public Key[] keys { get; set; }
    }

    //保存原始关键帧的左右导数，重现 Unity 的三次 Hermite 插值。
    private sealed class Key
    {
        public float time { get; set; }
        public float[] value { get; set; }
        public float[] incoming { get; set; }
        public float[] outgoing { get; set; }
        public int weighted { get; set; }
    }

    private static readonly Dictionary<string, SsrAnimationClip> clips = new(StringComparer.Ordinal);
    private readonly ClipData data;
    private readonly ConditionalWeakTable<GameObject, Transform[]> bindings = new();
    public float length => data.length;

    //只读取已导出的真实曲线，缺失数据时报告具体动画名称。
    private SsrAnimationClip(string clipName)
    {
        string root = SsrCombatMod.Root;
        string file = Path.Combine(root, "1.7", "AnimationData", clipName + ".json");
        data = JsonSerializer.Deserialize<ClipData>(File.ReadAllText(file))
            ?? throw new InvalidDataException("无法读取 SSR 动画：" + file);
        name = data.name;
        foreach (var track in data.tracks)
            foreach (var key in track.keys)
                if (key.weighted != 0) throw new NotSupportedException("SSR 动画包含尚未转换的加权切线：" + clipName);
    }

    //名称来自原来的 XML 或动画资源路径，所有实例共享只读关键帧。
    internal static SsrAnimationClip Load(string nameOrPath)
    {
        string clipName = Path.GetFileNameWithoutExtension(nameOrPath);
        if (!clips.TryGetValue(clipName, out var clip)) clips.Add(clipName, clip = new SsrAnimationClip(clipName));
        return clip;
    }

    //将同一实例的曲线节点缓存起来，采样不反复遍历层级。
    private Transform[] Bind(GameObject root)
    {
        var result = new Transform[data.tracks.Length];
        for (int i = 0; i < result.Length; i++)
        {
            string path = data.tracks[i].path;
            result[i] = string.IsNullOrEmpty(path) ? root.transform : root.transform.FindModelNode(path);
            if (result[i] == null) throw new InvalidOperationException($"SSR 动画 {name} 缺少节点：{path}");
        }
        return result;
    }

    //游戏时间完全由调用方提供，暂停、加速和关盖倒放均沿用原有逻辑。
    public void SampleAnimation(GameObject root, float time)
    {
        Transform[] nodes = bindings.GetValue(root, Bind);
        time = Mathf.Clamp(time, 0, length);
        for (int i = 0; i < nodes.Length; i++)
        {
            Track track = data.tracks[i];
            Vector4 value = Sample(track.keys, time);
            switch (track.kind)
            {
                case "position": nodes[i].localPosition = new Vector3(value.x, value.y, value.z); break;
                case "scale": nodes[i].localScale = new Vector3(value.x, value.y, value.z); break;
                case "rotation":
                    float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
                    if (magnitude == 0) throw new InvalidDataException("SSR 动画包含零旋转四元数：" + name);
                    nodes[i].localRotation = new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
                    break;
                case "euler": nodes[i].localRotation = Quaternion.Euler(value.x, value.y, value.z); break;
                default: throw new InvalidDataException("未知 SSR 动画通道：" + track.kind);
            }
        }
    }

    //在相邻原始关键帧之间插值，端点直接返回保存值。
    private static Vector4 Sample(Key[] keys, float time)
    {
        int last = keys.Length - 1;
        if (time <= keys[0].time) return Read(keys[0].value);
        if (time >= keys[last].time) return Read(keys[last].value);
        int left = 0, right = last;
        while (right - left > 1)
        {
            int mid = (left + right) / 2;
            if (keys[mid].time <= time) left = mid;
            else right = mid;
        }
        Key a = keys[left], b = keys[right];
        float duration = b.time - a.time, t = (time - a.time) / duration;
        float t2 = t * t, t3 = t2 * t;
        return Read(a.value) * (2 * t3 - 3 * t2 + 1) + Read(a.outgoing) * ((t3 - 2 * t2 + t) * duration)
            + Read(b.value) * (-2 * t3 + 3 * t2) + Read(b.incoming) * ((t3 - t2) * duration);
    }

    //三维通道的第四分量不会参与赋值。
    private static Vector4 Read(float[] value) => new(value[0], value[1], value[2], value.Length == 4 ? value[3] : 0);
}

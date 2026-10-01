using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SSR.UnityComponent.Missiles;
using UnityEditor;
using UnityEngine;

namespace SSR.UnityComponent.Outline.Editor
{
    //从源动画提取九组舱盖、支撑杆和编号的独立动作，保留原始预制体与动画。
    public static class SiloDoorAssetBuilder
    {
        private const string Folder = "Assets/SSR/Missiles/Game";
        private const string Source = "Assets/SSR/Turrets/T1/Animations/Source/VerticalLauncherSource.anim";
        private const float Epsilon = 0.0002f;

        //生成逐舱动画和运行配置，并构建 Windows 资源包，不打开或运行场景。
        [MenuItem("SSR/导弹井/构建逐舱开盖资源")]
        public static string Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Source);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/MissileSiloGame.prefab");
            if (!source || !prefab) throw new InvalidOperationException("缺少导弹井源动画或游戏包装。");
            var layout = JsonUtility.FromJson<SiloLayoutData>(File.ReadAllText(Folder + "/SiloLayout.json", Encoding.UTF8));
            var bindings = AnimationUtility.GetCurveBindings(source);
            var windows = bindings.GroupBy(b => b.path).ToDictionary(g => g.Key, g => MotionWindow(source, g));
            var used = new HashSet<string>();
            for (int slot = 0; slot < 9; slot++)
            {
                string number = (slot + 1).ToString("D2");
                Vector2 window = windows["舱盖编号_" + number + "_"];
                if (window.y <= window.x) throw new InvalidOperationException("舱盖缺少开启动作：" + number);
                var paths = windows.Where(p => Vector2.Distance(p.Value, window) < Epsilon).Select(p => p.Key).ToArray();
                foreach (string path in paths)
                {
                    if (!used.Add(path) || !prefab.transform.Find(layout.animatorPath + "/" + path))
                        throw new InvalidOperationException("舱盖绑定重复或节点缺失：" + path);
                }
                var clip = new AnimationClip { name = "SiloOpening" + number, frameRate = source.frameRate, wrapMode = WrapMode.ClampForever };
                foreach (var binding in bindings.Where(b => paths.Contains(b.path)))
                    AnimationUtility.SetEditorCurve(clip, binding, Crop(AnimationUtility.GetEditorCurve(source, binding), window));
                clip.EnsureQuaternionContinuity();
                string destination = Folder + "/SiloOpening" + number + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
                if (existing)
                {
                    EditorUtility.CopySerialized(clip, existing);
                    UnityEngine.Object.DestroyImmediate(clip);
                    clip = existing;
                }
                else AssetDatabase.CreateAsset(clip, destination);
                EditorUtility.SetDirty(clip);
                AssetImporter.GetAtPath(destination).assetBundleName = "ssr_combat_windows";
                layout.slots[slot].openingPath = destination;
                layout.slots[slot].openingSeconds = clip.length;
            }
            if (windows.Any(p => p.Value.y > p.Value.x && !used.Contains(p.Key)))
                throw new InvalidOperationException("源动画含有未归属到九个舱盖的运动节点。");
            string json = JsonUtility.ToJson(layout, true);
            SiloLayoutReader.Read(json, Folder + "/SiloLayout.json");
            File.WriteAllText(Folder + "/SiloLayout.json", json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(Folder + "/SiloLayout.json");
            AssetDatabase.SaveAssets();
            return "逐舱动画已生成；" + CombatWindowsBundleBuilder.Build();
        }

        //依据原始曲线找出单组节点从闭合姿态离开到完全打开的真实时间段。
        private static Vector2 MotionWindow(AnimationClip source, IEnumerable<EditorCurveBinding> bindings)
        {
            float first = source.length, last = 0;
            foreach (var binding in bindings)
            {
                var keys = AnimationUtility.GetEditorCurve(source, binding).keys;
                if (keys.Length < 2 || Mathf.Abs(keys[0].value - keys[keys.Length - 1].value) < 0.0001f) continue;
                for (int i = 1; i < keys.Length; i++)
                    if (Mathf.Abs(keys[i].value - keys[0].value) > 0.0001f) { first = Mathf.Min(first, keys[i - 1].time); break; }
                for (int i = keys.Length - 2; i >= 0; i--)
                    if (Mathf.Abs(keys[i].value - keys[keys.Length - 1].value) > 0.0001f) { last = Mathf.Max(last, keys[i + 1].time); break; }
            }
            return new Vector2(first, last);
        }

        //裁切同组节点的曲线并归零时间，保留内部关键帧与边界切线。
        private static AnimationCurve Crop(AnimationCurve source, Vector2 window)
        {
            var keys = new List<Keyframe> { Boundary(source, window.x, 0) };
            foreach (var key in source.keys)
            {
                if (key.time <= window.x + Epsilon || key.time >= window.y - Epsilon) continue;
                var shifted = key;
                shifted.time -= window.x;
                keys.Add(shifted);
            }
            keys.Add(Boundary(source, window.y, window.y - window.x));
            return new AnimationCurve(keys.ToArray());
        }

        //使用源关键帧的真实切线构造裁切边界，常量曲线在边界保持原值。
        private static Keyframe Boundary(AnimationCurve source, float time, float shiftedTime)
        {
            foreach (var key in source.keys)
            {
                if (Mathf.Abs(key.time - time) > Epsilon) continue;
                var result = key;
                result.time = shiftedTime;
                return result;
            }
            return new Keyframe(shiftedTime, source.Evaluate(time));
        }
    }
}

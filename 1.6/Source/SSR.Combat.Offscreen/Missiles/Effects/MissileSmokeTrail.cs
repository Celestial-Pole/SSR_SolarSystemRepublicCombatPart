using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //按喷口经过的距离铺设烟团，独立推进扩散，并把整条烟迹合批绘制。
    internal sealed class MissileSmokeTrail
    {
        private readonly SiloSettings settings;
        private readonly List<MissileSmokePuff> puffs = new List<MissileSmokePuff>();
        private readonly MissileEffectMesh mesh = new MissileEffectMesh();
        private Vector3 previous;
        private double previousTime;
        private float remainder;
        private int emissionIndex;
        private bool started;
        internal bool Empty => puffs.Count == 0;

        //保存当前发射使用的烟雾密度与寿命参数。
        internal MissileSmokeTrail(SiloSettings settings) { this.settings = settings; }

        //在相邻喷口采样之间按固定距离补点，飞行加速不会把烟迹拉成断续的珠串。
        internal void Emit(Vector3 position, double time, int seed)
        {
            if (!started)
            {
                started = true;
                Add(position, time, seed);
            }
            else
            {
                float distance = Vector3.Distance(previous, position);
                for (float step = settings.smokeSpacing - remainder; step <= distance; step += settings.smokeSpacing)
                {
                    float t = step / distance;
                    Add(Vector3.LerpUnclamped(previous, position, t), previousTime + (time - previousTime) * t, seed);
                }
                remainder = (remainder + distance) % settings.smokeSpacing;
            }
            previous = position;
            previousTime = time;
        }

        //保存固定出生位置和确定性种子，不消耗游戏战斗随机数序列。
        private void Add(Vector3 position, double time, int seed)
        {
            puffs.Add(new MissileSmokePuff { Position = position, BirthTime = time, Seed = (seed % 997 + emissionIndex++ * 17) % 997 });
        }

        //仅按游戏时间回收消散烟团，暂停时保持原状。
        internal void Tick(double time) { puffs.RemoveAll(p => time - p.BirthTime >= settings.smokeLifetime); }

        //让烟团在出生地点附近缓慢漂移，转弯后旧烟迹仍保留原来的曲线。
        private static Vector3 PositionAt(MissileSmokePuff puff, double time)
        {
            float age = (float)(time - puff.BirthTime);
            return puff.Position + new Vector3(0.05f + Mathf.Sin(puff.Seed) * 0.08f,
                0.13f, 0.02f + Mathf.Cos(puff.Seed) * 0.08f) * age;
        }

        //按年龄扩散烟团并加入固定尺寸差异，避免等大的圆点沿路径机械排列。
        private float SizeAt(MissileSmokePuff puff, double time)
        {
            float age = Mathf.Clamp01((float)(time - puff.BirthTime) / settings.smokeLifetime);
            return Mathf.Lerp(settings.smokeStartSize, settings.smokeEndSize, Mathf.Sqrt(age))
                * (0.85f + 0.3f * Mathf.Repeat(puff.Seed * 0.618f, 1));
        }

        //计算所有扩散烟团的采集范围，预留朝向相机面片的对角尺寸。
        internal bool TryBounds(double time, out Bounds bounds)
        {
            bounds = default;
            for (int i = 0; i < puffs.Count; i++)
            {
                var area = new Bounds(PositionAt(puffs[i], time), Vector3.one * (SizeAt(puffs[i], time) * 1.5f));
                if (i == 0) bounds = area;
                else bounds.Encapsulate(area);
            }
            return puffs.Count != 0;
        }

        //按深度排列烟团并生成一个网格，增加烟量不增加逐烟团绘制调用。
        internal Mesh Build(Camera camera, double time)
        {
            puffs.Sort((a, b) => Vector3.Dot(PositionAt(b, time) - PositionAt(a, time), camera.transform.forward).CompareTo(0f));
            mesh.Clear();
            foreach (var puff in puffs)
            {
                float age = Mathf.Clamp01((float)(time - puff.BirthTime) / settings.smokeLifetime);
                float half = SizeAt(puff, time) * 0.5f;
                mesh.AddQuad(PositionAt(puff, time), camera.transform.right * half, camera.transform.up * half, age, puff.Seed);
            }
            return mesh.Upload();
        }

        //释放透明显示网格，保留烟团时间和位置供地图重新可见时恢复。
        internal void ReleaseDisplay() { mesh.Release(); }
    }
}

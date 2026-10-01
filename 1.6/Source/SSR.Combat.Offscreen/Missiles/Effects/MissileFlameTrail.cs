using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //保留喷口历史和后喷方向，生成随转弯弯曲、随气流扩散的短寿命热尾流。
    internal sealed class MissileFlameTrail
    {
        private readonly SiloSettings settings;
        private readonly List<MissileFlamePoint> points = new List<MissileFlamePoint>();
        private readonly List<Vector3> centers = new List<Vector3>();
        private readonly List<float> ages = new List<float>(), distances = new List<float>();
        private readonly MissileEffectMesh mesh = new MissileEffectMesh();
        private readonly int seed;
        internal bool Empty => points.Count == 0;

        //保存尾流长度、后喷速度和扰动参数。
        internal MissileFlameTrail(SiloSettings settings, int seed) { this.settings = settings; this.seed = seed; }

        //记录真实喷口姿态，旧热气不会跟随弹体当前方向整体旋转。
        internal void Emit(Vector3 position, Vector3 forward, double time)
        {
            points.Add(new MissileFlamePoint { Position = position, Velocity = -forward * settings.flameGasSpeed, BirthTime = time });
        }

        //按游戏时间移除冷却热气，发动机停止后仍保留短暂消散过程。
        internal void Tick(double time) { points.RemoveAll(p => time - p.BirthTime >= settings.flameTrailSeconds); }

        //从发射位置沿当时喷流方向推进热气。
        private static Vector3 PositionAt(MissileFlamePoint point, double time)
        {
            return point.Position + point.Velocity * (float)(time - point.BirthTime);
        }

        //覆盖后喷和扰动后的完整热流范围，独立于弹体采集纹理。
        internal bool TryBounds(double time, float scale, out Bounds bounds)
        {
            bounds = default;
            float diameter = settings.flameWidth * scale + settings.flameTurbulence * 2;
            for (int i = 0; i < points.Count; i++)
            {
                var area = new Bounds(PositionAt(points[i], time), Vector3.one * diameter);
                if (i == 0) bounds = area;
                else bounds.Encapsulate(area);
            }
            return points.Count != 0;
        }

        //从最近喷口向后截取连续历史，尾流长度受配置约束而不随高速无限增长。
        private float Gather(double time, float scale)
        {
            centers.Clear(); ages.Clear(); distances.Clear();
            float length = 0, limit = settings.flameTrailLength * scale;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector3 position = PositionAt(points[i], time);
                float age = Mathf.Clamp01((float)(time - points[i].BirthTime) / settings.flameTrailSeconds);
                bool last = false;
                if (centers.Count != 0)
                {
                    Vector3 previous = centers[centers.Count - 1];
                    float segment = Vector3.Distance(previous, position);
                    if (length + segment > limit)
                    {
                        float t = (limit - length) / segment;
                        position = Vector3.LerpUnclamped(previous, position, t);
                        age = Mathf.Lerp(ages[ages.Count - 1], age, t);
                        segment = limit - length;
                        last = true;
                    }
                    length += segment;
                }
                centers.Add(position); ages.Add(age); distances.Add(length);
                if (last) break;
            }
            return length;
        }

        //以连续横截面连接历史喷口，在直飞时也加入渐强的小幅气流摆动。
        internal Mesh Build(Camera camera, double time, float clock, float scale)
        {
            mesh.Clear();
            float length = Gather(time, scale);
            if (centers.Count < 2 || length < 0.0001f) return null;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 tangent = centers[Mathf.Max(i - 1, 0)] - centers[Mathf.Min(i + 1, centers.Count - 1)];
                Vector3 side = Vector3.Cross(camera.transform.forward, tangent);
                side = side.sqrMagnitude > 0.000001f ? side.normalized : camera.transform.right;
                float along = 1 - distances[i] / length;
                float wave = Mathf.Sin(clock * 42 - distances[i] * 5 + seed) * settings.flameTurbulence * (1 - along);
                float width = settings.flameWidth * scale * Mathf.Lerp(0.18f, 0.5f, along);
                mesh.AddRibbonPair(centers[i] + side * wave, side * width, along, ages[i], seed);
            }
            return mesh.Upload();
        }

        //释放热尾流显示网格，暂停或隐藏不清除采样历史。
        internal void ReleaseDisplay() { mesh.Release(); }
    }
}

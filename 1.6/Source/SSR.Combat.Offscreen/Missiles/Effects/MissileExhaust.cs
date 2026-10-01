using System;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace SSR.Combat.Offscreen
{
    //协调短喷焰、历史热尾流和距离采样烟迹，复用共享相机的独立透明采集通道。
    internal sealed class MissileExhaust : IDisposable
    {
        internal readonly Vector3 Anchor;
        internal readonly Building_MissileSilo Silo;
        internal readonly TurretCaptureFrame Frame = new TurretCaptureFrame { NeedsDepth = true, MaximumResolution = 1024 };
        private readonly SiloSettings settings;
        private readonly MissileSmokeTrail smoke;
        private readonly MissileFlameTrail flame;
        private readonly Mesh coreMesh;
        private readonly Material flameMaterial, smokeMaterial;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly double created;
        private IMissileExhaustSource emitter;
        private float previousFlight;
        private bool emitting;
        private static double Time => Find.TickManager.TicksGame / 60d;
        internal bool Finished => emitter == null && smoke.Empty && flame.Empty;
        internal Projectile_VerticalMissile Emitter => emitter as Projectile_VerticalMissile;
        internal Projectile_GuidedDefenseMissile GuidedEmitter => emitter as Projectile_GuidedDefenseMissile;
        private bool Burning => emitter != null && emitter.EngineIgnited;
        private float FlameScale => Burning ? 1 + (settings.ignitionFlareMultiplier - 1)
            * (1 - Mathf.Clamp01(emitter.EngineSeconds / settings.ignitionFlareSeconds)) : 1;

        //保存资源引用与投影原点，网格只在显示时按需创建。
        internal MissileExhaust(IMissileExhaustSource missile)
        {
            emitter = missile;
            settings = missile.ExhaustSettings;
            Anchor = missile.ExhaustAnchor;
            Silo = missile.ExhaustSilo;
            smoke = new MissileSmokeTrail(settings);
            flame = new MissileFlameTrail(settings, missile.TrailSeed);
            coreMesh = missile.FlamePrefab.GetComponent<MeshFilter>().sharedMesh;
            flameMaterial = missile.FlamePrefab.GetComponent<MeshRenderer>().sharedMaterial;
            smokeMaterial = missile.SmokePrefab.GetComponent<MeshRenderer>().sharedMaterial;
            created = Time;
            previousFlight = missile.FlightSeconds;
        }

        //以游戏刻推进烟焰寿命，点火后按细分轨迹采样真实喷口。
        internal void Tick()
        {
            double now = Time;
            smoke.Tick(now);
            flame.Tick(now);
            if (emitter == null) return;
            float end = emitter.FlightSeconds;
            if (Burning)
            {
                float start = Mathf.Max(previousFlight, emitter.IgnitionSeconds);
                if (!emitting)
                {
                    Sample(start, now - (end - start));
                    emitting = true;
                }
                int samples = Mathf.CeilToInt((end - start) * 240);
                for (int i = 1; i <= samples; i++)
                {
                    float flight = Mathf.Lerp(start, end, i / (float)samples);
                    Sample(flight, now - (end - flight));
                }
            }
            previousFlight = end;
        }

        //向热气历史和烟团距离累积器提交同一喷口，地下部分不生成特效。
        private void Sample(float seconds, double birth)
        {
            emitter.ExhaustPose(seconds, out var rotation, out var tail);
            if (tail.y < 0) return;
            flame.Emit(tail, rotation * Vector3.forward, birth);
            smoke.Emit(tail, birth, emitter.TrailSeed);
        }

        //停止短喷焰和采样，已经生成的热气与烟团继续消散。
        internal void Stop() { emitter = null; }

        //合并烟焰自身的范围，弹体的离屏纹理尺寸保持独立。
        internal bool TryBounds(out Bounds bounds)
        {
            double now = Time;
            bool found = smoke.TryBounds(now, out bounds);
            if (flame.TryBounds(now, FlameScale, out var trailBounds))
            {
                if (found) bounds.Encapsulate(trailBounds);
                else { bounds = trailBounds; found = true; }
            }
            if (Burning)
            {
                emitter.ExhaustPose(emitter.FlightSeconds, out _, out var tail);
                float radius = (settings.flameLength + settings.flameWidth * 0.5f) * FlameScale;
                var coreBounds = new Bounds(tail, Vector3.one * (2 * radius));
                if (found) bounds.Encapsulate(coreBounds);
                else { bounds = coreBounds; found = true; }
            }
            return found;
        }

        //按烟团、热尾流、短喷焰提交三个批次，保留井体与弹体的深度遮挡。
        internal void Submit(CommandBuffer commands, Camera camera)
        {
            double now = Time;
            float clock = (float)(now - created);
            PrepareProperties(clock, true, false, settings.smokeOpacity);
            SubmitBatch(commands, smoke.Build(camera, now), smokeMaterial);
            PrepareProperties(clock, true, true, 0.9f);
            SubmitBatch(commands, flame.Build(camera, now, clock, FlameScale), flameMaterial);
            if (Burning) SubmitCore(commands, camera, clock);
        }

        //重置每次绘制使用的着色参数，烟团年龄来自各自顶点，不互相覆盖。
        private void PrepareProperties(float clock, bool vertexData, bool trail, float opacity)
        {
            properties.Clear();
            properties.SetFloat("_Clock", clock);
            properties.SetFloat("_VertexData", vertexData ? 1 : 0);
            properties.SetFloat("_Trail", trail ? 1 : 0);
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_FlameBrightness", settings.flameBrightness);
        }

        //把历史气流和烟团放到共享离屏空间，一条尾迹只提交一个对应材质的网格。
        private void SubmitBatch(CommandBuffer commands, Mesh mesh, Material material)
        {
            if (mesh) commands.DrawMesh(mesh, Matrix4x4.Translate(OffscreenTurretRenderer.Offset), material, 0, 0, properties);
        }

        //只让短喷焰跟随当前喷口，较长的发光尾流由真实喷口历史形成。
        private void SubmitCore(CommandBuffer commands, Camera camera, float clock)
        {
            emitter.ExhaustPose(emitter.FlightSeconds, out var rotation, out var tail);
            Vector3 projected = Vector3.ProjectOnPlane(rotation * Vector3.forward, camera.transform.forward);
            Quaternion billboard = projected.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(camera.transform.forward, projected) : camera.transform.rotation;
            float scale = FlameScale, length = settings.flameLength * scale;
            PrepareProperties(clock, false, false, 1);
            properties.SetFloat("_Age", emitter.EngineSeconds);
            properties.SetFloat("_Seed", emitter.TrailSeed);
            Vector3 center = tail - billboard * Vector3.up * (length * 0.5f);
            commands.DrawMesh(coreMesh, Matrix4x4.TRS(center + OffscreenTurretRenderer.Offset, billboard,
                new Vector3(settings.flameWidth * scale, length, 1)), flameMaterial, 0, 0, properties);
        }

        //释放地图显示资源，飞行历史和烟团仍保留在模拟层。
        internal void ReleaseDisplay() { Frame.Dispose(); smoke.ReleaseDisplay(); flame.ReleaseDisplay(); }

        //结束尾迹生命周期并释放全部绘制资源。
        public void Dispose() { ReleaseDisplay(); }
    }
}

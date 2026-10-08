using RimWorld;
using SSR.UnityComponent.Outline;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //在炮塔共享相机上分别采集井体、飞行弹体和透明烟焰。
    internal sealed partial class OffscreenTurretRenderer
    {
        private Material missileOccluder;

        //合并投影仍重叠的弹体与井体，让颜色、法线和描边共用遮挡关系。
        internal void DrawSilo(Camera camera, SiloVisual silo, MissileBodyVisual[] attached)
        {
            colorCommands.Clear();
            geometryCommands.Clear();
            var offset = Matrix4x4.Translate(Offset);
            silo.Submission.Draw(colorCommands, geometryCommands, geometryMaterial, offset, out _);
            foreach (var missile in attached) missile.Submission.Draw(colorCommands, geometryCommands, geometryMaterial, offset, out _);
            var frame = silo.Submission.Frame;
            frame.ConfigureWorld(capture, camera, silo.Bounds, silo.Owner.GroundOrigin, MapProjectionPlane.Height);
            Render(frame, true);
            plane.Draw(camera, frame);
        }

        //投影分离后使用紧凑的弹体范围采集，保留原材质、光影和外轮廓。
        internal void DrawMissile(Camera camera, MissileBodyVisual missile)
        {
            colorCommands.Clear();
            geometryCommands.Clear();
            missile.Submission.Draw(colorCommands, geometryCommands, geometryMaterial, Matrix4x4.Translate(Offset), out _);
            var frame = missile.Submission.Frame;
            frame.ConfigureWorld(capture, camera, missile.Bounds, missile.Owner.Trajectory.Anchor, AltitudeLayer.Projectile.AltitudeFor());
            Render(frame, true);
            plane.Draw(camera, frame);
        }

        //透明烟焰只执行实体深度遮挡和预乘合成，不进入接触阴影或描边通道。
        internal void DrawExhaust(Camera camera, MissileExhaust exhaust, Bounds bounds, SiloVisual silo, MissileBodyVisual body)
        {
            if (!missileOccluder)
                missileOccluder = new Material(RequireShader(CombatAssetBundle.Require(), "MissileOccluder"));
            float altitude = body != null && !body.Owner.ClearOfSilo ? MapProjectionPlane.Height + 0.001f : AltitudeLayer.Projectile.AltitudeFor() - 0.001f;
            exhaust.Frame.ConfigureWorld(capture, camera, bounds, exhaust.Anchor, altitude);
            colorCommands.Clear();
            var offset = Matrix4x4.Translate(Offset);
            silo?.Submission.DrawDepth(colorCommands, missileOccluder, offset);
            body?.Submission.DrawDepth(colorCommands, missileOccluder, offset);
            exhaust.Submit(colorCommands, capture);
            var previous = RenderTexture.active;
            try { Capture(exhaust.Frame.Output, colorCommands); }
            finally { capture.targetTexture = null; RenderTexture.active = previous; }
            plane.Draw(camera, exhaust.Frame);
        }
    }
}

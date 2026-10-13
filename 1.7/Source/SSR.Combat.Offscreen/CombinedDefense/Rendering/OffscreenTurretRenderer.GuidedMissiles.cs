using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //让制导弹体与导弹井共用实体采集、轮廓合成和透明烟焰通道。
    internal sealed partial class OffscreenTurretRenderer
    {
        //按完整弹体范围采集飞行姿态，将真实高度投影到地图上。
        internal void DrawGuidedMissile(Camera camera, GuidedMissileBodyVisual body)
        {
            colorCommands.Clear();
            body.Submission.Draw(colorCommands, Matrix4x4.Translate(Offset), out _);
            var frame = body.Submission.Frame;
            frame.ConfigureWorld(capture, camera, body.Bounds, body.Owner.Flight.Anchor, AltitudeLayer.Projectile.AltitudeFor());
            Render(frame, true);
            plane.Draw(camera, frame);
        }

        //烟焰使用真实喷口历史，发射架和弹体的深度共同遮挡筒内特效。
        internal void DrawGuidedExhaust(Camera camera, MissileExhaust exhaust, Bounds bounds,
            OffscreenTurretComp turret, GuidedMissileBodyVisual body)
        {
            if (missileOccluder == null) missileOccluder = new Material(SsrShaders.Require("SSR/Missiles/Occluder"), "SSR烟焰遮挡");
            exhaust.Frame.ConfigureWorld(capture, camera, bounds, exhaust.Anchor, AltitudeLayer.Projectile.AltitudeFor() - 0.001f);
            colorCommands.Clear();
            var offset = Matrix4x4.Translate(Offset);
            turret?.Submission.DrawDepth(colorCommands, missileOccluder, offset);
            body?.Submission.DrawDepth(colorCommands, missileOccluder, offset);
            exhaust.Submit(colorCommands, capture);
            Capture(exhaust.Frame.Output, colorCommands);
            plane.Draw(camera, exhaust.Frame);
        }
    }
}

using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //保留逻辑模型的地图坐标，仅把可见炮塔提交给远处的离屏采集相机。
    public sealed class OffscreenTurretGraphic : Graphic_Single
    {
        //同步地图位置和缩放，由原版可见性检查决定本帧是否提交炮塔。
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            var comp = thing?.TryGetComp<OffscreenTurretComp>();
            if (comp == null)
            {
                base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
                return;
            }
            var model = comp.CurrentUnityObject;
            if (!model) return;
            model.ownTransform.position = loc + DrawOffset(rot);
            model.ownTransform.localScale = new Vector3(drawSize.x, 1, drawSize.y);
            model.ownTransform.rotation = Quaternion.Euler(0, extraRotation + rot.AsAngle, 0);
            model.expirationTick = 6000;
            comp.LastDrawFrame = Time.frameCount;
        }

        //炮塔颜色来自模型材质，染色图形仍使用同一离屏入口。
        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo) { return this; }
    }
}

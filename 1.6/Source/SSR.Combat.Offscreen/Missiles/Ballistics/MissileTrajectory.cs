using SSR.UnityComponent.Missiles;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //以弹头为轨迹参考，连接冷弹射、点火抬升、弧顶转向和斜向加速俯冲。
    internal sealed class MissileTrajectory : IExposable
    {
        internal Vector3 Anchor, Scale, TipOffset, TrailOffset;
        private Quaternion initialRotation;
        private MissileAscent ascent;
        private MissileTurnArc turn;
        private float tailZ;
        internal float Duration => ascent.Duration + turn.Duration;
        internal float IgnitionSeconds => ascent.IgnitionDelay;
        internal static float GroundProjection => (TurretCaptureProfile.Direction * Vector3.up).z;

        //为读档提供无参实例，轨迹控制点随后由存档填充。
        public MissileTrajectory() { }

        //从构建布局生成完整轨迹，将地图落点转换到统一的正交采集空间。
        internal MissileTrajectory(Building_MissileSilo silo, SiloSlotData slot, IntVec3 impact)
        {
            Anchor = silo.GroundOrigin;
            Quaternion yaw = silo.ModelRotation;
            Scale = slot.scale;
            TipOffset = Vector3.Scale(slot.tip, Scale);
            TrailOffset = Vector3.Scale(slot.trail, Scale);
            tailZ = slot.tailZ * Scale.z;
            initialRotation = yaw * slot.rotation;
            Vector3 center = Anchor + yaw * slot.position;
            var cold = new MissileColdLaunch(silo.Settings, center, initialRotation, TipOffset,
                tailZ, silo.Assets.Layout.clearanceHeight);
            ascent = new MissileAscent(silo.Settings, cold);
            Vector3 impactPoint = impact.ToVector3Shifted();
            impactPoint.z = Anchor.z + (impactPoint.z - Anchor.z) / GroundProjection;
            impactPoint.y = 0;
            turn = new MissileTurnArc(silo.Settings, ascent.EndPosition, impactPoint, ascent.EndSpeed);
        }

        //按游戏时间求弹头位置和切线，不使用依赖帧率的累积积分。
        internal void Evaluate(float seconds, out Vector3 tip, out Quaternion rotation)
        {
            if (seconds <= ascent.Duration)
            {
                ascent.Evaluate(seconds, out tip, out rotation);
                return;
            }
            turn.Evaluate(seconds - ascent.Duration, out tip, out var velocity);
            rotation = turn.Facing(velocity) * initialRotation;
        }

        //将采集空间的水平位置还原为地图位置，保证弹头落在指定格中心。
        internal Vector3 ProjectGround(Vector3 point)
        {
            return new Vector3(point.x, 0, Anchor.z + (point.z - Anchor.z) * GroundProjection);
        }

        //返回弹体根节点和尾焰挂点，同时计算包括尾翼的最低点。
        internal void Pose(float seconds, out Vector3 position, out Quaternion rotation, out Vector3 trail, out float tailHeight)
        {
            Evaluate(seconds, out var tip, out rotation);
            position = tip - rotation * TipOffset;
            trail = position + rotation * TrailOffset;
            tailHeight = (position + rotation * new Vector3(0, 0, tailZ)).y;
        }

        //保存既定控制点和模型尺寸，读档不重新计算或随机改变弹道。
        public void ExposeData()
        {
            Scribe_Values.Look(ref Anchor, "anchor");
            Scribe_Values.Look(ref Scale, "scale");
            Scribe_Values.Look(ref TipOffset, "tipOffset");
            Scribe_Values.Look(ref TrailOffset, "trailOffset");
            Scribe_Values.Look(ref initialRotation, "initialRotation");
            Scribe_Deep.Look(ref ascent, "ascent");
            Scribe_Deep.Look(ref turn, "turn");
            Scribe_Values.Look(ref tailZ, "tailZ");
        }
    }
}

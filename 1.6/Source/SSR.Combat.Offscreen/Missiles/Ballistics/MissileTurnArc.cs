using System;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //朝锁定落点规划一次单向转弯和相切俯冲，按同一加速度公式累积全程弧长。
    internal sealed class MissileTurnArc : IExposable
    {
        private Vector3 start, heading, end, impact;
        private float radius, angle, arcLength, diveLength, entrySpeed, acceleration, duration;
        private MissileTurnPath path;
        internal float Duration => duration;

        //为读档提供路径实例，几何和动力参数由存档恢复。
        public MissileTurnArc() { }

        //按实际高度和目标位置求切线圆弧，直接继承上升出口速度。
        internal MissileTurnArc(SiloSettings settings, Vector3 origin, Vector3 destination, float speed)
        {
            start = origin;
            impact = destination;
            entrySpeed = speed;
            acceleration = settings.flightAcceleration;
            Vector3 horizontal = impact - start;
            horizontal.y = 0;
            float distance = horizontal.magnitude;
            if (distance <= 0.001f || entrySpeed <= 0)
                throw new InvalidOperationException("导弹落点与发射舱位重合或上升出口速度无效，无法建立转弯路径。");
            heading = horizontal / distance;
            float height = start.y - impact.y;
            //半径必须小于航程的一半，转弯角才会小于一百八十度，出弯位置也不会越过目标。
            //短程优先满足单向抵达约束，不能为保留最小半径而先飞过落点再折返。
            radius = Mathf.Min(Mathf.Max(distance * settings.turnRadiusFraction, settings.minimumTurnRadius), distance * 0.48f);
            float centerToTarget = distance - radius;
            angle = Mathf.PI + Mathf.Asin(radius / Mathf.Sqrt(height * height + centerToTarget * centerToTarget))
                - Mathf.Atan2(centerToTarget, height);
            end = PointAt(angle);
            RebuildPath();
            arcLength = path.Length;
            diveLength = Vector3.Distance(end, impact);
            float length = arcLength + diveLength;
            duration = 2 * length / (entrySpeed + Mathf.Sqrt(entrySpeed * entrySpeed + 2 * acceleration * length));
        }

        //按圆弧角度求位置，起始切线保持竖直向上。
        private Vector3 PointAt(float radians)
        {
            return start + heading * (radius * (1 - Mathf.Cos(radians))) + Vector3.up * (radius * Mathf.Sin(radians));
        }

        //从确定的几何数据构造平滑转向，不为保存弧长查找表扩大存档。
        private void RebuildPath()
        {
            Vector3 exit = heading * Mathf.Sin(angle) + Vector3.up * Mathf.Cos(angle);
            path = new MissileTurnPath(start, end, exit, radius * angle);
        }

        //围绕固定转向轴求姿态，越过最高点或倒转竖直方向时不随机翻滚。
        internal Quaternion Facing(Vector3 velocity)
        {
            float pitch = Mathf.Atan2(Vector3.Dot(velocity, heading), velocity.y) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(pitch, Vector3.Cross(Vector3.up, heading));
        }

        //用位移等于初速度乘时间加二分之一加速度乘时间平方，跨阶段保持速度连续增长。
        internal void Evaluate(float seconds, out Vector3 position, out Vector3 velocity)
        {
            float t = Mathf.Clamp(seconds, 0, duration);
            float travel = entrySpeed * t + 0.5f * acceleration * t * t;
            float speed = entrySpeed + acceleration * t;
            if (travel < arcLength)
            {
                path.Evaluate(travel, out position, out var direction);
                velocity = direction * speed;
            }
            else
            {
                position = Vector3.LerpUnclamped(end, impact, Mathf.Clamp01((travel - arcLength) / diveLength));
                velocity = (impact - end) * (speed / diveLength);
            }
        }

        //保存路径几何和统一动力时钟，不在进入俯冲或读档时重置速度。
        public void ExposeData()
        {
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref heading, "heading");
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref impact, "impact");
            Scribe_Values.Look(ref radius, "radius");
            Scribe_Values.Look(ref angle, "angle");
            Scribe_Values.Look(ref arcLength, "arcLength");
            Scribe_Values.Look(ref diveLength, "diveLength");
            Scribe_Values.Look(ref entrySpeed, "entrySpeed");
            Scribe_Values.Look(ref acceleration, "acceleration");
            Scribe_Values.Look(ref duration, "duration");
            if (Scribe.mode == LoadSaveMode.LoadingVars) RebuildPath();
        }
    }
}

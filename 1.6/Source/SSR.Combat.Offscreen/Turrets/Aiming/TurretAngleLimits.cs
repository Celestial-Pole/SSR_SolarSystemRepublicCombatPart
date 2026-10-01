using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //处理跨越零度的机械转角范围，防止限位炮塔走过禁止区域。
    internal static class TurretAngleLimits
    {
        //将角度限制在配置圆弧内，两个相同端点表示允许整周旋转。
        internal static float Clamp(float angle, Vector2 range)
        {
            angle = Mathf.Repeat(angle, 360);
            float extent = Mathf.Repeat(range.y - range.x, 360);
            if (extent < 0.0001f) return angle;
            float offset = Mathf.Repeat(angle - range.x, 360);
            if (offset <= extent) return angle;
            return Mathf.Repeat(Mathf.Abs(Mathf.DeltaAngle(angle, range.x)) < Mathf.Abs(Mathf.DeltaAngle(angle, range.y))
                ? range.x : range.y, 360);
        }

        //求允许圆弧内的转动距离，全周转轴使用最短转向。
        internal static float Delta(float current, float target, Vector2 range)
        {
            if (Mathf.Repeat(range.y - range.x, 360) < 0.0001f) return Mathf.DeltaAngle(current, target);
            return Mathf.Repeat(Clamp(target, range) - range.x, 360) - Mathf.Repeat(Clamp(current, range) - range.x, 360);
        }

        //按最大角速度和角加速度推进单个轴，接近目标时按制动距离减速。
        internal static float Advance(float current, float target, ref float velocity, float speed, float acceleration, Vector2 range)
        {
            const float seconds = 1f / 60;
            float delta = Delta(current, target, range);
            if (speed <= 0) { velocity = 0; return current; }
            float desired = Mathf.Sign(delta) * (acceleration > 0 ? Mathf.Min(speed, Mathf.Sqrt(2 * acceleration * Mathf.Abs(delta))) : speed);
            float next = acceleration > 0 ? Mathf.MoveTowards(velocity, desired, acceleration * seconds) : desired;
            float step = acceleration > 0 ? (velocity + next) * 0.5f * seconds : next * seconds;
            if (Mathf.Abs(delta) < 0.0001f || Mathf.Sign(step) == Mathf.Sign(delta) && Mathf.Abs(step) >= Mathf.Abs(delta))
            { velocity = 0; return Clamp(target, range); }
            float value = Clamp(current + step, range);
            velocity = Mathf.Abs(Mathf.DeltaAngle(current + step, value)) > 0.001f ? 0 : next;
            return value;
        }
    }
}

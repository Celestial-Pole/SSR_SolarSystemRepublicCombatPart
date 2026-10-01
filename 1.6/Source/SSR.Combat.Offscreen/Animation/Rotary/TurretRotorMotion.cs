using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //维护转轮的逐刻相位和速度，使加减速终点与指定开火位置连续衔接。
    internal sealed class TurretRotorMotion : IExposable
    {
        private float angle, speed, ratedSpeed;
        private float startAngle, startSpeed, endAngle, endSpeed;
        private int rampTick, rampDuration;
        private bool requested, ramping;

        //供存档系统构造独立的机械运动状态。
        public TurretRotorMotion() { }

        //按逻辑刻推进机械状态，启动阶段结束后保持与逐发间隔对应的恒定转速。
        internal void Tick(bool running, float degreesPerTick, float step, float phase, int upTicks, int downTicks)
        {
            if (running != requested || !Mathf.Approximately(ratedSpeed, degreesPerTick))
            {
                requested = running;
                ratedSpeed = degreesPerTick;
                BeginRamp(running, step, phase, running ? upTicks : downTicks);
            }
            if (!ramping) { angle = Mathf.Repeat(angle + speed, 360); return; }
            rampTick++;
            Evaluate(rampTick, out angle, out speed);
            if (rampTick < rampDuration) return;
            angle = Mathf.Repeat(endAngle, 360);
            speed = endSpeed;
            ramping = false;
        }

        //在加速结束且枪管处于开火站位时允许实际发射。
        internal bool AtFiringPosition(float step, float phase)
        {
            float offset = Mathf.Repeat(angle - phase, step);
            return requested && !ramping && Mathf.Min(offset, step - offset) < 0.01f;
        }

        //采样当前逻辑刻之后的显示姿态，不改变逐发判断使用的机械状态。
        internal float Sample(float fraction)
        {
            if (!ramping) return angle + speed * fraction;
            Evaluate(Mathf.Min(rampTick + fraction, rampDuration), out float sample, out _);
            return sample;
        }

        //提供显示姿态对应的瞬时角速度，照明采样不改变机械状态。
        internal float SampleSpeed(float fraction)
        {
            if (!ramping) return speed;
            Evaluate(Mathf.Min(rampTick + fraction, rampDuration), out _, out float velocity);
            return velocity;
        }

        //选取前方最近的开火相位，并用连续曲线在启停时间内达到目标速度。
        private void BeginRamp(bool running, float step, float phase, int duration)
        {
            startAngle = angle;
            startSpeed = speed;
            endSpeed = running ? ratedSpeed : 0;
            rampDuration = duration;
            rampTick = 0;
            endAngle = startAngle + (startSpeed + endSpeed) * duration * 0.5f;
            if (running && step > 0)
                endAngle = Mathf.Ceil((endAngle - phase) / step) * step + phase;
            ramping = true;
        }

        //使用三次厄米曲线同时约束起止角度和速度，避免对齐枪管时突然跳转。
        private void Evaluate(float tick, out float position, out float velocity)
        {
            float t = tick / rampDuration, t2 = t * t, t3 = t2 * t;
            position = (2 * t3 - 3 * t2 + 1) * startAngle
                + (t3 - 2 * t2 + t) * startSpeed * rampDuration
                + (-2 * t3 + 3 * t2) * endAngle
                + (t3 - t2) * endSpeed * rampDuration;
            velocity = ((6 * t2 - 6 * t) * startAngle
                + (3 * t2 - 4 * t + 1) * startSpeed * rampDuration
                + (-6 * t2 + 6 * t) * endAngle
                + (3 * t2 - 2 * t) * endSpeed * rampDuration) / rampDuration;
        }

        //保存转轮相位、速度和启停曲线，读档后继续原有机械状态。
        public void ExposeData()
        {
            Scribe_Values.Look(ref angle, "angle");
            Scribe_Values.Look(ref speed, "speed");
            Scribe_Values.Look(ref ratedSpeed, "ratedSpeed");
            Scribe_Values.Look(ref startAngle, "startAngle");
            Scribe_Values.Look(ref startSpeed, "startSpeed");
            Scribe_Values.Look(ref endAngle, "endAngle");
            Scribe_Values.Look(ref endSpeed, "endSpeed");
            Scribe_Values.Look(ref rampTick, "rampTick");
            Scribe_Values.Look(ref rampDuration, "rampDuration");
            Scribe_Values.Look(ref requested, "requested");
            Scribe_Values.Look(ref ramping, "ramping");
        }
    }
}

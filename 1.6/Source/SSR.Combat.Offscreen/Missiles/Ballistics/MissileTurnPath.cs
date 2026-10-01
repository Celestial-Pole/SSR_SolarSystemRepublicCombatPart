using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //用单向五阶曲线连接竖直线与俯冲线，两端曲率为零，并按弧长查询连续转向的位置。
    internal sealed class MissileTurnPath
    {
        private const int Samples = 256;
        private readonly Vector3[] points = new Vector3[6];
        private readonly float[] lengths = new float[Samples + 1];
        internal float Length => lengths[Samples];

        //以参考圆弧的切线构造控制点，出入弯处的角速度从零开始并回到零。
        internal MissileTurnPath(Vector3 start, Vector3 end, Vector3 exitDirection, float referenceLength)
        {
            points[0] = start;
            points[1] = start + Vector3.up * (referenceLength / 5);
            points[2] = 2 * points[1] - start;
            points[5] = end;
            points[4] = end - exitDirection * (referenceLength / 5);
            points[3] = 2 * points[4] - end;
            //参考转角小于一百八十度时，控制边方向依次为竖直、半转角、出弯方向。
            //控制点沿目标方向递增，切线只向同一侧压低，不形成折返或第二个弯。
            Vector3 previous = start;
            for (int i = 1; i <= Samples; i++)
            {
                Vector3 current = PositionAt(i / (float)Samples);
                lengths[i] = lengths[i - 1] + Vector3.Distance(previous, current);
                previous = current;
            }
        }

        //计算五阶曲线位置，控制点保证两端的位置、切线和零曲率约束。
        private Vector3 PositionAt(float t)
        {
            float u = 1 - t, u2 = u * u, t2 = t * t;
            return points[0] * (u2 * u2 * u) + points[1] * (5 * u2 * u2 * t)
                + points[2] * (10 * u2 * u * t2) + points[3] * (10 * u2 * t2 * t)
                + points[4] * (5 * u * t2 * t2) + points[5] * (t2 * t2 * t);
        }

        //对曲线求导得到朝向，方向与当前速度大小分离。
        private Vector3 TangentAt(float t)
        {
            float u = 1 - t, u2 = u * u, t2 = t * t;
            return ((points[1] - points[0]) * (u2 * u2)
                + (points[2] - points[1]) * (4 * u2 * u * t)
                + (points[3] - points[2]) * (6 * u2 * t2)
                + (points[4] - points[3]) * (4 * u * t2 * t)
                + (points[5] - points[4]) * (t2 * t2)).normalized;
        }

        //二分定位弧长再还原参数，防止直接按曲线参数推进引发视觉上的忽快忽慢。
        internal void Evaluate(float distance, out Vector3 position, out Vector3 direction)
        {
            float travel = Mathf.Clamp(distance, 0, Length);
            int low = 0, high = Samples;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (lengths[middle] < travel) low = middle;
                else high = middle;
            }
            float fraction = (travel - lengths[low]) / (lengths[high] - lengths[low]);
            float t = (low + fraction) / Samples;
            position = PositionAt(t);
            direction = TangentAt(t);
        }
    }
}

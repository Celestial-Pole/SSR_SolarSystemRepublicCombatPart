using System;
using System.Collections.Generic;
using SSR.UnityComponent.Outline;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //按采集分辨率复用中间缓冲，避免不同大小的炮塔交替绘制时反复创建纹理。
    internal sealed class TurretBufferPool : IDisposable
    {
        private readonly Dictionary<int, OutlineBuffers> buffers = new Dictionary<int, OutlineBuffers>();
        private readonly Dictionary<int, int> lastUsed = new Dictionary<int, int>();
        private readonly List<int> expired = new List<int>();

        //获取指定精度的颜色、几何与合成缓冲，并记录本帧使用状态。
        internal OutlineBuffers Get(int size)
        {
            if (!buffers.TryGetValue(size, out var value))
            {
                value = new OutlineBuffers();
                buffers.Add(size, value);
                value.Ensure(size, size);
            }
            lastUsed[size] = Time.frameCount;
            return value;
        }

        //释放连续一百二十帧未使用的精度档位，避免退出近景后持续占用大纹理显存。
        internal void ReleaseUnused()
        {
            expired.Clear();
            foreach (var pair in lastUsed)
                if (Time.frameCount - pair.Value > 120) expired.Add(pair.Key);
            foreach (int size in expired)
            {
                buffers[size].Dispose();
                buffers.Remove(size);
                lastUsed.Remove(size);
            }
        }

        //释放渲染器持有的所有采集档位。
        public void Dispose()
        {
            foreach (var value in buffers.Values) value.Dispose();
            buffers.Clear();
            lastUsed.Clear();
            expired.Clear();
        }
    }
}

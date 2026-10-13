using System;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //按实际发射事件采样后坐片段，游戏暂停、加速和复位都由游戏时间决定。
    internal sealed class TurretRecoilPlayback
    {
        private readonly GameObject target;
        private readonly AnimationClip clip;
        private int shotTick = -1;

        //读取原生动画曲线，并将后坐节点采样到起始姿态。
        internal TurretRecoilPlayback(GameObject model, string transformPath, string clipName)
        {
            var node = model.transform.FindModelNode(transformPath);
            if (!node) throw new InvalidOperationException("找不到炮塔后坐节点：" + transformPath);
            target = node.gameObject;
            clip = SsrAnimationClip.Load(clipName);
            clip.SampleAnimation(target, 0);
        }

        //成功发射后从片段起点播放一次，连续发射时以最新一发重新开始后坐。
        internal void NotifyShot()
        {
            shotTick = Find.TickManager.TicksGame;
            clip.SampleAnimation(target, 0);
        }

        //按游戏刻推进当前片段，播放完成后回到初始位置并停止采样。
        internal void Tick()
        {
            if (shotTick < 0 || !target) return;
            float elapsed = (Find.TickManager.TicksGame - shotTick) / 60f;
            if (elapsed >= clip.length)
            {
                clip.SampleAnimation(target, 0);
                shotTick = -1;
                return;
            }
            clip.SampleAnimation(target, elapsed);
        }
    }
}

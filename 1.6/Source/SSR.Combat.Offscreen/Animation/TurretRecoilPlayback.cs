using System;
using System.Linq;
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

        //从预制体取得原后坐片段，并移除会受旧状态布尔值驱动的自动 Animator。
        internal TurretRecoilPlayback(GameObject model, string transformPath, string clipName)
        {
            var node = model.transform.Find(transformPath);
            if (!node) throw new InvalidOperationException("找不到炮塔后坐节点：" + transformPath);
            target = node.gameObject;
            var animator = target.GetComponent<Animator>();
            if (!animator || !animator.runtimeAnimatorController)
                throw new InvalidOperationException("炮塔后坐节点缺少动画控制器：" + transformPath);
            clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(value => value.name == clipName);
            if (!clip || clip.length <= 0)
                throw new InvalidOperationException("找不到有效的炮塔后坐片段：" + clipName);
            //只移除实例组件，资源包中的原片段继续作为逐发动画的数据来源。
            animator.enabled = false;
            UnityEngine.Object.Destroy(animator);
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

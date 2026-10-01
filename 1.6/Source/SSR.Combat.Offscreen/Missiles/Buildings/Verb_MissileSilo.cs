using Verse;

namespace SSR.Combat.Offscreen
{
    //只向控制器提交一次固定目标的齐射请求，不使用原版多发弹丸计时。
    public sealed class Verb_MissileSilo : Verb_LaunchProjectile
    {
        //仅在闭盖装填完成且允许开火时接受原版索敌结果。
        public override bool Available()
        {
            var silo = (Building_MissileSilo)caster;
            return base.Available() && silo.CanContinue && silo.Controller.Ready;
        }

        //锁定本轮落点，后续逐发由建筑的游戏刻驱动。
        protected override bool TryCastShot() { return ((Building_MissileSilo)caster).Controller.Begin(currentTarget); }
    }
}

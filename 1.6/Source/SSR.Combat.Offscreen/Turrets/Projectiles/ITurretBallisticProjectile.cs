namespace SSR.Combat.Offscreen
{
    //向原版碰撞入口提供电磁弹的真实飞行高度，限制接管范围。
    internal interface ITurretBallisticProjectile
    {
        TurretProjectileFlight Flight { get; }
        int RemainingTicks { get; }
    }
}

namespace SSR.Combat.Offscreen
{
    //供三维弹丸、飞行器或其组件向炮塔暴露真实位置，返回假表示暂时不能被瞄准。
    public interface ITurretSpatialTarget
    {
        //读取当前游戏刻的目标状态，不能在此推进飞行时间或修改地图。
        bool TryGetTurretTarget(out TurretTargetState state);
    }
}

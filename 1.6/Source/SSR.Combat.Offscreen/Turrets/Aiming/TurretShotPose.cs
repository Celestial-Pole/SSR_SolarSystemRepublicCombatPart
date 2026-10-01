using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //保存发射瞬间的模型空间姿态，隔离弹丸飞行和炮塔后续瞄准。
    internal readonly struct TurretShotPose
    {
        internal readonly Vector3 Position, Direction, GroundAnchor, Target;

        //绑定炮口位置、真实前向、投影基准和本次瞄准点。
        internal TurretShotPose(Vector3 position, Vector3 direction, Vector3 groundAnchor, Vector3 target)
        {
            Position = position;
            Direction = direction;
            GroundAnchor = groundAnchor;
            Target = target;
        }
    }
}

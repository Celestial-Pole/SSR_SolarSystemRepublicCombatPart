using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //保存统一的地面投影、离地高度和速度，纵坐标表示真实高度而非地图绘制层。
    public readonly struct TurretTargetState
    {
        public readonly TurretTargetKind Kind;
        public readonly Vector3 GroundPosition;
        public readonly float Height;
        public readonly Vector3 Velocity;

        //建立以格和游戏秒为单位的目标状态，速度的纵分量为高度变化率。
        public TurretTargetState(TurretTargetKind kind, Vector3 groundPosition, float height, Vector3 velocity)
        {
            Kind = kind;
            GroundPosition = new Vector3(groundPosition.x, 0, groundPosition.z);
            Height = height;
            Velocity = velocity;
        }

        //得到用于速度差分的三维位置。
        internal Vector3 Position => new Vector3(GroundPosition.x, Height, GroundPosition.z);
    }
}

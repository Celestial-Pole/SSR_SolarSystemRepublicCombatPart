using RimWorld;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //按固定炮口方向推进三维直线弹道，统一绘制、地面碰撞和空中目标扫掠。
    internal sealed class TurretProjectileFlight : IExposable
    {
        private Vector3 start, direction, anchor;
        private float speed, distance;
        private int duration;
        private static readonly Vector3 Projection = TurretCaptureProfile.Direction * Vector3.up;
        internal int Duration => duration;

        //提供深度存档所需的构造入口，发射时由初始化方法填入姿态。
        public TurretProjectileFlight() { }

        //使用真实炮口建立航迹，保留原版水平方向散布，俯仰由发射时的模型决定。
        internal void Initialize(Building_ConfigurableTurret turret, LocalTargetInfo used, LocalTargetInfo intended,
            float tilesPerTick, float extraRange, bool penetrating)
        {
            TurretShotPose pose = turret.Aim.CaptureShot(intended);
            start = pose.Position;
            direction = pose.Direction;
            anchor = pose.GroundAnchor;
            speed = tilesPerTick;
            if (used.Cell != intended.Cell)
            {
                Vector3 aim = pose.Target - start;
                Vector3 miss = aim + new Vector3(used.Cell.x - intended.Cell.x, 0,
                    (used.Cell.z - intended.Cell.z) / Projection.z);
                direction = Quaternion.AngleAxis(Vector3.SignedAngle(aim.Yto0(), miss.Yto0(), Vector3.up), Vector3.up) * direction;
            }
            distance = Mathf.Max(0.01f, Vector3.Dot(pose.Target - start, direction));
            if (penetrating)
            {
                float horizontal = new Vector2(direction.x, direction.z * Projection.z).magnitude;
                distance = Mathf.Max(distance + extraRange, (turret.AttackVerb.verbProps.range + extraRange) / Mathf.Max(0.01f, horizontal));
            }
            //俯射弹抵达地面即结束，不能穿过地面继续沿二维地图前进。
            if (direction.y < -0.0001f)
                distance = Mathf.Min(distance, Mathf.Max(0.01f, (anchor.y - start.y) / direction.y));
            duration = Mathf.Max(1, Mathf.CeilToInt(distance / speed));
        }

        //按真实三维速度计算位置，最后一刻限制在终点而非越过地面。
        private Vector3 WorldPosition(int remaining)
        {
            return start + direction * Mathf.Min(distance, Mathf.Max(0, duration - remaining) * speed);
        }

        //将模型空间压缩为地图地面坐标，高度独立保存而非混入地图格索引。
        internal Vector3 GroundPosition(int remaining, float altitude)
        {
            Vector3 world = WorldPosition(remaining);
            return new Vector3(world.x, altitude, anchor.z + (world.z - anchor.z) * Projection.z);
        }

        //投影真实高度，使弹丸起点和飞行方向与模型炮口一致。
        internal Vector3 DrawPosition(int remaining, float altitude)
        {
            Vector3 point = GroundPosition(remaining, altitude);
            point.z += Height(remaining) * Projection.y;
            return point;
        }

        //返回当前离地高度，原版绘制层不参与弹道计算。
        internal float Height(int remaining) => Mathf.Max(0, WorldPosition(remaining).y - anchor.y);

        //按投影后的三维速度定向弹丸贴图。
        internal Quaternion Rotation => Quaternion.Euler(0, Mathf.Atan2(direction.x,
            direction.z * Projection.z + direction.y * Projection.y) * Mathf.Rad2Deg, 0);

        //仅允许处在同一高度范围内的地面实体参与原版穿透和碰撞判定。
        internal bool AllowsGroundHit(Thing target, int remaining)
        {
            if (TurretTargetResolver.IsAirTarget(target)) return false;
            Vector3 from = GroundPosition(remaining + 1, 0), to = GroundPosition(remaining, 0);
            Vector3 segment = to - from;
            float fraction = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(target.TrueCenter().Yto0() - from, segment) / segment.sqrMagnitude) : 1;
            float height = Mathf.Lerp(Height(remaining + 1), Height(remaining), fraction);
            float bodyHeight = target is Pawn pawn ? 1.7f * Mathf.Sqrt(pawn.BodySize)
                : target is Plant ? 3f : Mathf.Max(1.5f, target.def.fillPercent * 2);
            return height <= bodyHeight;
        }

        //扫掠整刻的三维线段，避免高速电磁弹跨过高空目标而漏判。
        internal bool SweepsAirTarget(Projectile projectile, LocalTargetInfo target, int remaining)
        {
            if (!target.HasThing || !target.Thing.Spawned || target.Thing.Map != projectile.Map
                || (projectile.HitFlags & ProjectileHitFlags.IntendedTarget) == 0
                || !TurretTargetResolver.IsAirTarget(target.Thing)
                || !TurretTargetResolver.TryRead(target, 0, out var state)) return false;
            Vector3 point = new Vector3(state.GroundPosition.x, anchor.y + state.Height,
                anchor.z + (state.GroundPosition.z - anchor.z) / Projection.z);
            Vector3 relativeStart = WorldPosition(remaining) - point;
            Vector3 velocity = new Vector3(state.Velocity.x, state.Velocity.y, state.Velocity.z / Projection.z) / 60;
            Vector3 segment = WorldPosition(remaining - 1) - WorldPosition(remaining) - velocity;
            float fraction = segment.sqrMagnitude > 0 ? Mathf.Clamp01(-Vector3.Dot(relativeStart, segment) / segment.sqrMagnitude) : 0;
            float radius = Mathf.Max(0.75f, Mathf.Max(target.Thing.def.size.x, target.Thing.def.size.z) * 0.5f);
            return (relativeStart + segment * fraction).sqrMagnitude <= radius * radius;
        }

        //保存独立航迹，读档后继续沿发射时的方向飞行。
        public void ExposeData()
        {
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref speed, "speed");
            Scribe_Values.Look(ref distance, "distance");
            Scribe_Values.Look(ref duration, "duration");
        }
    }
}

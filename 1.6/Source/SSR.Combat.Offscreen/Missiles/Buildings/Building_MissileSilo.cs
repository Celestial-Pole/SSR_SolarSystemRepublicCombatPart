using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //继承原版炮塔的索敌和操作命令，将装填、开盖和发射交给导弹井控制器。
    public sealed class Building_MissileSilo : Building_TurretGun
    {
        private static readonly AccessTools.FieldRef<Building_TurretGun, bool> ReadHoldFire =
            AccessTools.FieldRefAccess<Building_TurretGun, bool>("holdFire");
        private SiloLaunchController controller;
        internal SiloSettings Settings => def.GetModExtension<SiloSettings>();
        internal SiloAssets Assets => SiloAssets.Get(Settings);
        internal SiloLaunchController Controller => controller ?? (controller = new SiloLaunchController(this));
        internal bool CanContinue => Spawned && Active && !IsStunned && !ReadHoldFire(this)
            && !Map.roofGrid.Roofed(Position) && (GetComp<CompFlickable>()?.SwitchIsOn ?? true);
        internal Vector3 GroundOrigin => new Vector3(DrawPos.x, 0, DrawPos.z);
        //让建筑四向旋转与模型正面偏角共同控制井体及全部发射舱位。
        internal Quaternion ModelRotation => Quaternion.Euler(0, Rotation.AsAngle + Settings.modelYawOffset, 0);
        //将完整动画包围盒转换到当前建筑方向，保留东西向舱盖伸出的采集范围。
        internal Bounds CaptureBounds
        {
            get
            {
                var layout = Assets.Layout;
                Quaternion rotation = ModelRotation;
                Vector3 x = rotation * Vector3.right * layout.captureSize.x;
                Vector3 y = rotation * Vector3.up * layout.captureSize.y;
                Vector3 z = rotation * Vector3.forward * layout.captureSize.z;
                var size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                return new Bounds(GroundOrigin + rotation * layout.captureCenter, size);
            }
        }
        protected override bool CanSetForcedTarget => Faction == Faction.OfPlayer;

        //先确保资源及布局可用，再登记建筑和显示，避免资源错误留下已登记的空模型。
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            _ = Assets;
            base.SpawnSetup(map, respawningAfterLoad);
            map.GetComponent<MissileMapVisuals>().Add(this);
        }

        //先推进原版索敌，再按游戏刻推进独立发射流程。
        protected override void Tick()
        {
            base.Tick();
            Controller.Tick();
        }

        //专用控制器负责冷却，避免原版连发回调另外安排一轮。
        protected override void BurstComplete() { burstCooldownTicksLeft = 0; }

        //保存控制器状态和原版强制目标、停火状态。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref controller, "siloController", this);
        }

        //地图只提交离屏影像，组件仍可绘制开关等原版覆盖标识。
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Map.GetComponent<MissileMapVisuals>().MarkVisible(this);
            Comps_PostDraw();
        }

        //建筑离开地图立即释放模型，已经升空的弹丸保持独立飞行。
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map.GetComponent<MissileMapVisuals>().Remove(this);
            base.DeSpawn(mode);
        }

        //在检查面板显示发射阶段和舱内剩余数量。
        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n导弹井：" + Controller.Status + "\n舱内导弹：" + Controller.LoadedCount + " / 9";
        }

        //固定垂直发射井没有可供预览的旋转和俯仰转轴。
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            var command = new Command_Action
            {
                defaultLabel = "角度预览",
                defaultDesc = "固定垂直发射井，没有旋转或俯仰机构。",
                icon = def.uiIcon,
                groupable = false
            };
            command.Disable("固定垂直发射井，没有旋转或俯仰机构。");
            yield return command;
        }
    }
}

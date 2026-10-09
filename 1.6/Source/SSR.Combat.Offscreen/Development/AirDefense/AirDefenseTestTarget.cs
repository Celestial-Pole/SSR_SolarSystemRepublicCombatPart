using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //提供开发场地的可调高度空中靶标，只供现有防空系统读取目标状态。
    public sealed class AirDefenseTestTarget : Building, ITurretSpatialTarget
    {
        private IntVec3 anchor;
        private int startTick, mode;
        private Vector3 ground, velocity;
        private float height;
        private static readonly string[] Modes = { "低空 3 格", "中空 12 格", "高空 30 格", "爬升下降", "横向飞行", "正上方", "超高 130 格" };

        //记录航迹原点，目标始终保留真实地面位置用于射程和敌我关系判断。
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad) { anchor = Position; startTick = Find.TickManager.TicksGame; }
            UpdateFlight();
        }

        //按游戏刻推进靶标航迹，不修改炮塔角度和索敌结果。
        protected override void Tick() { base.Tick(); UpdateFlight(); }

        //根据选定工况计算位置、真实离地高度和速度，移动实体格子与航迹同步。
        private void UpdateFlight()
        {
            float seconds = (Find.TickManager.TicksGame - startTick) / 60f;
            float phase = seconds * Mathf.PI / 6f;
            ground = anchor.ToVector3Shifted();
            velocity = Vector3.zero;
            height = mode == 0 ? 3 : mode == 1 ? 12 : mode == 2 ? 30 : mode == 6 ? 130 : 12;
            if (mode == 3)
            {
                height = 21.5f + 18.5f * Mathf.Sin(phase);
                velocity.y = 18.5f * Mathf.PI / 6f * Mathf.Cos(phase);
            }
            if (mode == 4)
            {
                ground.x += 12 * Mathf.Sin(phase);
                velocity.x = 2 * Mathf.PI * Mathf.Cos(phase);
            }
            if (mode == 5) { ground.z -= 20; height = 40; }
            Position = ground.ToIntVec3();
        }

        //向生产用目标解析器提供三维目标，准入、机械范围和开火仍由原系统决定。
        public bool TryGetTurretTarget(out TurretTargetState state)
        {
            state = new TurretTargetState(TurretTargetKind.Spatial, ground, height, velocity);
            return Spawned;
        }

        //按炮塔采集相机的高度投影绘制靶心，避免把绘图层级当作实际飞行高度。
        public override Vector3 DrawPos => new Vector3(ground.x, def.altitudeLayer.AltitudeFor(),
            ground.z + height * (TurretCaptureProfile.Direction * Vector3.up).y);

        //显示空中靶心及其地面投影，连接线用于观察当前高度。
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector3 projectedGround = new Vector3(ground.x, drawLoc.y, ground.z);
            GenDraw.DrawLineBetween(projectedGround, drawLoc);
            GenDraw.DrawCircleOutline(projectedGround, 0.4f);
            GenDraw.DrawCircleOutline(drawLoc, 0.8f);
            GenDraw.DrawLineBetween(drawLoc - Vector3.right, drawLoc + Vector3.right);
            GenDraw.DrawLineBetween(drawLoc - Vector3.forward, drawLoc + Vector3.forward);
        }

        //吸收伤害以持续观察转轴；靶标不承担拦截命中率或伤害结算测试。
        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed) { absorbed = true; }

        //提供七种可重复工况，由玩家切换后交给炮塔自动重新索敌。
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            if (!Prefs.DevMode) yield break;
            for (int index = 0; index < Modes.Length; index++)
            {
                int selected = index;
                yield return new Command_Action
                {
                    defaultLabel = Modes[index],
                    defaultDesc = "切换真实高度或航迹，保留炮塔原有索敌、俯仰限制和射击条件。",
                    icon = ContentFinder<Texture2D>.Get("Things/Building/Security/30mmElectromagneticCIWS/ElectromagneticCIWS_MenuIcon"),
                    action = () => { mode = selected; startTick = Find.TickManager.TicksGame; UpdateFlight(); }
                };
            }
        }

        //显示当前工况与场地炮塔的准入、可达性和实际两轴角度。
        public override string GetInspectString()
        {
            var text = new StringBuilder(Modes[mode]);
            text.Append("\n真实高度：").Append(height.ToString("F1")).Append(" 格");
            foreach (var turret in Map.GetComponent<AirDefenseTestField>().Turrets)
            {
                bool admitted = turret.TryReadTarget(this, out var state);
                text.Append("\n").Append(turret.Label).Append("：")
                    .Append(!admitted ? "目标策略拒绝" : turret.Aim.CanReach(state) ? "机械可达" : "超出机械范围")
                    .Append("；水平 ").Append(turret.Aim.Yaw.ToString("F1"))
                    .Append("° / 俯仰 ").Append(turret.Aim.Pitch.ToString("F1")).Append("°");
            }
            return text.ToString();
        }

        //保存航迹原点和工况，加载后仍由当前模型和瞄准配置驱动测试。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref anchor, "testAnchor");
            Scribe_Values.Look(ref startTick, "testStartTick");
            Scribe_Values.Look(ref mode, "testMode");
        }
    }
}

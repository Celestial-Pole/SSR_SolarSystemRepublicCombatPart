using Verse;

namespace SSR.Combat.Offscreen
{
    //从建筑 XML 读取资源位置和发射参数，弹丸伤害使用原版 ProjectileProperties。
    public sealed class SiloSettings : DefModExtension
    {
        public string prefabPath;
        public string missilePath;
        public string layoutPath;
        public string flamePath;
        public string smokePath;
        //模型正面相对建筑北向的偏角，井体、发射位和采集范围共同使用。
        public float modelYawOffset = 90f;
        public float launchInterval = 0.5f;
        public float cooldownSeconds = 6f;
        //冷发射使用格、秒计量；弹射速度指导向行程结束时的速度。
        public float ejectionSpeed = 14f;
        public float ejectionClearance = 0.12f;
        public float ejectionGravity = 26f;
        public float ignitionDelay = 0.93f;
        //弹尾离井后施加短暂侧向扰动，随后保持侧向惯性。
        public float ejectionDisturbanceSeconds = 0.08f;
        public FloatRange coldLateralSpeedRange = new FloatRange(0.12f, 0.35f);
        //推力在点火建立时间内增长，达到指定净向上加速度，单位为格每平方秒。
        public float ignitionAcceleration = 72f;
        public float ignitionRampSeconds = 0.06f;
        //抬升高度为弹头距地面的高度，耗时由速度和加速度求出。
        public float ascentHeight = 20f;
        public float flameWidth = 1f;
        public float flameLength = 1.1f;
        //同时控制喷口和热尾流的发光强度，不改变烟团颜色。
        public float flameBrightness = 1.35f;
        public float ignitionFlareMultiplier = 2f;
        public float ignitionFlareSeconds = 0.22f;
        //历史喷口形成弯曲热尾流，后喷气速使用格每秒，长度和摆幅使用格。
        public float flameTrailSeconds = 0.18f;
        public float flameTrailLength = 4.6f;
        public float flameGasSpeed = 5f;
        public float flameTurbulence = 0.13f;
        //烟团按路径距离生成，尺寸为直径，寿命按游戏秒计量。
        public float smokeSpacing = 0.4f;
        public float smokeLifetime = 1.5f;
        public float smokeStartSize = 0.65f;
        public float smokeEndSize = 1.8f;
        public float smokeOpacity = 0.2f;
        //转弯和俯冲共享沿路径的净加速度，接续动力抬升出口速度。
        public float flightAcceleration = 7f;
        //转弯几何独立于速度；期望半径受水平航程百分之四十八的单向转弯上限约束。
        public float turnRadiusFraction = 0.46f;
        public float minimumTurnRadius = 18f;
    }
}

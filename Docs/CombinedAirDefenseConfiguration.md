# 弹炮合一防空系统

建筑定义：`SSR_Turret_CombinedAirDefense`。配置集中在 `1.6/Defs/ThingDefs/Bulidings/Buildings_CombinedAirDefense.xml`，不与普通近防炮共用可变参数。

模型：`Assets/SSR/Prefab/TurretT1_MGAA_2X.prefab`。底座尺寸保存在内部 `Root` 的三倍缩放中，建筑图形 `drawSize` 保持 `(1,1)`。建筑占地三乘三，禁止材质建造，建筑按钮在基础炮塔研究完成后显示。

## 机械挂点

| 职责 | 模型路径 |
|---|---|
| 偏航 | `Root/Yaw` |
| 俯仰 | `Root/Yaw/Pitch` |
| 八枪管转轮 | `Root/Yaw/Pitch/Rotor` |
| 近防炮开火点 | `Root/Yaw/Pitch/FirePoint` |
| 左导弹架 | `Root/Yaw/Pitch/RackLeft` |
| 右导弹架 | `Root/Yaw/Pitch/RackRight` |
| 左雷达 | `Root/Yaw/RadarLeft` |
| 右雷达 | `Root/Yaw/RadarRight` |

奇数弹位在左侧，偶数弹位在右侧。每个 `Slot01` 至 `Slot08` 内有 `FirePoint` 和完整 `Missile`。发射只隐藏该弹位的导弹，发射筒保留；冷却完成后恢复。炮口沿自身正 Z 朝向，预制体中已旋转为炮塔局部负 Z。

俯仰绕局部正 X 轴，范围为下俯 10 度至上仰 90 度；偏航绕局部正 Y 轴。两个圆形屋顶装置独立扫描，后部矩形天线随炮塔偏航，不参与俯仰。

## 武器参数

近防炮逐发时序保留原版动词和 SSR 穿透弹结算。射程在近防炮武器的 `verbs/li/range`，射速在 `ticksBetweenBurstShots`，枪管数量与启停时长在 `OffscreenPrefabProperties`。默认八根枪管，每刻一发，每发对应 45 度转轮步进，启动 18 刻、停止 30 刻。弹药由原版燃料组件维护，消耗特种钢；导弹独立补充，不消耗材料。

导弹参数位于 `CombinedAirDefenseSettings`：

| 字段 | 默认值 | 含义 |
|---|---:|---|
| `missileRange` | 65 | 最大水平射程，单位格 |
| `launchIntervalTicks` | 30 | 两枚导弹之间的发射间隔 |
| `reloadTicks` | 600 | 单个弹位的补充冷却 |
| `ejectionSpeed` | 8 | 冷弹射出口速度，格每秒 |
| `ejectionClearance` | 0.12 | 弹尾离筒后的额外净空，格 |
| `ejectionGravity` | 9.81 | 离筒滑行的重力，格每平方秒 |
| `ignitionDelay` | 0.30 | 达到离筒净空后的点火延迟上限，秒 |
| `minimumIgnitionHeight` | 0.2 | 限制滑行时间时使用的最低弹体端点高度，格 |
| `maximumSpeed` | 72 | 最大飞行速度，格每秒 |
| `acceleration` | 160 | 点火后的加速度，格每平方秒 |
| `turnDegreesPerSecond` | 240 | 最大制导转速，度每秒 |
| `fuseRadius` | 0.6 | 三维接近引信半径，格 |
| `missileLifetimeTicks` | 600 | 飞行寿命 |

各弹位供电时持续冷却，停火仍允许补充；断电或被眩晕时停止推进。雷达转速在 `radars/li/degreesPerSecond`，默认左侧 60、右侧 90 度每秒。

目标策略 `SSR_TurretTargeting_CombinedAirDefense` 允许地面、抛射弹丸、空投物和 SSR 空间目标，最大真实离地高度为 240 格。自动锁定优先拦截空中威胁，手动锁定覆盖自动选择；两种武器共用目标，分别检查自身射程、弹药和冷却。近防炮射程为 50 格，50 至 65 格之间由导弹射击。

## 导弹模型与尾迹

完整弹体资源：`Assets/SSR/CombinedDefense/Game/MGAAMissile.prefab` 与同目录的 `MGAAMissile.asset`。模型局部长度 0.82、含尾翼最大直径 0.068；弹头、喷口分别为 `TipPoint` 与 `TrailPoint`。

弹头初始位置就是弹位 FirePoint。冷弹射从静止沿发射筒轴加速，筒内位置与朝向持续跟随发射器当前的偏航、俯仰角。弹尾完全离筒并达到净空后固定出口姿态，以弹射速度惯性滑行并受重力影响；经过点火延迟才产生尾焰、施加发动机推力并追踪目标。筒内阶段不向世界正上方弹射，也不提前使用制导转向。

默认弹射约 0.65 秒，随后最多滑行 0.30 秒，再点火加速。低俯角时，以弹头和弹尾中较低的一端计算下降到点火高度的时刻，将无动力滑行限制在该时刻的八成以内，留出制导恢复余量。

喷焰、烟雾使用导弹井的 `MissileFlame.prefab`、`MissileSmoke.prefab`，尺寸和采样参数在 `exhaust` 内独立配置。尾迹记录真实喷口经过的位置，导弹转弯后旧烟迹保留原曲线并自行消散。导弹飞行与引信在三维空间计算，空中爆炸只影响三维半径内的空中实体。

编辑器建模工具在 `Assets/SSR/CombinedDefense/Editor`。导弹网格可以重新生成；炮塔分组工具只接受尚未分组的原始预制体，避免重复改变关节。PNG 使用现有“导出当前炮塔图标”工具，更新后应将图标取景报告的 `drawSize` 和 `drawOffset` 同步到蓝图配置。

## 游戏内测试地图

现有“创建防空测试地图”入口会在右侧生成这座炮塔，接入场地电网与真实 SSR 索敌、俯仰、近防炮和导弹逻辑。没有额外的独立调试入口。

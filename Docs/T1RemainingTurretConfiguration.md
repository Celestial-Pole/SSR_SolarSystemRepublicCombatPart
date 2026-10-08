# T1 五种炮塔接入与配置

五种已涂色模型均已接入 RimWorld 1.6，T1 的 13 种模型现在都有对应游戏建筑。所有新增定义位于 `1.6/Defs/ThingDefs/Bulidings/Buildings_<型号>.xml`，每份文件包含建筑、武器和弹丸。

## 型号与默认参数

| 型号 | 建筑 Def | 占地 | 射程 | 连发 | 默认目标 |
|---|---|---|---|---|---|
| 电磁哨戒机枪 | `SSR_Turret_EMSentry` | 1×1 | 45 | 12 发，间隔 5 刻 | 地面 |
| 便携电磁哨戒机枪 | `SSR_Turret_PortableEMSentry` | 1×1 | 40 | 8 发，间隔 6 刻 | 地面 |
| 电磁机关炮 | `SSR_Turret_EMAutocannon` | 2×2 | 55 | 8 发，间隔 6 刻 | 地面 |
| 远程多管火箭炮 | `SSR_Turret_RocketArtillery` | 2×2 | 90，最小 10 | 10 发，间隔 12 刻 | 地面 |
| 导弹发射箱 | `SSR_Turret_MissileBox` | 2×2 | 100，最小 10 | 4 发，间隔 30 刻 | 地面、空中 |

数值是当前接入默认值，均可在对应 XML 调整；尚未进行游戏内平衡测试。沿用基础炮塔研究与 SSR 防御建筑分类。三种电磁炮使用特种钢补充弹药；便携型号支持拆卸搬运，不依赖电网，其余型号配置了电力组件。

## 旋转、俯仰和后坐

共用 `TurretAimSettings`，没有为每种型号写独立瞄准逻辑。

- `rootTransform`、`yawPath`、`pitchPath`、`shootingOrigine`：真实模型挂点。
- `yawRotationAixe`、`pitchRotationAixe`、`shootingOrigineForward`：关节局部轴和炮口方向。
- `pitchRotationRange`：默认 `(350,85)`，表示关节局部转角 -10° 至 85°；实际抬升方向由 `pitchRotationAixe` 和模型轴向决定。`yawRotationRange` 默认零向量表示水平整圈。
- `radarPaths`：随炮口指向转动的雷达节点列表。节点局部 Z 轴朝向盘面正前方，放在偏航节点下，绕自身轴承俯仰，与主炮俯仰节点分离。
- `yawRotationSpeed`、`pitchRotationSpeed`、对应 `Acceleration`：角速度和角加速度。
- `yawAimTolerance`、`pitchAimTolerance`、`aimConeTolerance`：允许开火的瞄准误差。
- `targeting`、`allowManualTarget`、`returnToIdle`、`idleDelayTicks`：目标策略、手动集火与回正。`returnToIdle` 默认关闭，当前各型号均保持最后的偏航和俯仰角，不自动回正。

电磁炮结构为 `Root/Yaw/Pitch/Recoil/FirePoint`，底座固定、炮身偏航、炮管独立俯仰。后坐片段只移动 `Recoil`，通过 `OffscreenPrefabProperties.recoilTransformPath/recoilClipName` 绑定，不覆盖偏航或俯仰。发射器使用 `Root/Yaw/Pitch/FirePoint` 作为整架瞄准参考；每枚导弹使用自身弹位挂点出筒。

电磁机关炮的炮管两侧装饰块 `Slice.004` 和枪座后方装饰条 `Slice.005` 固定在 `Root/Yaw`，不参与俯仰和后坐；后方圆形雷达通过 `Root/Yaw/RadarPitch` 绕自身转轴跟随炮口指向。

游戏图形 `drawSize` 保持 `(1,1)`，模型比例保存在 `Root` 内，避免只放大水平而压扁高度。菜单使用 `_MenuIcon_Isometric.png`，蓝图使用独立正面 PNG 及 Unity 导出的取景尺寸、偏移。

全部 13 种炮塔的放置预览现直接从已构建资源包导出，同时写回 `building/blueprintGraphicData` 的尺寸和底座偏移。原有六种炮塔曾沿用模型缩放值作为 PNG 的绘制尺寸，导致蓝图过小且底座错位；这两种尺寸现在分别维护。九联装导弹井使用实际闭舱模型、90° 正面修正和地下裁剪，分别采集四个方向，蓝图通过 `Graphic_Multi` 选择对应图片，不再旋转单张俯视图。

## 筒内库存与实体导弹

两种发射器使用 `Building_TubeMissileTurret`、`Verb_TubeMissile` 和 `TubeLauncherSettings`，复用弹炮合一现有的 `Projectile_GuidedDefenseMissile`、三维制导、实体渲染与导弹井烟焰资源。

导弹发射箱直接提取原模型的四枚 `球体` 弹体，保留完整网格、子网格、材质和尺寸。火箭炮当前导入资源只有空筒，没有完整火箭；从同批导弹箱的真实弹体提取网格，按筒径与长度适配后装入十个弹位。原火箭资源右侧五筒存在重叠副本，游戏预制体只保留十个实际发射口。

| 发射器 | 运行预制体 | 库存与飞行共用弹体 | 单弹补充 |
|---|---|---|---|
| 多管火箭炮 | `Assets/SSR/Turrets/T1/Game/RocketArtillery.prefab` | `Assets/SSR/Turrets/T1/Game/RocketPayload.prefab` | 900 刻 |
| 导弹发射箱 | `Assets/SSR/Turrets/T1/Game/MissileBox.prefab` | `Assets/SSR/Turrets/T1/Game/MissileBoxPayload.prefab` | 1200 刻 |

每个 `missileSlots` 条目指向 `Root/Yaw/Pitch/Slots/SlotXX`，包含 `Missile` 与 `FirePoint`。`FirePoint` 与库存弹头 `TipPoint` 的位置和姿态一致，飞行实体使用该弹体的实际尺寸；`TrailPoint` 决定弹尾位置。发射成功后只隐藏相应库存弹体，生成具有独立位置、伤害和存档状态的真实弹丸。筒内运动随当前发射架姿态，弹尾完全离筒后离开发射架，滑行后点火制导。

发射器等待整组补满后开始下一轮，避免最先装好的一枚立即单独发射。火箭炮和导弹箱均先完成偏航与俯仰瞄准，再开盖，完全打开后开始首发；一轮开始后，保持目标准入检查和制导，不因逐发的炮口角度误差中断。满弹时火箭炮发射 10 枚、导弹箱发射 4 枚，各筒依次发射一枚。停火或目标失效会结束剩余发射，断电或眩晕沿用原版暂停连发的行为。

主要配置：

- `missilePrefabPath`、`missileProjectile`、`missileSlots`：完整弹体资源、弹丸 Def 与逐枚库存。
- `doors`：每扇盖板的 `path`、`axis`、`openAngle`；`openingTicks` 默认 24 刻。完成瞄准后开盖，完全打开才发射，齐射期间及弹尾未离筒时保持开盖。
- `reloadTicks`：各弹位独立自动补充；停电或眩晕暂停机械与补充，停火阻止继续发射。
- `ejectionSpeed`、`ejectionClearance`、`ejectionGravity`、`ignitionDelay`、`minimumIgnitionHeight`：冷弹射和点火时机。
- `maximumSpeed`、`acceleration`、`turnDegreesPerSecond`、`fuseRadius`、`missileLifetimeTicks`：三维飞行与引信。
- `exhaust`：与现有导弹共用的尾焰、喷口历史和烟迹参数。
- 武器 `verbs/li` 的 `range`、`minRange`、`burstShotCount`、`ticksBetweenBurstShots`：射程和逐发时序；弹丸的 `projectile` 节点设置伤害与爆炸范围。

## 构建与预览

Unity 工程：`src/SSR.Combat.UnityAsset`。编辑器菜单 `SSR/炮塔资源` 中的“构建剩余五种炮塔”整理机械关节与弹体，“预览剩余五种炮塔”按实际 XML 输出抬升 35°、开盖和离筒姿态。模型修改并重新打包后，“同步蓝图与实际模型”从构建产物生成蓝图并更新 XML；“导出当前炮塔图标”同时更新斜视按钮图标。采集尺寸记录保存在 `.local/build-requests/turret-icons.xml`。

机构预览按需生成到 `Docs/Previews/T1Integration`，生成的 PNG 不纳入版本控制。

运行 DLL 已通过 Release 编译，零警告、零错误。Windows 资源包已重新构建，登记 166 项运行资源；五种建筑的武器/弹丸引用、唯一 Def 名、菜单/蓝图图片及预制体资源包登记已核对。Unity 编辑器按实际挂点检查了五种炮塔的俯仰方向，以及两种发射器的弹头位置、库存网格和飞行网格一致性。预览是编辑器静态机械姿态，未启动 RimWorld，未执行游戏内战斗测试。

2026-10-07 已将核心包和战斗包部署至 `E:\steam\steamapps\common\RimWorld\Mods` 对应目录，并在 `ModsConfig.xml` 中启用 `brrainz.harmony` → `ssr.core` → `ssr.combat`，保留原有 27 个模组及其顺序。部署后的 DLL、Windows 资源包、新增定义和斜视图标与项目文件哈希一致。启用列表备份：`E:\ModdevMics\Backups\SSR_Combat\20261007-105126\ModsConfig.xml`。

蓝图与齐射修正已同步到 Steam 模组和发布输出目录：运行 DLL、8 份建筑定义、16 张蓝图贴图共 25 个文件均通过 SHA-256 核对。13 种建筑的蓝图尺寸及偏移与资源包采集记录一致；两个筒式发射器的连发数分别等于 10、4 个实际弹位。运行 DLL Release 编译零警告、零错误。本次未启动游戏或执行战斗测试。

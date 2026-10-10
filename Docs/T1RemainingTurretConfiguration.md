# T1 五种炮塔接入与配置

T1 的 13 种模型均有对应游戏建筑。建筑、武器和弹丸定义集中在 `1.6/Defs/ThingDefs/Bulidings/Buildings_Security_Turrets.xml`。

## 型号与默认参数

| 型号 | 建筑 Def | 占地 | 射程 | 连发 | 默认目标 |
|---|---|---|---|---|---|
| 电磁哨戒机枪 | `SSR_Turret_Electromagnetic_SentryGun` | 1×1 | 45 | 12 发，间隔 5 刻 | 地面 |
| 便携电磁哨戒机枪 | `SSR_Turret_Electromagnetic_PortableSentryGun` | 1×1 | 40 | 8 发，间隔 6 刻 | 地面 |
| 电磁机关炮 | `SSR_Turret_Electromagnetic_Autocannon` | 2×2 | 55 | 8 发，间隔 6 刻 | 地面 |
| 远程多管火箭炮 | `SSR_Turret_RocketArtillery` | 2×2 | 90，最小 10 | 10 发，间隔 12 刻 | 地面 |
| 导弹发射箱 | `SSR_Turret_Box-typeMissileLauncher` | 2×2 | 100，最小 10 | 4 发，间隔 30 刻 | 地面、空中 |

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

普通、电磁及两种便携哨戒机枪的最大仰角均为 60°。普通哨戒使用局部负角上仰，范围为 `(300,85)`；电磁哨戒使用局部正角上仰，范围为 `(350,60)`。145mm 与 300mm 电磁炮使用 `(315,5)`，即上仰 45°、下俯 5°；500mm 仍为上仰 85°、下俯 3°。自动瞄准和角度预览滑条共用这些范围。

电磁机关炮的炮管两侧装饰块 `Slice.004` 和枪座后方装饰条 `Slice.005` 固定在 `Root/Yaw`，不参与俯仰和后坐；后方圆形雷达通过 `Root/Yaw/RadarPitch` 绕自身转轴跟随炮口指向。

游戏图形 `drawSize` 保持 `(1,1)`，模型比例保存在 `Root` 内，避免只放大水平而压扁高度。菜单使用 `_MenuIcon_Isometric.png`，蓝图使用独立正面 PNG 及 Unity 导出的取景尺寸、偏移。

炮塔与导弹井的重叠区域按建筑 `size` 的占地面积排序：大炮塔覆盖小炮塔，同占地面积时南侧覆盖北侧。整座建筑的炮身与描边共用遮挡层次，旋转和俯仰不会改变排序；独立飞行的导弹继续使用弹丸层。

地图黑边统一为 `0.05` 格，参照核心包数控加工中心的原图（1536×1024、绘制尺寸 3×2 格，直边黑线约 25 像素）。采集时按 `0.05 × 采集分辨率 ÷ 地图取景宽度` 换算，不随炮塔尺寸增粗；镜头缩放时与同系列贴图建筑等比例变化。导弹井、出筒合并采集和独立飞行弹体使用同一宽度，轮廓重叠判断也沿用该宽度。

全部 13 种炮塔的放置预览从已构建资源包导出，同时写回 `building/blueprintGraphicData` 的尺寸和底座偏移。模型缩放与蓝图绘制尺寸分别维护。九联装导弹井使用实际闭舱模型、90° 正面修正和地下裁剪，分别采集四个方向，蓝图通过 `Graphic_Multi` 选择对应图片。

## 筒内库存与实体导弹

两种发射器使用 `Building_TubeMissileTurret`、`Verb_TubeMissile` 和 `TubeLauncherSettings`，复用弹炮合一现有的 `Projectile_GuidedDefenseMissile`、三维制导、实体渲染与导弹井烟焰资源。

导弹发射箱直接提取原模型的四枚 `球体` 弹体，保留完整网格、子网格、材质和尺寸。火箭炮当前导入资源只有空筒，没有完整火箭；从同批导弹箱的真实弹体提取网格，按筒径与长度适配后装入十个弹位。原火箭资源右侧五筒存在重叠副本，游戏预制体只保留十个实际发射口。

| 发射器 | 运行预制体 | 库存与飞行共用弹体 | 单弹补充 |
|---|---|---|---|
| 多管火箭炮 | `Assets/SSR/Turrets/T1/Game/RocketArtillery.prefab` | `Assets/SSR/Turrets/T1/Game/RocketPayload.prefab` | 900 刻 |
| 导弹发射箱 | `Assets/SSR/Turrets/T1/Game/MissileBox.prefab` | `Assets/SSR/Turrets/T1/Game/MissileBoxPayload.prefab` | 1200 刻 |

每个 `missileSlots` 条目指向 `Root/Yaw/Pitch/Slots/SlotXX`，包含 `Missile` 与 `FirePoint`。`FirePoint` 与库存弹头 `TipPoint` 的位置和姿态一致，飞行实体使用该弹体的实际尺寸；`TrailPoint` 决定弹尾位置。发射成功后只隐藏相应库存弹体，生成具有独立位置、伤害和存档状态的真实弹丸。筒内运动随当前发射架姿态，弹尾完全离筒后离开发射架，滑行后点火制导。

火箭炮允许用余弹开始下一轮：目标被摧毁或失效后，经过正常射击冷却和重新瞄准即可攻击下一个目标，不必等待十管全部补满；每轮最多发射起射时的待发数量，上限 10 枚。打空后等待整组补满，避免最先装好的一枚立即单独发射。导弹箱仍要求四管满装后起射。两者均先完成偏航与俯仰瞄准，再开盖，完全打开后开始首发；一轮开始后，保持目标准入检查和制导，不因逐发的炮口角度误差中断。停火或目标失效会结束剩余发射，断电或眩晕沿用原版暂停连发的行为。

主要配置：

- `missilePrefabPath`、`missileProjectile`、`missileSlots`：完整弹体资源、弹丸 Def 与逐枚库存。
- `doors`：每扇盖板的 `path`、`axis`、`openAngle`；`openingTicks` 默认 24 刻。完成瞄准后开盖，完全打开才发射，齐射期间及弹尾未离筒时保持开盖。
- `reloadTicks`：各弹位独立自动补充；停电或眩晕暂停机械与补充，停火阻止继续发射。
- `allowPartialSalvo`：默认 `false`，要求满装起射；火箭炮设为 `true`，允许使用余弹开始下一轮，打空后仍等待整组补满。
- `ejectionSpeed`、`ejectionClearance`、`ejectionGravity`、`ignitionDelay`、`minimumIgnitionHeight`：冷弹射和点火时机。
- `maximumSpeed`、`acceleration`、`turnDegreesPerSecond`、`fuseRadius`、`missileLifetimeTicks`：三维飞行与引信。
- `exhaust`：与现有导弹共用的尾焰、喷口历史和烟迹参数。
- 武器 `verbs/li` 的 `range`、`minRange`、`burstShotCount`、`ticksBetweenBurstShots`：射程和逐发时序；弹丸的 `projectile` 节点设置伤害与爆炸范围。

## 构建与图标

Unity 工程：`src/SSR.Combat.UnityAsset`。编辑器菜单 `SSR/炮塔资源` 中的“构建剩余五种炮塔”根据源模型生成机械关节与弹体。日常调整直接维护现有游戏预制体，重新生成会覆盖其中的手工调整。

Windows 资源包入口为 `SSR.UnityComponent.Outline.Editor.CombatWindowsBundleBuilder.Build`。重新打包后，“同步蓝图与实际模型”从资源包生成蓝图并更新 XML；“导出当前炮塔图标”同时更新斜视按钮图标。

# 炮塔的 RimWorld 1.7 适配

目标为本机 RimWorld 1.7.4976 rev1043 / Waylith / .NET 10。修改范围只有 SSR 战斗包；SSR.Core 和其他 Mod 没有迁移、部署或修改。

## 构建与加载

在项目根目录执行 `pwsh -File Tools/Waylith/Build.ps1`。可通过 `GameRoot`、`CoreRoot`、`HarmonyPath` 参数指定引用位置。

- `1.7/Assemblies` 包含 `FS_SSR_Combat.dll`、`FS_TransWeapon.dll`、`SSR.Combat.Offscreen.dll`。
- 1.7 按顺序加载根目录、`1.6`、`1.7`。已核对游戏的 `ModContentPack.GetAllFilesForModPreserveOrder`：同相对路径采用最后一层文件，所以三个 1.7 DLL 覆盖三个旧 DLL，资源与 Defs 继续共用。
- 1.6 加载目录和原有三个 DLL 保留。共享源码的引擎分支由 `RW_1_7` 编译符号控制。
- `FS_SSR_Combat` 和 `FS_TransWeapon` 源码从本项目原有 1.6 DLL 恢复。当前 Defs 没有引用的旧 `FS_SSR.Defense` Animator 炮塔类不参加 1.7 构建；在用炮塔由 `SSR.Combat.Offscreen` 提供。
- 战斗包原来的 SSR.Core 依赖声明保留。此处引用核心库只为编译战斗包的既有公共类型，不代表核心库本身完成了 1.7 适配。

## 绘制和动画

1.7 模型入口读取 `1.7/Models/combat.glb`：42 个场景、719 份有效网格、10 个材质，旧线框网格不参加绘制。GLB 保留原节点层级、显隐、顶点、法线、UV、索引、颜色和烟焰参数。`SsrModelNode` 是纯 C# 数据，实例只复制节点，几何与材质共享；父子矩阵按变换版本缓存。旧 XML 中的 `.prefab` 路径只是 GLB 场景标识，不再读取预制体。

1.7 实体网格直接提交 `SsrLiveFeature`，在原版地图冻结绘制快照之前采集姿态，再录制进引擎当前帧的命令缓冲。模型没有 GameObject、Transform、Renderer、Animator、Unity 材质或独立 Camera；`SsrProjection` 只保存正交投影矩阵。烟焰也使用 Waylith 原生网格、材质和附件，不再使用 `CameraTextureRenderer` 或同步 GPU 提交。

共享战斗源码通过编译别名操作 `SsrModelNode` 和原生网格，不影响 1.6。与原版交互仍使用游戏提供的 Vector3、Quaternion、Bounds、Time 和地图 Camera 接口；这些接口来自 1.7 的 UnityStubs，保留它们不代表炮塔依赖 Unity 运行时。SSR.Core 作为整个战斗包的既有依赖保留，本次未改动其他 Mod。

实体颜色和几何使用两张共享屏幕浮点附件，阴影和滤波使用两张共享 R16 浮点附件。模型直接转换到原来的 60° 俯视坐标，再投影到地图；轮廓按 0.05 格计算八方向覆盖率。最终合成仍位于原版建筑队列和光照覆盖层之前，建筑之间保留占地、南北位置与编号的排序。独立弹体仍通过表面高度控制地图深度。

实体合成和烟焰面片提交到全局 `MeshDrawSubmission` 时，显式使用 `LayerPassRouter.Resolve(0)`，与原版普通地图绘制一致。`MeshDrawCommand` 的默认 `passId = 0` 是 `AnyPass`，不能当作地图主阶段：它会先进入 `WaterDepth` 的 R16 单通道附件，再进入地图彩色阶段；当前引擎的绘制项缓存只比较管线版本，会跨这两个渲染器复用水深管线，造成颜色输出异常。SSR 内部附件命令只交给自己的 `RecordBatch`，不进入全局地图队列。

原来的 32 点接触阴影、双向保边滤波、半球主辅光、转轮平均照明和地面裁剪公式继续使用。共享颜色附件的第四通道保存对象编号，法线保持二十四位编码，采样按对象隔离，避免相邻炮塔相互污染阴影。颜色覆盖率单独插值，不对对象编号做线性过滤。

实体不再创建、缓存或重建单座炮塔的颜色和高度贴图。所有实体共用两次网格步骤和三次阴影步骤，最后在地图上实时合成，SSR 实体流程没有独立提交或 `WaitForFence`。共享附件采用两倍屏幕尺寸，最终像素合并四个覆盖率样本；窗口变化时由引擎重建，镜头缩放不会重分配附件。像素采样方式已改变，60° 投影与 0.05 格线宽的数学定义保持不变。

主线程冻结姿态、颜色和材质属性，渲染线程只读取独立绘制快照，不读取建筑或动画节点。帧生命周期由引擎管理，跨步骤使用原生渲染通道的读写依赖。透明烟焰仍按原来的紧凑范围采集，并保持实体深度遮挡、双线性采样、1024 分辨率上限和缩小滞回；目标由实例与在途帧共同持有，帧完成后才释放。1.6 保留原来的独立离屏流程；烟焰、烟迹年龄和尾焰噪声沿用原有算法。

描边和接触阴影的取景 UV 步长使用 `_SSRColorTexelSize`、`_SSRGeometryTexelSize`，避免引擎按纹理名称自动替换成共享屏幕的纹素尺寸。两种坐标单位不能混用。

`1.7/AnimationData` 保存 6 段后坐和 9 段井盖的原始位置、旋转曲线、关键帧及左右切线。按原来的游戏刻和开关盖时间进行 Hermite 插值，不依赖 Animator 或 AnimationClip 的空接口。

日常开发直接维护 GLB、JSON 动画/布局和 GLSL，再运行 `Tools/Waylith/Build.ps1`；构建不读取 Unity 项目或 AssetBundle，也不要求安装 Unity。1.7 的舱位数据结构和读取器保存在自己的源码目录。

`Tools/Waylith/export_native_models.py` 是旧资源的一次性迁移入口，使用 UnityPy 读取现有包并转换为标准 GLB 2.0，不启动 Unity。`migration-source.sha256` 只记录迁移来源，不限制此后直接编辑 GLB。原始 Unity 项目和根目录 AssetBundle 留给 1.6；1.7 的旧专用包已归档，`NativeTurretModels.xml` 移除本项目的旧 AssetDef 和 Shader 探测字段。

GLB 使用标准右手坐标，加载时还原战斗逻辑原有的轴向和三角形绕序。编辑时保持场景名称及动画挂点路径；节点使用 TRS 和 `extras.active`，几何支持 float32 位置/法线/UV 与 uint32 三角形索引。材质除标准原色外，保留 `extras.ssrShader`、`floats` 和 `colors`。GLB 可在 Blender 查看和编辑，但发布时需要保留这些名称和附加参数，普通默认导出不能保证全部保留。

## 验证边界

构建检查 C# 编译、全部 GLSL 阶段的 SPIR-V 编译、Waylith 材质布局及顶点/片段接口。没有启动游戏或自动运行场景。画面一致性、实际开火、地图切换及其他依赖的游戏内行为仍需实机确认，不能仅凭这些静态检查宣称效果完全一致。

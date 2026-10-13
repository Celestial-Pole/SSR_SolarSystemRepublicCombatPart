#编译本项目的 1.7 程序集及 Shader，不修改依赖模组、不启动游戏。
param(
    [string]$GameRoot = 'E:\steam\steamapps\common\RimWorld',
    [string]$CoreRoot = 'E:\ModdevMics\References\SSR\SSR_SolarSystemRepublicCore',
    [string]$HarmonyPath = 'E:\steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll'
)
$ErrorActionPreference = 'Stop'
$ssrRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
#日常构建只依赖独立 GLB、布局和曲线，不读取 Unity 项目或 AssetBundle。
foreach ($ssrResource in @('1.7\Models\combat.glb', '1.7\Models\silolayout.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $ssrRoot $ssrResource) -PathType Leaf)) {
        throw "缺少原生模型资源：$ssrResource；从旧包迁移可运行 Tools/Waylith/export_native_models.py。"
    }
}
foreach ($ssrRequired in @((Join-Path $GameRoot 'Assembly-CSharp.dll'), (Join-Path $GameRoot 'UnityStubs.dll'), $HarmonyPath,
    (Join-Path $CoreRoot '1.6\Assemblies\FS_SSR.dll'))) {
    if (-not (Test-Path -LiteralPath $ssrRequired -PathType Leaf)) { throw "缺少编译引用：$ssrRequired" }
}
foreach ($ssrName in @('SSR.Combat.Offscreen', 'FS_TransWeapon')) {
    & dotnet build (Join-Path $ssrRoot "1.7\Source\$ssrName\$ssrName.csproj") --configuration Release --nologo `
        "-p:GameRoot=$GameRoot" "-p:CoreRoot=$CoreRoot" "-p:HarmonyPath=$HarmonyPath"
    if ($LASTEXITCODE -ne 0) { throw "SSR 1.7 编译失败：$ssrName" }
}
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Compile-Shaders.ps1') -GameRoot $GameRoot
if ($LASTEXITCODE -ne 0) { throw 'SSR Shader 编译或接口核对失败。' }
Write-Output 'SSR 战斗包 1.7 已编译；本步骤未启动游戏。'

using System.IO;
using HarmonyLib;
using Verse;
namespace SSR.Combat.Offscreen;

//注册原生资源和渲染阶段，不拦截游戏或其他模组的资源加载。
public sealed class SsrCombatMod : Mod
{
    internal static string Root { get; private set; }
    //GLB 按首次模型请求装载，绘制入口直接注册到原生帧渲染器。
    public SsrCombatMod(ModContentPack content) : base(content)
    {
        Root = content.RootDir;
        SsrShaders.Initialize(Path.Combine(Root, "1.7", "Shaders"));
        SsrLiveRenderer.Install(new Harmony("SSR.Combat.NativeRendering"));
    }
}

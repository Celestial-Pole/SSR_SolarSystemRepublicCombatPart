#调用游戏自带编译器检查 GLSL 和参数布局，不创建图形设备或启动游戏。
param([string]$GameRoot = 'E:\steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
if ([Environment]::Version.Major -lt 10) { throw '请使用 .NET 10 的 PowerShell。' }
if (-not ('SsrShaderCompiler' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
//让编译器依赖使用游戏目录中的一致版本。
public sealed class SsrShaderCompiler : AssemblyLoadContext {
    readonly string root;
    [DllImport("kernel32", CharSet = CharSet.Unicode)] private static extern bool SetDllDirectory(string path);
    public SsrShaderCompiler(string root) : base("SSR shader compiler") { this.root = root; }
    //解析独立编译上下文的游戏程序集。
    protected override Assembly Load(AssemblyName name) {
        string file = Path.Combine(root, name.Name + ".dll");
        return File.Exists(file) ? LoadFromAssemblyPath(file) : null;
    }
    //展开本地 include 后编译完整阶段，错误直接阻止构建。
    public int Compile(string file) {
        SetDllDirectory(root);
        try {
            var assembly = LoadFromAssemblyPath(Path.Combine(root, "Waylith.dll"));
            var loader = assembly.GetType("Waylith.Rendering.ShaderSourceLoader");
            loader.GetMethod("RegisterShaderSourceDirectory").Invoke(null, new object[] { Path.GetDirectoryName(file) });
            string source = (string)loader.GetMethod("ReadShaderSource").Invoke(null, new object[] { file });
            bool vertex = file.Contains(".vert.");
            var layout = loader.GetMethod(vertex ? "CreateVertexInputLayout" : "CreateMaterialLayout")
                .Invoke(null, new object[] { file });
            if ((bool)layout.GetType().GetProperty("HasErrors").GetValue(layout))
                throw new InvalidDataException("Shader 参数或顶点布局错误：" + file);
            if (!vertex) {
                string pass = Path.GetFileName(file);
                bool composite = pass == "SSR_Composite.frag.glsl" || pass == "SSR_Resolve.frag.glsl"
                    || pass == "SSR_Contact.frag.glsl" || pass == "SSR_ContactFilter.frag.glsl" || pass == "SSR_SurfaceHeight.frag.glsl";
                bool liveModel = pass == "SSR_LiveColor.frag.glsl" || pass == "SSR_LiveGeometry.frag.glsl";
                string vertexFile = Path.Combine(Path.GetDirectoryName(file), liveModel ? "SSR_LiveModel.vert.glsl"
                    : composite ? "SSR_Blit.vert.glsl" : "SSR_Model.vert.glsl");
                if ((bool)loader.GetMethod("HasStageInterfaceErrors").Invoke(null, new object[] { vertexFile, file }))
                    throw new InvalidDataException("Shader 阶段接口错误：" + file);
            }
            var compiler = assembly.GetType("Waylith.Rendering.RuntimeShaderCompiler");
            var language = assembly.GetType("Waylith.Rendering.RuntimeShaderLanguage");
            var stage = assembly.GetType("Waylith.Rendering.RuntimeShaderStage");
            var method = compiler.GetMethod("Compile", new[] {typeof(string), typeof(string), typeof(string), language, stage});
            return ((byte[])method.Invoke(null, new object[] { source, Path.GetFileName(file), "main",
                Enum.Parse(language, "Glsl"), Enum.Parse(stage, file.Contains(".vert.") ? "Vertex" : "Fragment") })).Length;
        } finally { SetDllDirectory(null); }
    }
}
'@
}
$ssrCompiler = [SsrShaderCompiler]::new($GameRoot)
$ssrShaders = Join-Path $PSScriptRoot '..\..\1.7\Shaders'
Get-ChildItem -LiteralPath $ssrShaders -File | Where-Object Name -Match '\.(vert|frag)\.glsl$' | ForEach-Object {
    $ssrLength = $ssrCompiler.Compile($_.FullName)
    Write-Output ($_.Name + ': ' + $ssrLength + ' bytes SPIR-V')
}

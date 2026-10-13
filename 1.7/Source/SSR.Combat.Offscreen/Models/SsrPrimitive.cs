using System.Numerics;
using Waylith.Graphics.Meshes;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//GLB 的单个三角形批次，材质原色不经过 Unity 材质转换。
internal sealed record SsrPrimitive(Mesh Mesh, Material Material, Vector4 Color);

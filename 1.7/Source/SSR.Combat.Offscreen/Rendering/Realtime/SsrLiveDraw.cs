using System.Numerics;
using NativeMesh = Waylith.Graphics.Meshes.Mesh;

namespace SSR.Combat.Offscreen;

//固定当前帧的原始 RGBA、网格变换及实例参数，不保留可变材质引用。
internal readonly record struct SsrLiveDraw(NativeMesh Mesh, Vector4 Color,
    Matrix4x4 Matrix, int Submesh, float ClipGround, float RotatingSurface);

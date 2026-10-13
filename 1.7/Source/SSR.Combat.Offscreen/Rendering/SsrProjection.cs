using UnityEngine;
using Waylith.Rendering;
namespace SSR.Combat.Offscreen;

//纯数学正交取景，不注册 Camera、场景对象或渲染事件。
internal sealed class SsrProjection
{
    internal readonly SsrModelNode transform = new();
    internal float orthographicSize, nearClipPlane, farClipPlane;
    internal Matrix4x4 worldToCameraMatrix => Matrix4x4.Scale(new Vector3(1, 1, -1))
        * Matrix4x4.Rotate(Quaternion.Inverse(transform.rotation)) * Matrix4x4.Translate(-transform.position);
    internal Matrix4x4 cameraToWorldMatrix => worldToCameraMatrix.inverse;
    internal Matrix4x4 projectionMatrix => Matrix4x4.Ortho(-orthographicSize, orthographicSize,
        -orthographicSize, orthographicSize, nearClipPlane, farClipPlane);

    //烟焰目标沿用纹理向上的 UV，Vulkan 附件只翻转投影 Y。
    internal CameraView NativeView()
    {
        var projection = projectionMatrix;
        projection.m11 = -projection.m11;
        projection.m13 = -projection.m13;
        var view = Matrix4x4.ToNumerics(worldToCameraMatrix);
        var native = Matrix4x4.ToNumerics(projection);
        return new CameraView(view, native, native * view, 0);
    }
}

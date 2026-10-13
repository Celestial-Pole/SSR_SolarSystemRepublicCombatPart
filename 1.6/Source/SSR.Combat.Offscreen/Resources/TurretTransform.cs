using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //使用模型节点名定位炮口和动画挂点。
    internal static class TurretTransform
    {
        //由真实网格及世界矩阵计算包围盒，绕过 1.7 的 Renderer.bounds 空接口。
        internal static Bounds WorldBounds(this Renderer renderer)
        {
#if RW_1_7
            var filter = renderer.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh)
                throw new System.NotSupportedException("SSR 模型缺少可计算包围盒的网格：" + renderer.gameObject.name);
            var local = filter.sharedMesh.bounds;
            var matrix = renderer.transform.localToWorldMatrix;
            var x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center), extents * 2);
#else
            return renderer.bounds;
#endif
        }

        //从世界矩阵提取节点缩放，Unity 版本继续使用原接口。
        internal static Vector3 WorldScale(this Transform node)
        {
#if RW_1_7
            return node.localToWorldMatrix.lossyScale;
#else
            return node.lossyScale;
#endif
        }

        //保留单位时间允许转过的角度，跨越目标方向时直接停在目标。
        internal static Quaternion RotateTowards(Quaternion from, Quaternion to, float degrees)
        {
#if RW_1_7
            float angle = Quaternion.Angle(from, to);
            return angle == 0 ? to : Quaternion.SlerpUnclamped(from, to, Mathf.Min(1, degrees / angle));
#else
            return Quaternion.RotateTowards(from, to, degrees);
#endif
        }

        //Unity 与 Waylith 都以 GameObject 名称保存预制体层级。
        internal static Transform FindModelNode(this Transform root, string path)
        {
            foreach (string part in path.Split('/'))
            {
                Transform next = null;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child.gameObject.name == part) { next = child; break; }
                }
                if (!next) return null;
                root = next;
            }
            return root;
        }
    }
}

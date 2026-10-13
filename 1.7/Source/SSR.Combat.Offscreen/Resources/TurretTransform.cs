using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //使用模型节点名定位炮口和动画挂点。
    internal static class TurretTransform
    {
        //由原生网格包围盒与节点矩阵计算地图范围。
        internal static Bounds WorldBounds(this SsrModelNode node, SsrPrimitive primitive)
        {
            var local = primitive.Mesh.Bounds;
            var matrix = node.localToWorldMatrix;
            var x = matrix.MultiplyVector(new Vector3(local.Extents.X, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, local.Extents.Y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, local.Extents.Z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            var center = new Vector3(local.Center.X, local.Center.Y, local.Center.Z);
            return new Bounds(matrix.MultiplyPoint3x4(center), extents * 2);
        }

        //仅以可见实体网格形成包围盒，库存显隐仍由节点控制。
        internal static Bounds ModelBounds(this SsrModelNode root)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (var node in root.Nodes())
                foreach (var primitive in node.Primitives)
                {
                    if (!node.activeInHierarchy) continue;
                    var part = node.WorldBounds(primitive);
                    if (found) bounds.Encapsulate(part); else { bounds = part; found = true; }
                }
            if (!found) throw new System.InvalidOperationException("SSR 模型没有活动几何：" + root.name);
            return bounds;
        }

        //从世界矩阵提取节点缩放，Unity 版本继续使用原接口。
        internal static Vector3 WorldScale(this Transform node)
        {
            return node.localToWorldMatrix.lossyScale;

        }

        //保留单位时间允许转过的角度，跨越目标方向时直接停在目标。
        internal static Quaternion RotateTowards(Quaternion from, Quaternion to, float degrees)
        {
            float angle = Quaternion.Angle(from, to);
            return angle == 0 ? to : Quaternion.SlerpUnclamped(from, to, Mathf.Min(1, degrees / angle));

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

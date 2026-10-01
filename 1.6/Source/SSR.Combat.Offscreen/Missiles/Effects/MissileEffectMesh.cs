using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //把烟团或热尾流合并为一个可复用网格，通过第二组纹理坐标传递年龄和形态种子。
    internal sealed class MissileEffectMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>(), data = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        private Mesh mesh;

        //开始重建本次透明采集的几何，复用托管缓冲。
        internal void Clear() { vertices.Clear(); uv.Clear(); data.Clear(); indices.Clear(); }

        //添加完整烟团面片，旋转与缩放由调用者的相机基向量确定。
        internal void AddQuad(Vector3 center, Vector3 right, Vector3 up, float age, float seed)
        {
            int first = vertices.Count;
            AddVertex(center - right - up, new Vector2(0, 0), age, seed);
            AddVertex(center + right - up, new Vector2(1, 0), age, seed);
            AddVertex(center - right + up, new Vector2(0, 1), age, seed);
            AddVertex(center + right + up, new Vector2(1, 1), age, seed);
            Connect(first);
        }

        //添加热尾流的左右边界，与上一对边界连接，避免独立长面片形成硬折线。
        internal void AddRibbonPair(Vector3 center, Vector3 side, float along, float age, float seed)
        {
            int first = vertices.Count;
            AddVertex(center - side, new Vector2(0, along), age, seed);
            AddVertex(center + side, new Vector2(1, along), age, seed);
            if (first >= 2) Connect(first - 2);
        }

        //同步添加顶点与着色数据，保持所有通道数量一致。
        private void AddVertex(Vector3 position, Vector2 coordinate, float age, float seed)
        {
            vertices.Add(position);
            uv.Add(coordinate);
            data.Add(new Vector2(age, seed));
        }

        //将相邻两对顶点拼成四边形，材质使用双面透明绘制。
        private void Connect(int first)
        {
            indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
            indices.Add(first + 1); indices.Add(first + 2); indices.Add(first + 3);
        }

        //将当前批次上传为一个网格，无有效三角形时不提交绘制。
        internal Mesh Upload()
        {
            if (indices.Count == 0) return null;
            if (!mesh) mesh = new Mesh { name = "SSR导弹烟焰批次", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, data);
            mesh.SetTriangles(indices, 0);
            return mesh;
        }

        //释放地图显示网格，烟团模拟数据由上层保留，重新可见时按需上传。
        internal void Release()
        {
            if (mesh) Object.Destroy(mesh);
            mesh = null;
            Clear();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen;

//炮塔自己的层级与变换数据，不创建引擎场景对象或注册组件。
public sealed class SsrModelNode
{
    public string name;
    public SsrModelNode parent { get; private set; }
    public SsrModelNode transform => this;
    public SsrModelNode gameObject => this;
    internal SsrPrimitive[] Primitives = System.Array.Empty<SsrPrimitive>();
    private readonly List<SsrModelNode> children = new();
    private Vector3 translation, scale = Vector3.one;
    private Quaternion orientation = Quaternion.identity;
    private int localRevision, cachedLocal = -1, cachedParent = -1, worldRevision;
    private Matrix4x4 world;
    public bool activeSelf { get; private set; } = true;
    public bool activeInHierarchy => activeSelf && (parent == null || parent.activeInHierarchy);
    public int childCount => children.Count;
    public Vector3 localPosition { get => translation; set { translation = value; localRevision++; } }
    public Quaternion localRotation { get => orientation; set { orientation = value; localRevision++; } }
    public Vector3 localScale { get => scale; set { scale = value; localRevision++; } }
    public Vector3 position { get => localToWorldMatrix.MultiplyPoint3x4(Vector3.zero); set => localPosition = parent == null ? value : parent.InverseTransformPoint(value); }
    public Quaternion rotation { get => parent == null ? orientation : parent.rotation * orientation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value; }
    public Vector3 forward => rotation * Vector3.forward;
    public Vector3 right => rotation * Vector3.right;
    public Vector3 up => rotation * Vector3.up;

    //每个节点只在自身或父节点变换后重算矩阵，同帧网格和挂点复用结果。
    public Matrix4x4 localToWorldMatrix
    {
        get
        {
            var parentMatrix = parent == null ? Matrix4x4.identity : parent.localToWorldMatrix;
            int revision = parent?.worldRevision ?? 0;
            if (cachedLocal != localRevision || cachedParent != revision)
            {
                world = parentMatrix * Matrix4x4.TRS(translation, orientation, scale);
                cachedLocal = localRevision;
                cachedParent = revision;
                worldRevision++;
            }
            return world;
        }
    }

    //装载和克隆时建立父子关系，地图运行中不重新组织模型层级。
    internal void AddChild(SsrModelNode child) { child.parent = this; children.Add(child); }
    //取得指定挂点。
    public SsrModelNode GetChild(int index) => children[index];
    //库存弹体只改变显隐，保持节点和动画绑定有效。
    public void SetActive(bool value) => activeSelf = value;
    //设置独立弹体或建筑根节点的世界姿态。
    public void SetPositionAndRotation(Vector3 point, Quaternion facing) { position = point; rotation = facing; }
    //保留节点的原始非等比缩放。
    public Vector3 TransformPoint(Vector3 point) => localToWorldMatrix.MultiplyPoint3x4(point);
    //将挂点变换回模型坐标。
    public Vector3 InverseTransformPoint(Vector3 point) => localToWorldMatrix.inverse.MultiplyPoint3x4(point);
    //向量变换包含缩放，适用于炮管实际方向。
    public Vector3 TransformVector(Vector3 vector) => localToWorldMatrix.MultiplyVector(vector);
    //将方向变换回父节点空间。
    public Vector3 InverseTransformVector(Vector3 vector) => localToWorldMatrix.inverse.MultiplyVector(vector);
    //旋转轴只接受旋转，不受节点缩放影响。
    public Vector3 TransformDirection(Vector3 vector) => rotation * vector;
    //检查活动轴的父子关系，包括节点自身。
    public bool IsChildOf(SsrModelNode ancestor)
    {
        for (var current = this; current != null; current = current.parent)
            if (current == ancestor) return true;
        return false;
    }
    //深拷贝活动状态与变换，网格和原生材质仍由所有实例共享。
    internal SsrModelNode Clone()
    {
        var copy = new SsrModelNode { name = name, translation = translation, orientation = orientation,
            scale = scale, Primitives = Primitives, activeSelf = activeSelf };
        foreach (var child in children) copy.AddChild(child.Clone());
        return copy;
    }
    //遍历模型数据，不查询引擎组件注册表。
    internal IEnumerable<SsrModelNode> Nodes()
    {
        yield return this;
        foreach (var child in children)
            foreach (var node in child.Nodes()) yield return node;
    }
    //共享的瞄准代码继续使用存在性判断，节点生命周期由普通引用管理。
    public static implicit operator bool(SsrModelNode node) => node is not null;
}

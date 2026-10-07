using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //从导弹箱提取库存与飞行共用的弹体。
    internal static class T1PayloadBuilder
    {
        //提取弹体，将其朝向设为局部正 Z。
        internal static GameObject Build(string name, Vector3 dimensions)
        {
            var source = T1ModelTools.Load("MissileBox");
            var result = new GameObject(name);
            try
            {
                var filter = source.GetComponentsInChildren<MeshFilter>().Single(m => m.name == "球体");
                var center = T1ModelTools.Bounds(source.transform, filter).center;
                var matrix = Matrix4x4.Scale(dimensions) * Matrix4x4.Rotate(Quaternion.Euler(0, 180, 0))
                    * Matrix4x4.Translate(-center) * source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                mesh.name = name;
                mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                var normals = matrix.inverse.transpose;
                mesh.normals = mesh.normals.Select(v => normals.MultiplyVector(v).normalized).ToArray();
                mesh.tangents = mesh.tangents.Select(t => {
                    var v = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                    return new Vector4(v.x, v.y, v.z, t.w);
                }).ToArray();
                mesh.RecalculateBounds();
                string directory = T1ModelTools.BasePath + "/Game";
                T1ModelTools.EnsureFolder(directory);
                string meshPath = directory + "/" + name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved)
                {
                    EditorUtility.CopySerialized(mesh, saved);
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                else { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
                result.AddComponent<MeshFilter>().sharedMesh = saved;
                result.AddComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<Renderer>().sharedMaterials;
                T1ModelTools.Joint(result.transform, result.transform, "TipPoint", new Vector3(0, 0, saved.bounds.max.z));
                T1ModelTools.Joint(result.transform, result.transform, "TrailPoint", new Vector3(0, 0, saved.bounds.min.z));
                return T1ModelTools.Save(result, directory + "/" + name + ".prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(result);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //构建电磁哨戒和电磁机关炮的瞄准、后坐关节。
    internal static class T1GunPrefabBuilder
    {
        //重建电磁哨戒、便携电磁哨戒和电磁机关炮预制体。
        [MenuItem("SSR/炮塔资源/整理剩余电磁炮塔")]
        public static void Build()
        {
            BuildOne("EMSentry", "TurretT1_ESTG_1X", 1, Vector3.zero);
            BuildOne("PortableEMSentry", "TurretT1_ESTGP_1X", 0.65f, new Vector3(0, 0, 0.27563f));
            BuildOne("EMAutocannon", "TurretT1_EMG_2X", 2, Vector3.zero);
            AssetDatabase.SaveAssets();
            Debug.Log("三种电磁炮塔的偏航、俯仰、后坐和真实炮口已保存。");
        }

        //按原模型轴承位置分组，分离炮管和固定支架。
        private static void BuildOne(string name, string output, float scale, Vector3 center)
        {
            var model = T1ModelTools.Load(name);
            try
            {
                bool autocannon = name == "EMAutocannon";
                var meshes = model.GetComponentsInChildren<MeshFilter>().ToArray();
                var old = model.transform.GetChild(0);
                var reference = model.transform;
                var root = T1ModelTools.Joint(reference, reference, "Root", Vector3.zero);
                var pivot = T1ModelTools.Bounds(reference, meshes.Single(m => m.name == (autocannon ? "柱体.002" : "柱体"))).center;
                var yaw = T1ModelTools.Joint(root, reference, "Yaw", pivot);
                var gunBounds = T1ModelTools.Bounds(reference, meshes.Single(m => m.name == (autocannon ? "立方体.001" : "立方体.011")));
                Vector3 pitchPoint = autocannon ? new Vector3(gunBounds.center.x, gunBounds.center.y, 0.255f)
                    : T1ModelTools.Bounds(reference, meshes.Single(m => m.name == "柱体.008")).center;
                var pitch = T1ModelTools.Joint(yaw, reference, "Pitch", pitchPoint);
                var recoil = T1ModelTools.Joint(pitch, reference, "Recoil", pitchPoint);
                var movable = autocannon
                    ? new HashSet<string> { "Slice.003", "立方体.001", "立方体.004" }
                    : new HashSet<string> { "Slice.003", "Slice.005", "柱体.001", "柱体.002", "柱体.003", "柱体.004", "立方体.004", "立方体.006", "立方体.011" };
                foreach (var mesh in meshes)
                {
                    Bounds bounds = T1ModelTools.Bounds(reference, mesh);
                    Transform parent = bounds.center.y <= pivot.y ? root : yaw;
                    if (movable.Contains(mesh.name)) parent = recoil;
                    mesh.transform.SetParent(parent, true);
                }
                T1ModelTools.Joint(recoil, reference, "FirePoint", new Vector3(gunBounds.center.x, gunBounds.center.y, gunBounds.max.z));
                if (autocannon) AddAutocannonRadar(yaw, reference);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                T1ModelTools.AddRecoil(recoil, name, autocannon ? 0.045f : 0.025f);
                root.localScale = Vector3.one * scale;
                root.localPosition = -center * scale;
                T1ModelTools.Save(model, "Assets/SSR/Prefab/" + output + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        //将机关炮雷达盘和背板挂到独立俯仰轴，支架留在炮塔上。
        internal static void AddAutocannonRadar(Transform yaw, Transform reference)
        {
            var disk = yaw.Find("柱体.009").GetComponent<MeshFilter>();
            var pivot = T1ModelTools.Joint(yaw, reference, "RadarPitch", T1ModelTools.Bounds(reference, disk).center);
            foreach (string part in new[] { "柱体.009", "立方体.007", "立方体.008" })
                yaw.Find(part).SetParent(pivot, true);
        }
    }
}

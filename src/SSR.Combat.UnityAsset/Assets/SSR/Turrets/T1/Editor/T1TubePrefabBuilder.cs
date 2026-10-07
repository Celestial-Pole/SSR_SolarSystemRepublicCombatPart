using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //构建多管火箭炮和导弹箱的关节、舱盖及弹位。
    internal static class T1TubePrefabBuilder
    {
        //构建两种发射器；火箭空筒装入按内径缩放的导弹箱弹体。
        [MenuItem("SSR/炮塔资源/整理剩余导弹发射器")]
        public static void Build()
        {
            var boxPayload = T1PayloadBuilder.Build("MissileBoxPayload", Vector3.one);
            var rocketPayload = T1PayloadBuilder.Build("RocketPayload", new Vector3(0.31f, 0.31f, 0.894f));
            BuildOne("MissileBox", boxPayload, false);
            BuildOne("RocketArtillery", rocketPayload, true);
            AssetDatabase.SaveAssets();
            Debug.Log("导弹箱四弹位和火箭炮十弹位已绑定同源实体弹体、俯仰与舱盖。");
        }

        //建立偏航和俯仰层级，挂接筒体、弹位和舱盖。
        private static void BuildOne(string name, GameObject payload, bool rocket)
        {
            var model = T1ModelTools.Load(name);
            try
            {
                var reference = model.transform;
                var meshes = model.GetComponentsInChildren<MeshFilter>().ToArray();
                var old = reference.GetChild(0);
                var root = T1ModelTools.Joint(reference, reference, "Root", Vector3.zero);
                var yaw = T1ModelTools.Joint(root, reference, "Yaw", new Vector3(0, 0.15867f, 0));
                var pitch = T1ModelTools.Joint(yaw, reference, "Pitch", new Vector3(0.00668f, 0.2375f, 0.36546f));
                var slots = T1ModelTools.Joint(pitch, reference, "Slots", Vector3.zero);
                var doors = T1ModelTools.Joint(pitch, reference, "Doors", Vector3.zero);
                int[] bodies = { 1, 3, 8, 16, 21, 26, 31, 36, 41, 46 };
                var payloadCenters = rocket
                    ? bodies.Select(i => T1ModelTools.Bounds(reference, meshes.Single(m => m.name == "柱体." + i.ToString("D3"))).center).ToArray()
                    : Enumerable.Range(0, 4).Select(i => T1ModelTools.Bounds(reference, meshes.Single(m => m.name == "球体" + (i == 0 ? "" : "." + i.ToString("D3")))).center).ToArray();
                foreach (var mesh in meshes)
                {
                    //移除源模型右侧重复叠放的五个筒壳。
                    if (rocket && mesh.name.StartsWith("柱体.") && int.TryParse(mesh.name.Substring(3), out int number)
                        && number >= 51 && number <= 75 || !rocket && mesh.name.StartsWith("球体"))
                    { UnityEngine.Object.DestroyImmediate(mesh.gameObject); continue; }
                    var bounds = T1ModelTools.Bounds(reference, mesh);
                    Transform parent = bounds.center.y <= 0.18f ? root : yaw;
                    if (bounds.center.y > 0.28f || mesh.name == "立方体.038" || mesh.name == "立方体.039") parent = pitch;
                    mesh.transform.SetParent(parent, true);
                }
                for (int i = 0; i < payloadCenters.Length; i++)
                {
                    string suffix = (i + 1).ToString("D2");
                    var slot = T1ModelTools.Joint(slots, reference, "Slot" + suffix, payloadCenters[i]);
                    var missile = (GameObject)PrefabUtility.InstantiatePrefab(payload);
                    missile.name = "Missile";
                    missile.transform.SetParent(slot, false);
                    missile.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    var tip = missile.transform.Find("TipPoint");
                    var fire = new GameObject("FirePoint").transform;
                    fire.SetParent(slot, false);
                    fire.SetPositionAndRotation(tip.position, missile.transform.rotation);
                    string doorName = rocket ? "柱体." + (bodies[i] + 1).ToString("D3") : "立方体." + (8 + 9 * i).ToString("D3");
                    var doorMesh = meshes.Single(m => m && m.name == doorName);
                    var doorBounds = T1ModelTools.Bounds(reference, doorMesh);
                    bool left = doorBounds.center.x < 0;
                    Vector3 hinge = new Vector3(left ? doorBounds.min.x : doorBounds.max.x, doorBounds.center.y, doorBounds.center.z + 0.00746f);
                    var door = T1ModelTools.Joint(doors, reference, "Door" + suffix, hinge);
                    doorMesh.transform.SetParent(door, true);
                }
                var aim = T1ModelTools.Joint(pitch, reference, "FirePoint", new Vector3(0,
                    payloadCenters.Average(v => v.y), payloadCenters[0].z - payload.transform.Find("TipPoint").localPosition.z));
                aim.localRotation = Quaternion.Euler(0, 180, 0);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                root.localScale = Vector3.one * 2;
                T1ModelTools.Save(model, T1ModelTools.BasePath + "/Game/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
    }
}

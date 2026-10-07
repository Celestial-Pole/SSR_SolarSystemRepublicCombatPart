using UnityEditor;

namespace SSR.Combat.Editor
{
    //构建电磁哨戒、电磁机关炮和筒式发射器资源。
    internal static class T1RemainingTurretBuilder
    {
        //依次构建炮塔和发射器预制体。
        [MenuItem("SSR/炮塔资源/构建剩余五种炮塔")]
        public static void Build()
        {
            T1GunPrefabBuilder.Build();
            T1TubePrefabBuilder.Build();
        }
    }
}

using System;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen;

//为离屏炮塔提供模型生命周期，不经过旧核心库的 Animator 驱动器。
public class SsrPrefabDrawer : ThingComp
{
    //保留炮塔瞄准代码所用的模型入口，实例只由所属组件管理。
    public sealed class UnityGameObjectUpdater
    {
        public GameObject ownGameObject;
        public Transform ownTransform => ownGameObject.transform;
        public int expirationTick;
        public static implicit operator bool(UnityGameObjectUpdater value) => value is not null;
    }

    private UnityGameObjectUpdater current;

    //当前离屏配置均使用一个模型，缺失或误配时明确报告。
    public UnityGameObjectUpdater CurrentUnityObject
    {
        get
        {
            if (current != null && current.ownGameObject) return current;
            var settings = (SsrPrefabProperties)props;
            if (settings.leveledPrefabPaths == null || settings.leveledPrefabPaths.Count != 1)
                throw new InvalidOperationException("SSR 炮塔需要唯一的模型路径：" + parent.def.defName);
            string path = settings.leveledPrefabPaths[0];
            var prefab = SsrModelLibrary.Require(path);
            if (!prefab) throw new InvalidOperationException("SSR 炮塔模型不存在：" + path);
            var instance = prefab.Clone();
            current = new UnityGameObjectUpdater { ownGameObject = instance };
            try { ((OffscreenTurretComp)this).Observe(current); }
            catch
            {
                current = null;
                throw;
            }
            return current;
        }
    }

    //模型销毁后释放兼容入口，避免组件继续持有地图实例。
    protected void ReleasePrefabHandle()
    {
        current = null;
    }
}

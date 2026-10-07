using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //供筒内导弹读取发射器姿态。
    public interface IGuidedMissileLauncher
    {
        //返回指定弹位的发射挂点。
        Transform GetMissileFirePoint(int slot);
    }
}

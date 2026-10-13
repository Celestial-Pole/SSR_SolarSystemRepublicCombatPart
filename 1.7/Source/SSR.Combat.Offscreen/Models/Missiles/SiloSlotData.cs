using System;
using UnityEngine;

namespace SSR.UnityComponent.Missiles
{
    //保存单个舱位在游戏包装根节点下的弹体姿态及头尾挂点。
    [Serializable]
    public sealed class SiloSlotData
    {
        public string path;
        public string openingPath;
        public float openingSeconds;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector3 tip;
        public Vector3 trail;
        public float tailZ;
    }
}

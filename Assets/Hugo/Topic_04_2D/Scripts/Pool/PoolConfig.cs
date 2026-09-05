using System;
using UnityEngine;

namespace NetworkPrototype.Topic04.Pooling
{
    [Serializable]
    public sealed class PoolConfig
    {
        public string poolKey;
        public GameObject prefab;
        [Min(0)] public int initialSize;
        public bool canExpand = true;
    }
}

using UnityEngine;

namespace NetworkPrototype.Topic04.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PoolObject : MonoBehaviour
    {
        public string PoolKey { get; private set; }
        public bool IsInPool { get; private set; }

        internal void Configure(string poolKey)
        {
            PoolKey = poolKey;
            IsInPool = true;
        }

        internal bool TryMarkSpawned()
        {
            if (!IsInPool)
            {
                return false;
            }

            IsInPool = false;
            return true;
        }

        internal bool TryMarkReturned()
        {
            if (IsInPool)
            {
                return false;
            }

            IsInPool = true;
            return true;
        }
    }
}

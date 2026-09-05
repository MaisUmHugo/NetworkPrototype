using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NetworkPrototype.Topic04.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [SerializeField] private List<PoolConfig> poolConfigs = new();

        private readonly Dictionary<string, Queue<GameObject>> pools = new();
        private readonly Dictionary<string, PoolConfig> configLookup = new();
        private readonly List<GameObject> createdObjects = new();
        private NetworkManager networkManager;
        private bool poolsInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            BindNetworkManager();
        }

        private void OnDestroy()
        {
            UnbindNetworkManager();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public NetworkObject NetworkSpawnFromPool(
            string poolKey,
            Vector3 position,
            Quaternion rotation,
            bool destroyWithScene = true)
        {
            if (!CanControlNetworkPool())
            {
                Debug.LogWarning("[PoolManager] Apenas o servidor pode spawnar objetos da pool em rede.", this);
                return null;
            }

            EnsurePoolsInitialized();
            GameObject instance = GetObject(poolKey, position, rotation);
            if (instance == null)
            {
                return null;
            }

            if (!instance.TryGetComponent(out NetworkObject networkObject))
            {
                Debug.LogError($"[PoolManager] O prefab da pool '{poolKey}' nao possui NetworkObject.", instance);
                ReturnObjectInternal(instance);
                return null;
            }

            if (networkObject.IsSpawned)
            {
                Debug.LogError($"[PoolManager] O objeto da pool '{poolKey}' ja estava spawnado.", instance);
                ReturnObjectInternal(instance);
                return null;
            }

            networkObject.Spawn(destroyWithScene);
            return networkObject;
        }

        public void NetworkReturnToPool(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                return;
            }

            if (!CanControlNetworkPool())
            {
                Debug.LogWarning("[PoolManager] Apenas o servidor pode devolver objetos de rede para a pool.", this);
                return;
            }

            if (networkObject.IsSpawned)
            {
                networkObject.Despawn(false);
            }

            ReturnObjectInternal(networkObject.gameObject);
        }

        public GameObject GetObject(string poolKey, Vector3 position, Quaternion rotation)
        {
            EnsurePoolsInitialized();

            if (!pools.TryGetValue(poolKey, out Queue<GameObject> queue))
            {
                Debug.LogWarning($"[PoolManager] Pool '{poolKey}' nao encontrada.", this);
                return null;
            }

            GameObject instance = null;
            while (queue.Count > 0 && instance == null)
            {
                instance = queue.Dequeue();
            }

            if (instance == null)
            {
                PoolConfig config = configLookup[poolKey];
                if (!config.canExpand)
                {
                    Debug.LogWarning($"[PoolManager] Pool '{poolKey}' esgotada e sem expansao.", this);
                    return null;
                }

                instance = CreateNewObject(config);
            }

            PoolObject poolObject = instance.GetComponent<PoolObject>();
            if (!poolObject.TryMarkSpawned())
            {
                Debug.LogError($"[PoolManager] Estado invalido ao retirar objeto da pool '{poolKey}'.", instance);
                return null;
            }

            // NGO does not allow a despawned NetworkObject to change parent.
            // Networked pooled objects therefore remain at the scene root during
            // their entire lifetime; only purely local objects use this manager
            // as their inactive hierarchy container.
            if (instance.GetComponent<NetworkObject>() == null)
            {
                instance.transform.SetParent(null, false);
            }
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            instance.GetComponent<IPoolable>()?.OnSpawnFromPool();
            return instance;
        }

        public void ReturnObject(GameObject instance)
        {
            if (instance != null &&
                instance.TryGetComponent(out NetworkObject networkObject) &&
                networkObject.IsSpawned)
            {
                Debug.LogError("[PoolManager] Use NetworkReturnToPool para um NetworkObject spawnado.", instance);
                return;
            }

            ReturnObjectInternal(instance);
        }

        private void BindNetworkManager()
        {
            if (networkManager != null)
            {
                return;
            }

            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[PoolManager] NetworkManager nao encontrado na cena.", this);
                return;
            }

            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnServerStopped += HandleServerStopped;

            if (networkManager.IsListening && networkManager.IsServer)
            {
                EnsurePoolsInitialized();
            }
        }

        private void UnbindNetworkManager()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnServerStopped -= HandleServerStopped;
            networkManager = null;
        }

        private void HandleServerStarted()
        {
            EnsurePoolsInitialized();
        }

        private void HandleServerStopped(bool wasClient)
        {
            ClearPools();
        }

        private bool CanControlNetworkPool()
        {
            return networkManager != null && networkManager.IsListening && networkManager.IsServer;
        }

        private void EnsurePoolsInitialized()
        {
            if (poolsInitialized)
            {
                return;
            }

            pools.Clear();
            configLookup.Clear();

            foreach (PoolConfig config in poolConfigs)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.poolKey) || config.prefab == null)
                {
                    continue;
                }

                if (configLookup.ContainsKey(config.poolKey))
                {
                    Debug.LogError($"[PoolManager] Chave duplicada: '{config.poolKey}'.", this);
                    continue;
                }

                var queue = new Queue<GameObject>();
                pools.Add(config.poolKey, queue);
                configLookup.Add(config.poolKey, config);

                for (int i = 0; i < config.initialSize; i++)
                {
                    queue.Enqueue(CreateNewObject(config));
                }
            }

            poolsInitialized = true;
        }

        private GameObject CreateNewObject(PoolConfig config)
        {
            GameObject instance = Instantiate(config.prefab);
            if (instance.GetComponent<NetworkObject>() == null)
            {
                instance.transform.SetParent(transform, false);
            }

            instance.name = $"{config.prefab.name}_Pooled";

            PoolObject poolObject = instance.GetComponent<PoolObject>();
            if (poolObject == null)
            {
                poolObject = instance.AddComponent<PoolObject>();
            }

            poolObject.Configure(config.poolKey);
            instance.SetActive(false);
            createdObjects.Add(instance);
            return instance;
        }

        private void ReturnObjectInternal(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (!instance.TryGetComponent(out PoolObject poolObject))
            {
                Debug.LogWarning("[PoolManager] Objeto retornado sem PoolObject.", instance);
                instance.SetActive(false);
                return;
            }

            if (!pools.TryGetValue(poolObject.PoolKey, out Queue<GameObject> queue))
            {
                Debug.LogWarning($"[PoolManager] Pool '{poolObject.PoolKey}' nao existe.", instance);
                instance.SetActive(false);
                return;
            }

            if (!poolObject.TryMarkReturned())
            {
                Debug.LogWarning($"[PoolManager] Retorno duplicado ignorado para '{poolObject.PoolKey}'.", instance);
                return;
            }

            instance.GetComponent<IPoolable>()?.OnReturnToPool();
            instance.SetActive(false);
            if (instance.GetComponent<NetworkObject>() == null)
            {
                instance.transform.SetParent(transform, false);
            }
            queue.Enqueue(instance);
        }

        private void ClearPools()
        {
            foreach (GameObject instance in createdObjects)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            createdObjects.Clear();
            pools.Clear();
            configLookup.Clear();
            poolsInitialized = false;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using NetworkPrototype.Topic04.Player;
using NetworkPrototype.Topic04.Pooling;
using Unity.Netcode;
using UnityEngine;

namespace NetworkPrototype.Topic04.Combat
{
    [DisallowMultipleComponent]
    public sealed class Topic04MatchManager : MonoBehaviour
    {
        public static Topic04MatchManager Instance { get; private set; }

        [SerializeField] private PoolManager poolManager;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private string projectilePoolKey = "Projectile2D";
        [SerializeField, Min(0.05f)] private float fireInterval = 0.4f;
        [SerializeField, Min(0f)] private float projectileSpawnOffset = 0.9f;
        [SerializeField, Min(0f)] private float respawnDelay = 2f;

        private readonly Dictionary<ulong, NetworkPlayer2DController> players = new();
        private readonly Dictionary<ulong, double> nextAllowedFireTime = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            poolManager ??= PoolManager.Instance;
        }

        private void Start()
        {
            poolManager ??= PoolManager.Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RegisterPlayer(NetworkPlayer2DController player)
        {
            if (!IsServerActive() || player == null)
            {
                return;
            }

            players[player.OwnerClientId] = player;
            nextAllowedFireTime.Remove(player.OwnerClientId);
            StartCoroutine(RespawnAfterDelay(player, 0f));
        }

        public void UnregisterPlayer(NetworkPlayer2DController player)
        {
            if (player == null)
            {
                return;
            }

            players.Remove(player.OwnerClientId);
            nextAllowedFireTime.Remove(player.OwnerClientId);
        }

        public void TryFire(NetworkPlayer2DController shooter, sbyte direction)
        {
            if (!IsServerActive() || shooter == null || !shooter.IsSpawned || !shooter.IsAlive)
            {
                return;
            }

            ulong shooterId = shooter.OwnerClientId;
            double now = Time.unscaledTimeAsDouble;
            if (nextAllowedFireTime.TryGetValue(shooterId, out double allowedAt) && now < allowedAt)
            {
                return;
            }

            poolManager ??= PoolManager.Instance;
            if (poolManager == null)
            {
                Debug.LogError("[Topic04MatchManager] PoolManager nao encontrado.", this);
                return;
            }

            float directionX = direction < 0 ? -1f : 1f;
            Vector3 spawnPosition = shooter.transform.position + Vector3.right * (directionX * projectileSpawnOffset);
            NetworkObject projectileObject = poolManager.NetworkSpawnFromPool(
                projectilePoolKey,
                spawnPosition,
                Quaternion.identity);

            if (projectileObject == null || !projectileObject.TryGetComponent(out NetworkProjectile2D projectile))
            {
                if (projectileObject != null)
                {
                    poolManager.NetworkReturnToPool(projectileObject);
                }

                return;
            }

            projectile.InitializeServer(shooterId, directionX, this);
            nextAllowedFireTime[shooterId] = now + fireInterval;
        }

        public void HandleProjectileHit(NetworkProjectile2D projectile, NetworkPlayer2DController victim)
        {
            if (!IsServerActive() || projectile == null || victim == null || !victim.IsAlive)
            {
                return;
            }

            if (victim.OwnerClientId == projectile.ShooterClientId)
            {
                return;
            }

            bool eliminated = victim.ServerApplyHit(projectile.Damage);
            if (eliminated &&
                players.TryGetValue(projectile.ShooterClientId, out NetworkPlayer2DController shooter) &&
                shooter != null &&
                shooter != victim)
            {
                shooter.ServerAwardPoint();
                StartCoroutine(RespawnAfterDelay(victim, respawnDelay));
            }
            else if (eliminated)
            {
                StartCoroutine(RespawnAfterDelay(victim, respawnDelay));
            }
        }

        public void ReturnProjectile(NetworkProjectile2D projectile)
        {
            if (!IsServerActive() || projectile == null || poolManager == null)
            {
                return;
            }

            poolManager.NetworkReturnToPool(projectile.NetworkObject);
        }

        private IEnumerator RespawnAfterDelay(NetworkPlayer2DController player, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return null;
            }

            if (!IsServerActive() || player == null || !player.IsSpawned)
            {
                yield break;
            }

            player.ServerRespawn(GetSpawnPosition(player.OwnerClientId));
        }

        private Vector2 GetSpawnPosition(ulong clientId)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return Vector2.zero;
            }

            int index = (int)(clientId % (ulong)spawnPoints.Length);
            Transform spawnPoint = spawnPoints[index];
            return spawnPoint != null ? (Vector2)spawnPoint.position : Vector2.zero;
        }

        private static bool IsServerActive()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening && manager.IsServer;
        }
    }
}

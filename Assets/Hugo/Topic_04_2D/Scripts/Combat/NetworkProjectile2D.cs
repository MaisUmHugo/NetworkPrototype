using NetworkPrototype.Topic04.Player;
using NetworkPrototype.Topic04.Pooling;
using Unity.Netcode;
using UnityEngine;

namespace NetworkPrototype.Topic04.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class NetworkProjectile2D : NetworkBehaviour, IPoolable
    {
        [SerializeField, Min(0f)] private float speed = 12f;
        [SerializeField, Min(0.05f)] private float lifetime = 2f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D projectileCollider;

        private Topic04MatchManager matchManager;
        private Vector2 direction;
        private double expiresAt;
        private bool activeOnServer;

        public ulong ShooterClientId { get; private set; }
        public int Damage => damage;

        private void Awake()
        {
            body ??= GetComponent<Rigidbody2D>();
            projectileCollider ??= GetComponent<Collider2D>();
        }

        public void InitializeServer(
            ulong shooterClientId,
            float horizontalDirection,
            Topic04MatchManager manager)
        {
            if (!IsServer)
            {
                return;
            }

            ShooterClientId = shooterClientId;
            direction = horizontalDirection < 0f ? Vector2.left : Vector2.right;
            matchManager = manager;
            expiresAt = Time.unscaledTimeAsDouble + lifetime;
            activeOnServer = true;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !activeOnServer)
            {
                return;
            }

            if (Time.unscaledTimeAsDouble >= expiresAt)
            {
                ReturnToPool();
                return;
            }

            Vector2 nextPosition = body.position + direction * (speed * Time.fixedDeltaTime);
            body.MovePosition(nextPosition);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsSpawned || !IsServer || !activeOnServer || other == null)
            {
                return;
            }

            NetworkPlayer2DController target = other.GetComponent<NetworkPlayer2DController>();
            if (target != null)
            {
                if (target.OwnerClientId == ShooterClientId || !target.IsAlive)
                {
                    return;
                }

                matchManager?.HandleProjectileHit(this, target);
                ReturnToPool();
                return;
            }

            if (other.GetComponent<Topic04ArenaSurface>() != null)
            {
                ReturnToPool();
            }
        }

        public void OnSpawnFromPool()
        {
            ShooterClientId = ulong.MaxValue;
            direction = Vector2.zero;
            expiresAt = double.PositiveInfinity;
            activeOnServer = false;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            if (projectileCollider != null)
            {
                projectileCollider.enabled = true;
            }
        }

        public void OnReturnToPool()
        {
            activeOnServer = false;
            direction = Vector2.zero;
            matchManager = null;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            if (projectileCollider != null)
            {
                projectileCollider.enabled = false;
            }
        }

        private void ReturnToPool()
        {
            if (!activeOnServer)
            {
                return;
            }

            activeOnServer = false;
            (matchManager != null ? matchManager : Topic04MatchManager.Instance)?.ReturnProjectile(this);
        }
    }
}

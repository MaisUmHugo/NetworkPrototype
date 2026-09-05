using System.Collections.Generic;
using NetworkPrototype.Topic04.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkPrototype.Topic04.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class NetworkPlayer2DController : NetworkBehaviour
    {
        private static readonly HashSet<NetworkPlayer2DController> SpawnedPlayers = new();

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float jumpImpulse = 9f;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.08f;

        [Header("Combat")]
        [SerializeField, Min(1)] private int maximumHealth = 1;

        [Header("References")]
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private BoxCollider2D bodyCollider;
        [SerializeField] private SpriteRenderer playerRenderer;

        private readonly NetworkVariable<int> health = new(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> score = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> alive = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction fireAction;
        private float horizontalInput;
        private sbyte facingDirection = 1;
        private bool jumpRequested;
        private bool localInputBlocked;

        public static IReadOnlyCollection<NetworkPlayer2DController> ActivePlayers => SpawnedPlayers;
        public int Health => health.Value;
        public int Score => score.Value;
        public bool IsAlive => alive.Value;

        private void Awake()
        {
            playerInput ??= GetComponent<PlayerInput>();
            body ??= GetComponent<Rigidbody2D>();
            bodyCollider ??= GetComponent<BoxCollider2D>();
            playerRenderer ??= GetComponent<SpriteRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            SpawnedPlayers.Add(this);
            alive.OnValueChanged += HandleAliveChanged;
            gameObject.name = $"Player2D_{OwnerClientId}";
            ApplyOwnerColor();
            ApplyAliveState(alive.Value);
            SetInputEnabled(IsOwner && alive.Value);

            if (IsServer)
            {
                Topic04MatchManager.Instance?.RegisterPlayer(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                Topic04MatchManager.Instance?.UnregisterPlayer(this);
            }

            alive.OnValueChanged -= HandleAliveChanged;
            SpawnedPlayers.Remove(this);
            SetInputEnabled(false);
            horizontalInput = 0f;
            jumpRequested = false;
            localInputBlocked = false;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !alive.Value || localInputBlocked)
            {
                return;
            }

            horizontalInput = moveAction != null
                ? Mathf.Clamp(moveAction.ReadValue<Vector2>().x, -1f, 1f)
                : 0f;

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                facingDirection = horizontalInput > 0f ? (sbyte)1 : (sbyte)-1;
            }

            if (jumpAction != null && jumpAction.WasPressedThisFrame())
            {
                jumpRequested = true;
            }

            if (fireAction != null && fireAction.WasPressedThisFrame())
            {
                RequestFireServerRpc(facingDirection);
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || !alive.Value || localInputBlocked || body == null)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.x = horizontalInput * moveSpeed;

            if (jumpRequested && IsGrounded())
            {
                velocity.y = jumpImpulse;
            }

            jumpRequested = false;
            body.linearVelocity = velocity;
        }

        public void SetLocalGameplayInputBlocked(bool blocked)
        {
            if (!IsOwner)
            {
                return;
            }

            localInputBlocked = blocked;
            horizontalInput = 0f;
            jumpRequested = false;

            if (body != null && blocked)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }

            SetInputEnabled(IsSpawned && alive.Value && !blocked);
        }

        internal bool ServerApplyHit(int damage)
        {
            if (!IsServer || !alive.Value || damage <= 0)
            {
                return false;
            }

            health.Value = Mathf.Max(0, health.Value - damage);
            if (health.Value > 0)
            {
                return false;
            }

            alive.Value = false;
            return true;
        }

        internal void ServerAwardPoint()
        {
            if (IsServer)
            {
                score.Value++;
            }
        }

        internal void ServerRespawn(Vector2 position)
        {
            if (!IsServer)
            {
                return;
            }

            ApplyRespawnPosition(position);
            health.Value = maximumHealth;
            alive.Value = true;
            ApplyRespawnPositionClientRpc(position);
        }

        [ServerRpc]
        private void RequestFireServerRpc(sbyte requestedDirection, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !alive.Value)
            {
                return;
            }

            sbyte validatedDirection = requestedDirection < 0 ? (sbyte)-1 : (sbyte)1;
            Topic04MatchManager.Instance?.TryFire(this, validatedDirection);
        }

        [ClientRpc]
        private void ApplyRespawnPositionClientRpc(Vector2 position)
        {
            ApplyRespawnPosition(position);
        }

        private void ApplyRespawnPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);

            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void HandleAliveChanged(bool previousValue, bool currentValue)
        {
            ApplyAliveState(currentValue);
            SetInputEnabled(IsOwner && currentValue && !localInputBlocked);
        }

        private void ApplyAliveState(bool isAlive)
        {
            if (playerRenderer != null)
            {
                playerRenderer.enabled = isAlive;
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = isAlive;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = isAlive;
                body.bodyType = IsOwner ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            }
        }

        private bool IsGrounded()
        {
            if (bodyCollider == null)
            {
                return false;
            }

            ContactFilter2D filter = ContactFilter2D.noFilter;
            int hitCount = bodyCollider.Cast(Vector2.down, filter, groundHits, groundProbeDistance);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = groundHits[i].collider;
                if (hitCollider != null && hitCollider.GetComponent<Topic04ArenaSurface>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetInputEnabled(bool shouldEnable)
        {
            if (playerInput == null)
            {
                moveAction = null;
                jumpAction = null;
                fireAction = null;
                return;
            }

            if (!shouldEnable)
            {
                if (playerInput.enabled)
                {
                    playerInput.DeactivateInput();
                    playerInput.enabled = false;
                }

                moveAction = null;
                jumpAction = null;
                fireAction = null;
                return;
            }

            if (playerInput.actions == null)
            {
                Debug.LogError("[NetworkPlayer2DController] PlayerInput sem Input Actions.", this);
                return;
            }

            playerInput.enabled = true;
            playerInput.ActivateInput();
            moveAction = playerInput.actions.FindAction("Player/Move", true);
            jumpAction = playerInput.actions.FindAction("Player/Jump", true);
            fireAction = playerInput.actions.FindAction("Player/Fire", true);
        }

        private void ApplyOwnerColor()
        {
            if (playerRenderer == null)
            {
                return;
            }

            float hue = (OwnerClientId * 0.23f) % 1f;
            playerRenderer.color = Color.HSVToRGB(hue, 0.72f, 0.95f);
        }
    }
}

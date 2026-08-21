using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkPrototype.Player
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float spawnSpacing = 2.5f;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private CapsuleCollider capsuleCollider;
        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(0f)] private float groundCheckHeight = 0.5f;
        [SerializeField, Min(0f)] private float groundCheckDistance = 2f;
        [SerializeField, Range(0.5f, 1f)] private float edgeProbeRadiusMultiplier = 0.9f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Vector2[] GroundProbeDirections =
        {
            Vector2.right,
            Vector2.left,
            Vector2.up,
            Vector2.down,
            new Vector2(1f, 1f).normalized,
            new Vector2(1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        private InputAction moveAction;

        private void Awake()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            if (capsuleCollider == null)
            {
                capsuleCollider = GetComponent<CapsuleCollider>();
            }
        }

        public override void OnNetworkSpawn()
        {
            ulong localClientId = NetworkManager != null
                ? NetworkManager.LocalClientId
                : ulong.MaxValue;

            gameObject.name = $"Player_{OwnerClientId}";
            ApplyOwnerColor();

            if (IsOwner)
            {
                PositionOwnerAtValidSpawn();
            }

            SetInputEnabled(IsOwner);

            Debug.Log(
                $"[NetworkPlayerController] Player spawned\n" +
                $"OwnerClientId: {OwnerClientId}\n" +
                $"LocalClientId: {localClientId}\n" +
                $"IsOwner: {IsOwner}\n" +
                $"InputEnabled: {playerInput != null && playerInput.enabled}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            SetInputEnabled(false);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Vector2 input = moveAction != null
                ? moveAction.ReadValue<Vector2>()
                : Vector2.zero;

            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 movement = new Vector3(input.x, 0f, input.y);
            if (movement.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 targetPosition = transform.position + movement * (moveSpeed * Time.deltaTime);
            if (IsPositionSupportedByGround(targetPosition))
            {
                transform.position = targetPosition;
            }
        }

        private void PositionOwnerAtValidSpawn()
        {
            float spawnX = (OwnerClientId % 4) * spawnSpacing;
            Vector3 targetPosition = new Vector3(spawnX, transform.position.y, 0f);
            if (IsPositionSupportedByGround(targetPosition))
            {
                transform.position = targetPosition;
                return;
            }

            Debug.LogWarning(
                $"[NetworkPlayerController] Spawn calculado fora do Ground; mantendo {transform.position}.",
                this);
        }

        private bool IsPositionSupportedByGround(Vector3 targetPosition)
        {
            if (groundMask.value == 0)
            {
                return false;
            }

            if (!HasGroundBelow(targetPosition))
            {
                return false;
            }

            float worldRadius = capsuleCollider != null
                ? capsuleCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z))
                : 0.5f;
            float probeRadius = worldRadius * edgeProbeRadiusMultiplier;

            foreach (Vector2 direction in GroundProbeDirections)
            {
                Vector3 offset = new Vector3(direction.x, 0f, direction.y) * probeRadius;
                if (!HasGroundBelow(targetPosition + offset))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasGroundBelow(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * groundCheckHeight;
            float rayDistance = groundCheckHeight + groundCheckDistance;
            return Physics.Raycast(
                origin,
                Vector3.down,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private void SetInputEnabled(bool shouldEnable)
        {
            if (playerInput == null)
            {
                if (shouldEnable)
                {
                    Debug.LogError("[NetworkPlayerController] PlayerInput nao foi encontrado.", this);
                }

                moveAction = null;
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
                return;
            }

            if (playerInput.actions == null)
            {
                Debug.LogError("[NetworkPlayerController] PlayerInput nao possui Input Actions.", this);
                playerInput.enabled = false;
                moveAction = null;
                return;
            }

            playerInput.enabled = true;
            playerInput.ActivateInput();
            moveAction = playerInput.actions.FindAction("Player/Move", true);
        }

        private void ApplyOwnerColor()
        {
            Renderer playerRenderer = GetComponentInChildren<Renderer>();
            if (playerRenderer == null)
            {
                return;
            }

            float hue = (OwnerClientId * 0.23f) % 1f;
            Color color = Color.HSVToRGB(hue, 0.72f, 0.95f);

            var properties = new MaterialPropertyBlock();
            playerRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, color);
            properties.SetColor(ColorId, color);
            playerRenderer.SetPropertyBlock(properties);
        }
    }
}

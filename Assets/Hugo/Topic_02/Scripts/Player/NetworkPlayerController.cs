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

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public override void OnNetworkSpawn()
        {
            ulong localClientId = NetworkManager != null
                ? NetworkManager.LocalClientId
                : ulong.MaxValue;

            gameObject.name = $"Player_{OwnerClientId}";
            ApplyOwnerColor();

            if (IsOwner)
            {
                float spawnX = (OwnerClientId % 8) * spawnSpacing;
                transform.position = new Vector3(spawnX, 1f, 0f);
            }

            Debug.Log(
                $"[NetworkPlayerController] Player spawned\n" +
                $"OwnerClientId: {OwnerClientId}\n" +
                $"LocalClientId: {localClientId}\n" +
                $"IsOwner: {IsOwner}",
                this);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 movement = new Vector3(input.x, 0f, input.y);
            transform.position += movement * (moveSpeed * Time.deltaTime);
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

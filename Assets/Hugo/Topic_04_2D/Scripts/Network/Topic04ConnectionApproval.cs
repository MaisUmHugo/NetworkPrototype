using Unity.Netcode;
using UnityEngine;

namespace NetworkPrototype.Topic04.Networking
{
    [DisallowMultipleComponent]
    public sealed class Topic04ConnectionApproval : MonoBehaviour
    {
        [SerializeField, Range(1, 4)] private int maximumPlayers = 4;

        private NetworkManager networkManager;

        private void Start()
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[Topic04ConnectionApproval] NetworkManager nao encontrado.", this);
                return;
            }

            networkManager.ConnectionApprovalCallback = ApproveConnection;
        }

        private void OnDestroy()
        {
            if (networkManager != null && networkManager.ConnectionApprovalCallback == ApproveConnection)
            {
                networkManager.ConnectionApprovalCallback = null;
            }
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool hasAvailableSlot = networkManager.ConnectedClientsIds.Count < maximumPlayers;
            response.Approved = hasAvailableSlot;
            response.CreatePlayerObject = hasAvailableSlot;
            response.PlayerPrefabHash = null;
            response.Position = null;
            response.Rotation = null;
            response.Pending = false;
            response.Reason = hasAvailableSlot ? string.Empty : "A arena ja possui quatro jogadores.";
        }
    }
}

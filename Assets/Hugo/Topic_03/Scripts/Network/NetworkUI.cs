using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace NetworkPrototype.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkUI : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;
        [SerializeField] private TMP_Text statusText;

        private void Awake()
        {
            hostButton.onClick.AddListener(StartHost);
            clientButton.onClick.AddListener(StartClient);
            serverButton.onClick.AddListener(StartServer);

            SetStatus("Offline - escolha Host, Client ou Server");
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        private void OnDestroy()
        {
            if (hostButton != null)
            {
                hostButton.onClick.RemoveListener(StartHost);
            }

            if (clientButton != null)
            {
                clientButton.onClick.RemoveListener(StartClient);
            }

            if (serverButton != null)
            {
                serverButton.onClick.RemoveListener(StartServer);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        public void StartHost()
        {
            StartSession("Host", manager => manager.StartHost());
        }

        public void StartClient()
        {
            StartSession("Client", manager => manager.StartClient());
        }

        public void StartServer()
        {
            StartSession("Server", manager => manager.StartServer());
        }

        private void StartSession(string role, Func<NetworkManager, bool> start)
        {
            NetworkManager manager = NetworkManager.Singleton;

            if (manager == null)
            {
                Debug.LogError("[NetworkUI] NetworkManager.Singleton nao foi encontrado.");
                SetStatus("Erro: NetworkManager nao encontrado");
                return;
            }

            if (manager.IsListening)
            {
                SetStatus("A sessao de rede ja esta em execucao");
                return;
            }

            bool started = start(manager);

            if (!started)
            {
                Debug.LogError($"[NetworkUI] Nao foi possivel iniciar como {role}.");
                SetStatus($"Falha ao iniciar {role}");
                return;
            }

            SetButtonsInteractable(false);

            if (role == "Client")
            {
                SetStatus("Executando como Client - aguardando o Host...");
            }
            else
            {
                SetStatus($"Executando como {role}");
            }

            Debug.Log($"[NetworkUI] Sessao iniciada como {role}.");
        }

        private void OnClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;

            if (manager == null)
            {
                return;
            }

            if (manager.IsClient &&
                !manager.IsHost &&
                clientId == manager.LocalClientId)
            {
                SetStatus("Executando como Client");
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            hostButton.interactable = interactable;
            clientButton.interactable = interactable;
            serverButton.interactable = interactable;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
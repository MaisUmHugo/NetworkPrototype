using NetworkPrototype.Topic04.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NetworkPrototype.Topic04.Networking
{
    [DisallowMultipleComponent]
    public sealed class Topic04NetworkUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject connectionMenu;
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private GameObject gameplayHud;

        [Header("Connection")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;
        [SerializeField] private Text statusText;

        [Header("Pause")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button quitButton;

        private NetworkManager networkManager;
        private bool sessionActive;

        private void Start()
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[Topic04NetworkUI] NetworkManager nao encontrado.", this);
                enabled = false;
                return;
            }

            hostButton.onClick.AddListener(StartHost);
            clientButton.onClick.AddListener(StartClient);
            serverButton.onClick.AddListener(StartServer);
            resumeButton.onClick.AddListener(ClosePauseMenu);
            disconnectButton.onClick.AddListener(Disconnect);
            quitButton.onClick.AddListener(QuitGame);

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnTransportFailure += HandleTransportFailure;

            ShowConnectionMenu("Escolha Host, Client ou Server.", true);
        }

        private void OnDestroy()
        {
            if (hostButton != null) hostButton.onClick.RemoveListener(StartHost);
            if (clientButton != null) clientButton.onClick.RemoveListener(StartClient);
            if (serverButton != null) serverButton.onClick.RemoveListener(StartServer);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(ClosePauseMenu);
            if (disconnectButton != null) disconnectButton.onClick.RemoveListener(Disconnect);
            if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);

            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnTransportFailure -= HandleTransportFailure;
        }

        private void Update()
        {
            if (!sessionActive ||
                networkManager == null ||
                !networkManager.IsClient ||
                !networkManager.IsConnectedClient ||
                Keyboard.current == null ||
                !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (pauseMenu.activeSelf)
            {
                ClosePauseMenu();
            }
            else
            {
                OpenPauseMenu();
            }
        }

        private void StartHost()
        {
            BeginConnection("Iniciando Host...");
            if (!networkManager.StartHost())
            {
                ShowConnectionMenu("Nao foi possivel iniciar o Host.", true);
            }
        }

        private void StartClient()
        {
            BeginConnection("Conectando como Client...");
            if (!networkManager.StartClient())
            {
                ShowConnectionMenu("Nao foi possivel iniciar o Client.", true);
            }
        }

        private void StartServer()
        {
            BeginConnection("Iniciando Dedicated Server...");
            if (!networkManager.StartServer())
            {
                ShowConnectionMenu("Nao foi possivel iniciar o Server.", true);
            }
        }

        private void BeginConnection(string message)
        {
            hostButton.interactable = false;
            clientButton.interactable = false;
            serverButton.interactable = false;
            statusText.text = message;
        }

        private void HandleServerStarted()
        {
            if (networkManager.IsServer && !networkManager.IsClient)
            {
                sessionActive = true;
                connectionMenu.SetActive(false);
                pauseMenu.SetActive(false);
                gameplayHud.SetActive(false);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId != networkManager.LocalClientId)
            {
                return;
            }

            sessionActive = true;
            connectionMenu.SetActive(false);
            pauseMenu.SetActive(false);
            gameplayHud.SetActive(true);
            SetLocalPlayerInputBlocked(false);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId == networkManager.LocalClientId)
            {
                string message = string.IsNullOrWhiteSpace(networkManager.DisconnectReason)
                    ? "Desconectado da sessao."
                    : networkManager.DisconnectReason;
                ShowConnectionMenu(message, true);
            }
        }

        private void HandleTransportFailure()
        {
            ShowConnectionMenu("Falha no transporte de rede.", true);
        }

        private void OpenPauseMenu()
        {
            pauseMenu.SetActive(true);
            SetLocalPlayerInputBlocked(true);
        }

        private void ClosePauseMenu()
        {
            pauseMenu.SetActive(false);
            SetLocalPlayerInputBlocked(false);
        }

        private void Disconnect()
        {
            ClosePauseMenu();
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            ShowConnectionMenu("Sessao encerrada.", true);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetLocalPlayerInputBlocked(bool blocked)
        {
            if (networkManager == null || !networkManager.IsClient)
            {
                return;
            }

            NetworkObject playerObject = networkManager.LocalClient?.PlayerObject;
            if (playerObject != null &&
                playerObject.TryGetComponent(out NetworkPlayer2DController controller))
            {
                controller.SetLocalGameplayInputBlocked(blocked);
            }
        }

        private void ShowConnectionMenu(string message, bool enableButtons)
        {
            sessionActive = false;
            SetLocalPlayerInputBlocked(false);
            connectionMenu.SetActive(true);
            pauseMenu.SetActive(false);
            gameplayHud.SetActive(false);
            hostButton.interactable = enableButtons;
            clientButton.interactable = enableButtons;
            serverButton.interactable = enableButtons;
            statusText.text = message;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using NetworkPrototype.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NetworkPrototype.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkUI : MonoBehaviour
    {
        private enum SessionRole
        {
            None,
            Host,
            Client,
            Server
        }

        private enum ConfirmationAction
        {
            None,
            Disconnect,
            QuitGame
        }

        [Header("Menu inicial")]
        [SerializeField] private GameObject connectionMenu;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Pause local")]
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button quitButton;

        [Header("Confirmacao")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private TMP_Text confirmationMessageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private InputAction pauseAction;
        private SessionRole currentRole;
        private ConfirmationAction pendingConfirmationAction;
        private bool confirmationOpenedFromPause;
        private bool isSessionActive;
        private bool isVoluntaryDisconnect;
        private bool isReturningToMenu;
        private Coroutine returnToMenuRoutine;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            RegisterButtonListeners();

            pauseAction = new InputAction("TogglePause", InputActionType.Button, "<Keyboard>/escape");
            pauseAction.performed += OnPausePerformed;
            pauseAction.Enable();

            ShowConnectionMenu("Offline - escolha Host, Client ou Server", true);
        }

        private void Start()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                SetStatus("Erro: NetworkManager nao encontrado");
                SetButtonsInteractable(false);
                return;
            }

            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.OnServerStarted += OnServerStarted;
            manager.OnTransportFailure += OnTransportFailure;
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();

            if (pauseAction != null)
            {
                pauseAction.performed -= OnPausePerformed;
                pauseAction.Disable();
                pauseAction.Dispose();
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnClientConnected;
                manager.OnClientDisconnectCallback -= OnClientDisconnected;
                manager.OnServerStarted -= OnServerStarted;
                manager.OnTransportFailure -= OnTransportFailure;
            }
        }

        private void OnApplicationQuit()
        {
            isVoluntaryDisconnect = true;
        }

        public void StartHost()
        {
            StartSession(SessionRole.Host, manager => manager.StartHost());
        }

        public void StartClient()
        {
            StartSession(SessionRole.Client, manager => manager.StartClient());
        }

        public void StartServer()
        {
            StartSession(SessionRole.Server, manager => manager.StartServer());
        }

        public void TogglePauseMenu()
        {
            if (confirmationPanel.activeSelf)
            {
                CancelConfirmation();
                return;
            }

            if (pauseMenu.activeSelf)
            {
                ClosePauseMenu();
                return;
            }

            if (CanOpenPauseMenu())
            {
                SetPauseMenuOpen(true, true);
            }
        }

        public void ClosePauseMenu()
        {
            SetPauseMenuOpen(false, isSessionActive);
        }

        public void Disconnect()
        {
            OpenConfirmation(
                ConfirmationAction.Disconnect,
                "Deseja desconectar da partida?");
        }

        public void QuitGame()
        {
            OpenConfirmation(
                ConfirmationAction.QuitGame,
                "Deseja fechar o jogo?");
        }

        private void StartSession(SessionRole role, Func<NetworkManager, bool> start)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                SetStatus("Erro: NetworkManager nao encontrado");
                return;
            }

            if (manager.IsListening || manager.ShutdownInProgress || isReturningToMenu)
            {
                SetStatus("A sessao de rede ainda esta ocupada");
                return;
            }

            currentRole = role;
            isSessionActive = false;
            isVoluntaryDisconnect = false;
            SetButtonsInteractable(false);

            switch (role)
            {
                case SessionRole.Client:
                    SetStatus("Conectando... aguardando o Host.");
                    break;
                case SessionRole.Host:
                    SetStatus("Iniciando Host...");
                    break;
                case SessionRole.Server:
                    SetStatus("Iniciando Server...");
                    break;
            }

            if (start(manager))
            {
                return;
            }

            currentRole = SessionRole.None;
            SetButtonsInteractable(true);
            SetStatus($"Falha ao iniciar {role}");
        }

        private void OnClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || clientId != manager.LocalClientId)
            {
                return;
            }

            if (currentRole == SessionRole.None)
            {
                currentRole = manager.IsHost ? SessionRole.Host : SessionRole.Client;
            }

            EnterGameplay(currentRole == SessionRole.Host
                ? "Executando como Host"
                : "Executando como Client");
        }

        private void OnServerStarted()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null ||
                currentRole != SessionRole.Server ||
                !manager.IsServer ||
                manager.IsClient)
            {
                return;
            }

            EnterGameplay("Executando como Server");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (isReturningToMenu)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;

            if (currentRole == SessionRole.Server)
            {
                return;
            }

            if (currentRole == SessionRole.Host &&
                manager != null &&
                clientId != manager.LocalClientId)
            {
                return;
            }

            if (currentRole != SessionRole.Client && currentRole != SessionRole.Host)
            {
                return;
            }

            string message;
            if (isVoluntaryDisconnect)
            {
                message = "Desconectado.";
            }
            else if (currentRole == SessionRole.Client && isSessionActive)
            {
                message = "Host desconectado.";
            }
            else
            {
                message = "Nao foi possivel conectar ao Host.";
            }

            ReturnToMenuAfterShutdown(message, true);
        }

        private void OnTransportFailure()
        {
            if (currentRole == SessionRole.None || isReturningToMenu)
            {
                return;
            }

            string message = currentRole == SessionRole.Client &&
                             isSessionActive &&
                             !isVoluntaryDisconnect
                ? "Host desconectado."
                : "Falha na conexao de rede.";

            ReturnToMenuAfterShutdown(message, true);
        }

        private void EnterGameplay(string status)
        {
            isSessionActive = true;
            pendingConfirmationAction = ConfirmationAction.None;
            confirmationOpenedFromPause = false;
            confirmationPanel.SetActive(false);
            pauseMenu.SetActive(false);
            connectionMenu.SetActive(false);
            SetLocalPlayerInputBlocked(false);
            SetStatus(status);
        }

        private void ReturnToMenuAfterShutdown(string finalStatus, bool requestShutdown)
        {
            NetworkManager manager = NetworkManager.Singleton;

            isReturningToMenu = true;
            isSessionActive = false;
            pendingConfirmationAction = ConfirmationAction.None;
            confirmationOpenedFromPause = false;
            confirmationPanel.SetActive(false);
            pauseMenu.SetActive(false);
            connectionMenu.SetActive(true);
            SetLocalPlayerInputBlocked(false);
            SetButtonsInteractable(false);
            SetStatus(requestShutdown && isVoluntaryDisconnect ? "Desconectando..." : finalStatus);

            if (requestShutdown &&
                manager != null &&
                manager.IsListening &&
                !manager.ShutdownInProgress)
            {
                manager.Shutdown();
            }

            if (returnToMenuRoutine != null)
            {
                StopCoroutine(returnToMenuRoutine);
            }

            returnToMenuRoutine = StartCoroutine(WaitForShutdownAndEnableMenu(finalStatus));
        }

        private IEnumerator WaitForShutdownAndEnableMenu(string finalStatus)
        {
            NetworkManager manager = NetworkManager.Singleton;
            while (manager != null && (manager.IsListening || manager.ShutdownInProgress))
            {
                yield return null;
                manager = NetworkManager.Singleton;
            }

            currentRole = SessionRole.None;
            isVoluntaryDisconnect = false;
            isReturningToMenu = false;
            returnToMenuRoutine = null;
            SetButtonsInteractable(true);
            SetStatus(finalStatus);
        }

        private void OpenConfirmation(ConfirmationAction action, string message)
        {
            confirmationOpenedFromPause = pauseMenu.activeSelf;
            pendingConfirmationAction = action;
            confirmationMessageText.text = message;

            if (confirmationOpenedFromPause)
            {
                pauseMenu.SetActive(false);
            }
            else
            {
                connectionMenu.SetActive(false);
            }

            confirmationPanel.SetActive(true);
        }

        private void ConfirmPendingAction()
        {
            ConfirmationAction action = pendingConfirmationAction;
            pendingConfirmationAction = ConfirmationAction.None;
            confirmationPanel.SetActive(false);

            switch (action)
            {
                case ConfirmationAction.Disconnect:
                    ExecuteDisconnect();
                    break;
                case ConfirmationAction.QuitGame:
                    ExitApplication();
                    break;
                default:
                    CancelConfirmation();
                    break;
            }
        }

        private void CancelConfirmation()
        {
            confirmationPanel.SetActive(false);
            pendingConfirmationAction = ConfirmationAction.None;

            if (confirmationOpenedFromPause && isSessionActive)
            {
                pauseMenu.SetActive(true);
            }
            else
            {
                connectionMenu.SetActive(true);
            }

            confirmationOpenedFromPause = false;
        }

        private void ExecuteDisconnect()
        {
            confirmationOpenedFromPause = false;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || currentRole == SessionRole.None)
            {
                ShowConnectionMenu("Offline - escolha Host, Client ou Server", true);
                return;
            }

            isVoluntaryDisconnect = true;
            ReturnToMenuAfterShutdown("Desconectado.", true);
        }

        private void ExitApplication()
        {
            isVoluntaryDisconnect = true;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening && !manager.ShutdownInProgress)
            {
                manager.Shutdown();
            }

            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            TogglePauseMenu();
        }

        private bool CanOpenPauseMenu()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return isSessionActive &&
                   currentRole != SessionRole.None &&
                   manager != null &&
                   manager.IsListening &&
                   !connectionMenu.activeSelf;
        }

        private void SetPauseMenuOpen(bool shouldOpen, bool updateLocalPlayerInput)
        {
            pauseMenu.SetActive(shouldOpen);

            if (updateLocalPlayerInput)
            {
                SetLocalPlayerInputBlocked(shouldOpen);
            }
        }

        private static void SetLocalPlayerInputBlocked(bool blocked)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || manager.LocalClient == null)
            {
                return;
            }

            NetworkObject playerObject = manager.LocalClient.PlayerObject;
            if (playerObject != null &&
                playerObject.TryGetComponent(out NetworkPlayerController controller))
            {
                controller.SetLocalGameplayInputBlocked(blocked);
            }
        }

        private bool ValidateReferences()
        {
            var missing = new List<string>();
            AddMissingReference(missing, connectionMenu, nameof(connectionMenu));
            AddMissingReference(missing, hostButton, nameof(hostButton));
            AddMissingReference(missing, clientButton, nameof(clientButton));
            AddMissingReference(missing, serverButton, nameof(serverButton));
            AddMissingReference(missing, quitGameButton, nameof(quitGameButton));
            AddMissingReference(missing, statusText, nameof(statusText));
            AddMissingReference(missing, pauseMenu, nameof(pauseMenu));
            AddMissingReference(missing, resumeButton, nameof(resumeButton));
            AddMissingReference(missing, disconnectButton, nameof(disconnectButton));
            AddMissingReference(missing, quitButton, nameof(quitButton));
            AddMissingReference(missing, confirmationPanel, nameof(confirmationPanel));
            AddMissingReference(missing, confirmationMessageText, nameof(confirmationMessageText));
            AddMissingReference(missing, yesButton, nameof(yesButton));
            AddMissingReference(missing, noButton, nameof(noButton));

            if (missing.Count == 0)
            {
                return true;
            }

            Debug.LogError(
                "[NetworkUI] Referencias obrigatorias nao configuradas na cena: " +
                string.Join(", ", missing),
                this);
            return false;
        }

        private static void AddMissingReference(
            ICollection<string> missing,
            UnityEngine.Object reference,
            string fieldName)
        {
            if (reference == null)
            {
                missing.Add(fieldName);
            }
        }

        private void RegisterButtonListeners()
        {
            hostButton.onClick.AddListener(StartHost);
            clientButton.onClick.AddListener(StartClient);
            serverButton.onClick.AddListener(StartServer);
            quitGameButton.onClick.AddListener(QuitGame);
            resumeButton.onClick.AddListener(ClosePauseMenu);
            disconnectButton.onClick.AddListener(Disconnect);
            quitButton.onClick.AddListener(QuitGame);
            yesButton.onClick.AddListener(ConfirmPendingAction);
            noButton.onClick.AddListener(CancelConfirmation);
        }

        private void RemoveButtonListeners()
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

            if (quitGameButton != null)
            {
                quitGameButton.onClick.RemoveListener(QuitGame);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(ClosePauseMenu);
            }

            if (disconnectButton != null)
            {
                disconnectButton.onClick.RemoveListener(Disconnect);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }

            if (yesButton != null)
            {
                yesButton.onClick.RemoveListener(ConfirmPendingAction);
            }

            if (noButton != null)
            {
                noButton.onClick.RemoveListener(CancelConfirmation);
            }
        }

        private void ShowConnectionMenu(string message, bool enableButtons)
        {
            isSessionActive = false;
            pendingConfirmationAction = ConfirmationAction.None;
            confirmationOpenedFromPause = false;
            confirmationPanel.SetActive(false);
            pauseMenu.SetActive(false);
            connectionMenu.SetActive(true);
            SetLocalPlayerInputBlocked(false);
            SetButtonsInteractable(enableButtons);
            SetStatus(message);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            hostButton.interactable = interactable;
            clientButton.interactable = interactable;
            serverButton.interactable = interactable;
        }

        private void SetStatus(string message)
        {
            statusText.text = message;
        }
    }
}

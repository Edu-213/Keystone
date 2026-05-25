using Unity.Netcode;
using Steamworks;
using UnityEngine;
using Assets._Keystone.Runtime.Scripts.SceneManagement;
using Assets._Keystone.Runtime.Scripts.SceneManagement.Extensions;
using System.Threading.Tasks;
using Assets._Keystone.Runtime.Scripts.Networking.Interface;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    [RequireComponent(typeof(INetworkTransportProvider))]
    public class SteamNetcodeBridge : MonoBehaviour
    {
        public enum NetworkSessionState
        {
            Idle, StartingHost, StartingClient,
            Connecting, InSession, LeavingSession, ReturningToMenu
        }

        public static SteamNetcodeBridge Instance { get; private set; }

        [SerializeField] private NetworkManager _networkManager;

        private INetworkTransportProvider _transportProvider;
        private SceneManagementBootstrapper _bootstrapper;
        private SceneManagementBootstrapper Bootstrapper => _bootstrapper ?? throw new System.InvalidOperationException("SceneManagementBootstrapper não inicializado.");
        private SceneLoader SceneLoader => Bootstrapper.SceneLoader;
        private SceneSyncController SceneSyncController => Bootstrapper.SceneSyncController;

        private SceneGroup pendingHostSceneGroup;
        private NetworkSessionState _state = NetworkSessionState.Idle;
        public NetworkSessionState State => _state;

        public void Initialize(SceneManagementBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_networkManager == null)
                _networkManager = NetworkManager.Singleton;

            _transportProvider = GetComponent<INetworkTransportProvider>();
        }

        private void Start()
        {
            if (_networkManager == null) return;
            _networkManager.OnServerStarted += HandleServerStarted;
            _networkManager.OnServerStopped += HandleServerStopped;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        }

        private void OnDestroy()
        {
            if (_networkManager == null) return;
            _networkManager.OnServerStarted -= HandleServerStarted;
            _networkManager.OnServerStopped -= HandleServerStopped;
            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        public bool StartHost(SceneGroup sceneGroupAfterStart = null)
        {
            if (!CanUseNetworkManager()) return false;

            if (_state != NetworkSessionState.Idle)
            {
                Debug.LogWarning($"[Netcode] Não é possível iniciar host no estado atual: {_state}");
                return false;
            }

            if (_networkManager.IsListening)
            {
                Debug.LogWarning("[Netcode] Já existe uma sessão ativa.");
                return false;
            }

            _transportProvider?.ConfigureAsHost();

            _state = NetworkSessionState.StartingHost;
            pendingHostSceneGroup = sceneGroupAfterStart;

            bool success = _networkManager.StartHost();
            Debug.Log(success ? "[Netcode] Host iniciado com sucesso." : "[Netcode] Falha ao iniciar host.");

            if (!success)
            {
                pendingHostSceneGroup = null;
                _state = NetworkSessionState.Idle;
            }

            return success;
        }

        public bool StartClient(ulong targetId = 0)
        {
            if (!CanUseNetworkManager()) return false;

            if (_state != NetworkSessionState.Idle)
            {
                Debug.LogWarning($"[Netcode] Não é possível iniciar client no estado atual: {_state}");
                return false;
            }

            if (_networkManager.IsListening)
            {
                Debug.LogWarning("[Netcode] Já existe uma sessão ativa.");
                return false;
            }

            _transportProvider.ConfigureAsClient(targetId);

            _state = NetworkSessionState.StartingClient;

            bool success = _networkManager.StartClient();
            Debug.Log(success ? $"[Netcode] Client iniciado." : "[Netcode] Falha ao iniciar client.");

            if (!success)
            {
                _state = NetworkSessionState.Idle;
                return false;
            }

            _state = NetworkSessionState.Connecting;
            SceneLoader?.RegisterNetworkCallbacks();

            return true;
        }

        public bool StartSingleplayer(SceneGroup sceneGroup = null)
        {
            return StartHost(sceneGroup);
        }

        private void HandleServerStarted()
        {
            if (_state == NetworkSessionState.StartingHost)
            {
                _state = NetworkSessionState.InSession;
            }

            if (pendingHostSceneGroup == null) return;

            if (_bootstrapper.SceneSyncController == null)
            {
                Debug.LogError("[Netcode] SceneSyncController não encontrado.");
                pendingHostSceneGroup = null;
                return;
            }

            SceneGroup groupToLoad = pendingHostSceneGroup;
            pendingHostSceneGroup = null;

            SceneSyncController.RequestHostLoadSceneGroup(groupToLoad);
        }

        private void HandleServerStopped(bool isServer)
        {
            Debug.Log("[Netcode] Servidor parado localmente.");

            if (_state != NetworkSessionState.ReturningToMenu)
            {
                _state = NetworkSessionState.ReturningToMenu;
            }

            ReturnToMenuOnce();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId != _networkManager.LocalClientId) return;

            if (_state == NetworkSessionState.Connecting)
            {
                _state = NetworkSessionState.InSession;
            }
            else
            {
                Debug.LogWarning($"[Netcode] OnClientConnectedCallback chamado mas estado já não é Connecting: {_state}");
            }
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (_networkManager == null || clientId != _networkManager.LocalClientId) return;

            if (_state == NetworkSessionState.LeavingSession)
            {
                Debug.Log("[Netcode] Cliente saiu manualmente.");
            }
            else
            {
                Debug.LogWarning("[Netcode] Sessão encerrada.");
            }

            SceneLoader.ResetNetworkLoadingState();
            _state = NetworkSessionState.ReturningToMenu;
            ReturnToMenuOnce();
        }

        public void LeaveSession()
        {
            if (_networkManager == null || !_networkManager.IsListening) return;

            if (_state != NetworkSessionState.InSession)
            {
                Debug.LogWarning($"[Netcode] Não é possível sair da sessão no estado atual: {_state}");
                return;
            }

            _state = NetworkSessionState.LeavingSession;
            _networkManager.Shutdown();

            Debug.Log("[Netcode] Saindo da sessão e voltando ao menu...");
            SceneLoader?.ResetNetworkLoadingState();
        }

        public void Shutdown()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _state = NetworkSessionState.LeavingSession;
                _networkManager.Shutdown();
            }
        }

        private void ReturnToMenuOnce()
        {
            if (_state != NetworkSessionState.ReturningToMenu) return;

            if (SceneSyncController != null)
                UnityTaskRunner.RunSafe(ReturnToMenuFlow());
        }

        private async Task ReturnToMenuFlow()
        {
            try
            {
                await SceneSyncController.LoadMainMenuAfterShutdown();
            }
            finally
            {
                _state = NetworkSessionState.Idle;
            }
        }

        private bool CanUseNetworkManager()
        {
            if (_networkManager == null)
            {
                Debug.LogError("[Netcode] NetworkManager não atribuído.");
                return false;
            }

            return true;
        }
    }
}
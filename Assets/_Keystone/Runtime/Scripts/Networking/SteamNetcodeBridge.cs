using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Steamworks;
using UnityEngine;
using Assets._Keystone.Runtime.Scripts.SceneManagement;
using Assets._Keystone.Runtime.Scripts.SceneManagement.Extensions;
using System.Threading.Tasks;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    public class SteamNetcodeBridge : MonoBehaviour
    {
        public enum NetworkSessionState
        {
            Idle,
            StartingHost,
            StartingClient,
            Connecting,
            InSession,
            LeavingSession,
            ReturningToMenu
        }

        public static SteamNetcodeBridge Instance { get; private set; }

        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private FacepunchTransport facepunchTransport;

        private SceneManagementBootstrapper _bootstrapper;
        private SceneManagementBootstrapper Bootstrapper => _bootstrapper ?? throw new System.InvalidOperationException("SceneManagementBootstrapper não inicializado.");

        private SceneLoader SceneLoader => Bootstrapper.SceneLoader;
        private SceneSyncController SceneSyncController => Bootstrapper.SceneSyncController;

        private SceneGroup pendingHostSceneGroup = null;
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

            if (facepunchTransport == null && _networkManager != null)
                facepunchTransport = _networkManager.GetComponent<FacepunchTransport>();
        }

        private void Start()
        {
            if (_networkManager != null)
            {
                _networkManager.OnServerStarted += HandleServerStarted;
                _networkManager.OnServerStopped += HandleServerStopped;
                _networkManager.OnClientConnectedCallback += HandleClientConnected;
                _networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            }
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnServerStarted -= HandleServerStarted;
                _networkManager.OnServerStopped -= HandleServerStopped;
                _networkManager.OnClientConnectedCallback -= HandleClientConnected;
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
        }

        public bool StartHost(SceneGroup sceneGroupAfterStart = null)
        {
            if (!CanUseNetworkManager())
                return false;

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

        private void HandleServerStarted()
        {
            if (_state == NetworkSessionState.StartingHost)
            {
                _state = NetworkSessionState.InSession;
            }

            if (pendingHostSceneGroup == null)
                return;

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

        public bool StartClient(SteamId hostSteamId)
        {
            if (!CanUseNetworkManager())
                return false;

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

            _state = NetworkSessionState.StartingClient;

            if (_networkManager.NetworkConfig.NetworkTransport is FacepunchTransport)
            {
                if (facepunchTransport == null)
                {
                    Debug.LogError("[Netcode] FacepunchTransport não encontrado.");
                    _state = NetworkSessionState.Idle;
                    return false;
                }
                facepunchTransport.targetSteamId = hostSteamId;
            }
            else
            {
                Debug.Log("[Netcode] Usando transporte padrão.");
            }

            bool success = _networkManager.StartClient();
            Debug.Log(success ? $"[Netcode] Client iniciado para host {hostSteamId}." : "[Netcode] Falha ao iniciar client.");

            if (!success) return false;


            _state = NetworkSessionState.Connecting;
            SceneLoader?.RegisterNetworkCallbacks();

            return success;
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == _networkManager.LocalClientId)
            {
                if (_state == NetworkSessionState.Connecting)
                {
                    _state = NetworkSessionState.InSession;
                }
                else
                {
                    Debug.LogWarning($"[Netcode] OnClientConnectedCallback chamado mas estado já não é Connecting: {_state}");
                }
            }
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (_networkManager == null || clientId != _networkManager.LocalClientId)
                return;

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
            if (_networkManager == null || !_networkManager.IsListening)
                return;

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

        private void ReturnToMenuOnce()
        {
            if (_state != NetworkSessionState.ReturningToMenu)
                return;

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

        public void Shutdown()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _state = NetworkSessionState.LeavingSession;
                _networkManager.Shutdown();
                Debug.Log("[Netcode] Sessão encerrada.");
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
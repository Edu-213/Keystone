using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Steamworks;
using UnityEngine;
using Assets.Scripts.Core.Network;
using Assets._Keystone.Runtime.Scripts.SceneManagement;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    public class SteamNetcodeBridge : MonoBehaviour
    {
        public static SteamNetcodeBridge Instance { get; private set; }

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FacepunchTransport facepunchTransport;
        [SerializeField] private SceneSyncController sceneSyncController;

        private SceneGroup pendingHostSceneGroup = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (facepunchTransport == null && networkManager != null)
                facepunchTransport = networkManager.GetComponent<FacepunchTransport>();

            if (sceneSyncController == null)
                sceneSyncController = FindFirstObjectByType<SceneSyncController>();
        }

        private void Start()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted += HandleServerStarted;
                networkManager.OnServerStopped += HandleServerStopped;
                networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            }
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
                networkManager.OnServerStopped -= HandleServerStopped;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
        }

        public bool StartHost(SceneGroup sceneGroupAfterStart = null)
        {
            if (!CanUseNetworkManager())
                return false;

            _isLeavingManually = false;
            _isReturningToMenu = false;

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[Netcode] Já existe uma sessão ativa.");
                return false;
            }
            pendingHostSceneGroup = sceneGroupAfterStart;

            bool success = networkManager.StartHost();
            Debug.Log(success
                ? "[Netcode] Host iniciado com sucesso."
                : "[Netcode] Falha ao iniciar host.");

            return success;
        }

        private void HandleServerStarted()
        {
            if (pendingHostSceneGroup == null)
                return;

            if (sceneSyncController == null)
            {
                Debug.LogError("[Netcode] SceneSyncController não encontrado.");
                return;
            }

            SceneGroup groupToLoad = pendingHostSceneGroup;
            pendingHostSceneGroup = null;

            sceneSyncController.HostLoadSceneGroupWrapper(groupToLoad);
        }

        private void HandleServerStopped(bool isServer)
        {
            Debug.Log("[Netcode] Servidor parado localmente.");
            ReturnToMenuOnce();
        }

        public bool StartClient(SteamId hostSteamId)
        {
            if (!CanUseNetworkManager())
                return false;

            _isLeavingManually = false;
            _isReturningToMenu = false;

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[Netcode] Já existe uma sessão ativa.");
                return false;
            }

            if (networkManager.NetworkConfig.NetworkTransport is Netcode.Transports.Facepunch.FacepunchTransport)
            {
                if (facepunchTransport == null)
                {
                    Debug.LogError("[Netcode] FacepunchTransport não encontrado.");
                    return false;
                }
                facepunchTransport.targetSteamId = hostSteamId;
            }
            else
            {
                Debug.Log("[Netcode] Usando transporte padrão (Provavelmente UnityTransport).");
            }

            bool success = networkManager.StartClient();
            Debug.Log(success
                ? $"[Netcode] Client iniciado para host {hostSteamId}."
                : "[Netcode] Falha ao iniciar client.");

            if (success)
            {
                if (sceneSyncController == null)
                    sceneSyncController = FindFirstObjectByType<SceneSyncController>();

                var sceneLoader = FindFirstObjectByType<SceneLoader>();
                sceneLoader?.RegisterNetworkCallbacks();
            }

            return success;
        }

        private bool _isLeavingManually;
        private bool _isReturningToMenu;
        private void HandleClientDisconnect(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
                return;

            if (_isLeavingManually)
            {
                Debug.Log("[Netcode] Cliente saiu manualmente.");
                _isLeavingManually = false;
            }
            else
            {
                Debug.LogWarning("[Netcode] Sessão encerrada.");
            }

            CleanupSceneLoadingState();
            ReturnToMenuOnce();
        }

        public void LeaveSession()
        {
            if (networkManager == null || !networkManager.IsListening || _isLeavingManually)
                return;

            _isLeavingManually = true;
            networkManager.Shutdown();

            Debug.Log("[Netcode] Saindo da sessão e voltando ao menu...");
            CleanupSceneLoadingState();
        }

        private void CleanupSceneLoadingState()
        {
            var sceneLoader = FindFirstObjectByType<SceneLoader>();
            sceneLoader?.ResetNetworkLoadingState();
        }

        private void ReturnToMenuOnce()
        {
            if (_isReturningToMenu)
                return;

            _isReturningToMenu = true;

            if (sceneSyncController == null)
                sceneSyncController = FindFirstObjectByType<SceneSyncController>();

            if (sceneSyncController != null)
                _ = sceneSyncController.LoadMainMenuAfterShutdown();
        }

        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
                Debug.Log("[Netcode] Sessão encerrada.");
            }
        }

        private bool CanUseNetworkManager()
        {
            if (networkManager == null)
            {
                Debug.LogError("[Netcode] NetworkManager não atribuído.");
                return false;
            }

            return true;
        }
    }
}
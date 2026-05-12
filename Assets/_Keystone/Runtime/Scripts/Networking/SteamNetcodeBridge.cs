using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Steamworks;
using UnityEngine;
using Assets.Scripts.Core.Network;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    public class SteamNetcodeBridge : MonoBehaviour
    {
        public static SteamNetcodeBridge Instance { get; private set; }

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FacepunchTransport facepunchTransport;
        [SerializeField] private SceneSyncController sceneSyncController;

        private int pendingHostSceneIndex = -1;

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

        private void OnEnable()
        {
            if (networkManager != null)
                networkManager.OnServerStarted += HandleServerStarted;
        }

        private void OnDisable()
        {
            if (networkManager != null)
                networkManager.OnServerStarted -= HandleServerStarted;
        }


        public bool StartHost(int sceneIndexAfterStart = -1)
        {
            if (!CanUseNetworkManager())
                return false;

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[Netcode] Já existe uma sessão ativa.");
                return false;
            }
            pendingHostSceneIndex = sceneIndexAfterStart;

            bool success = networkManager.StartHost();
            Debug.Log(success
                ? "[Netcode] Host iniciado com sucesso."
                : "[Netcode] Falha ao iniciar host.");

            return success;
        }

        private void HandleServerStarted()
        {
            if (pendingHostSceneIndex < 0)
                return;

            if (sceneSyncController == null)
                sceneSyncController = FindFirstObjectByType<SceneSyncController>();

            if (sceneSyncController == null)
            {
                Debug.LogError("[Netcode] SceneSyncController não encontrado.");
                return;
            }

            int indexToLoad = pendingHostSceneIndex;
            pendingHostSceneIndex = -1;

            sceneSyncController.HostLoadSceneGroupAsync(indexToLoad);
        }

        public bool StartClient(SteamId hostSteamId)
        {
            if (!CanUseNetworkManager())
                return false;

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
                // Aqui o UnityTransport usará o IP/Porta definidos no componente no Inspector
            }

            bool success = networkManager.StartClient();
            Debug.Log(success
                ? $"[Netcode] Client iniciado para host {hostSteamId}."
                : "[Netcode] Falha ao iniciar client.");

            return success;
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
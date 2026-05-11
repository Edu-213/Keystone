using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Steamworks;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    public class SteamNetcodeBridge : MonoBehaviour
    {
        public static SteamNetcodeBridge Instance { get; private set; }

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FacepunchTransport facepunchTransport;

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
        }

        public bool StartHost()
        {
            if (!CanUseNetworkManager())
                return false;

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[Netcode] Já existe uma sessão ativa.");
                return false;
            }

            bool success = networkManager.StartHost();
            Debug.Log(success
                ? "[Netcode] Host iniciado com sucesso."
                : "[Netcode] Falha ao iniciar host.");

            return success;
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

            if (facepunchTransport == null)
            {
                Debug.LogError("[Netcode] FacepunchTransport não encontrado.");
                return false;
            }

            facepunchTransport.targetSteamId = hostSteamId;

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
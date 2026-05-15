using Steamworks;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    public class GameNetworkManager : MonoBehaviour
    {
        public static GameNetworkManager Instance { get; private set; }
        
        public uint appId = 480;
        public bool IsSteamInitialized { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            TryInitializeSteam();

        }

        private void Update()
        {
            if (IsSteamInitialized)
            {
                SteamClient.RunCallbacks();
            }
        }

        private void OnApplicationQuit()
        {
            ShutdownSteam();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                ShutdownSteam();
        }

        private void TryInitializeSteam()
        {
            if (IsSteamInitialized)
                return;

            try
            {
                SteamClient.Init(appId, true);
                IsSteamInitialized = SteamClient.IsValid;

                if (IsSteamInitialized)
                    Debug.Log("[Steam] Steam inicializado com sucesso.");
                else
                    Debug.LogError("[Steam] SteamClient.Init executou, mas SteamClient.IsValid == false.");
            }
            catch (System.Exception e)
            {
                IsSteamInitialized = false;
                Debug.LogError($"[Steam] Falha ao inicializar Steam: {e}");
            }
        }

        private void ShutdownSteam()
        {
            if (!IsSteamInitialized)
                return;

            SteamClient.Shutdown();
            IsSteamInitialized = false;

            if (Instance == this)
                Instance = null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets._Keystone.Runtime.Scripts.SceneManagement;
using Assets._Keystone.Runtime.Scripts.SceneManagement.Extensions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    public class SceneSyncController : NetworkBehaviour
    {
        private SceneGroup currentSceneGroup = null;
        private bool playersSpawnedForCurrentLoad;
        private bool _isLoadingGroup = false;

        private SceneManagementBootstrapper _bootstrapper;
        private NetworkManager _networkManager;

        private SceneLoader SceneLoader => _bootstrapper?.SceneLoader ?? throw new InvalidOperationException("SceneManagementBootstrapper ou SceneLoader não inicializado.");

        void Awake()
        {
            _networkManager = NetworkManager.Singleton;
        }

        public void Initialize(SceneManagementBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
        }

        private void Start()
        {
            if (_networkManager == null)
            {
                _networkManager = NetworkManager.Singleton;
            }

            if (_networkManager?.SceneManager != null)
            {
                _networkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (_networkManager.SceneManager != null)
            {
                _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            }
        }

        public void RequestHostLoadSceneGroup(SceneGroup group)
        {
            UnityTaskRunner.RunSafe(HostLoadSceneGroup(group));
        }

        private async Task HostLoadSceneGroup(SceneGroup group)
        {
            if (!IsServer) return;
            if (_isLoadingGroup)
            {
                Debug.LogWarning("Tentativa de carregar grupo enquanto outro está em progresso!");
                return;
            }

            _isLoadingGroup = true;
            try
            {
                currentSceneGroup = group;
                playersSpawnedForCurrentLoad = false;

                await SceneLoader.LoadSceneGroup(group, useNetworkScene: true);
            }
            finally
            {
                _isLoadingGroup = false;
            }
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!IsServer) return;
            if (playersSpawnedForCurrentLoad) return;
            if (currentSceneGroup == null) return;

            string activeSceneName = currentSceneGroup.GetActiveSceneName();
            if (sceneName != activeSceneName) return;

            playersSpawnedForCurrentLoad = true;

            foreach (var clientId in clientsCompleted)
            {
                // PlayerSpawner.Instance.SpawnPlayer(clientId);
            }

            foreach (var clientId in clientsTimedOut)
            {
                Debug.LogWarning($"Client {clientId} timed out loading scene {sceneName}");
            }
        }

        public async Task LoadMainMenuAfterShutdown(int timeoutMs = 10000)
        {
            try
            {
                await WaitForNetworkShutdown(timeoutMs);
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning(ex.Message);
            }

            await SceneLoader.LoadSceneGroup(SceneLoader.MainMenuGroup, useNetworkScene: false);
            //GameUtils.ShowCursor(true);
        }

        private static async Task WaitForNetworkShutdown(int timeoutMs, int delayStep = 100)
        {
            int waited = 0;

            while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                await Task.Delay(delayStep);
                waited += delayStep;

                if (waited >= timeoutMs)
                    throw new TimeoutException("Timeout esperando NetworkManager parar antes de carregar MainMenu.");
            }
        }

        [ClientRpc]
        public void ReturnAllClientsToMainMenuClientRpc()
        {
            if (SteamNetcodeBridge.Instance != null)
            {
                SteamNetcodeBridge.Instance.LeaveSession();
            }
            else
            {
                UnityTaskRunner.RunSafe(FireAndForgetLoadMainMenu());
            }
        }

        private async Task FireAndForgetLoadMainMenu()
        {
            try
            {
                await LoadMainMenuAfterShutdown();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro carregando MainMenu no cliente: {ex}");
            }
        }
    }
}
using System;
using System.Threading.Tasks;
using Assets._Keystone.Runtime.Scripts.Networking;
using Assets._Keystone.Runtime.Scripts.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Core.Network
{
    public class SceneSyncController : NetworkBehaviour
    {
        [SerializeField] private SceneManagementBootstrapper sceneBootstrapper;
        private int currentSceneGroupIndex = -1;
        private bool playersSpawnedForCurrentLoad;
        private bool _isLoadingGroup = false;

        private void Start()
        {
            if (sceneBootstrapper == null)
            {
                sceneBootstrapper = FindFirstObjectByType<SceneManagementBootstrapper>();
            }

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SceneManager != null)
            {
                nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            }
        }

        public void HostLoadSceneGroupWrapper(int sceneIndex)
        {
            _ = HostLoadSceneGroup(sceneIndex);
        }

        private async Task HostLoadSceneGroup(int sceneIndex)
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
                currentSceneGroupIndex = sceneIndex;
                playersSpawnedForCurrentLoad = false;

                await sceneBootstrapper.SceneLoader.LoadSceneGroup(sceneIndex, useNetworkScene: true);
            }
            finally
            {
                _isLoadingGroup = false;
            }
        }

        private void OnLoadEventCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            System.Collections.Generic.List<ulong> clientsCompleted,
            System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            if (!IsServer) return;
            if (playersSpawnedForCurrentLoad) return;
            if (currentSceneGroupIndex < 0) return;

            var group = sceneBootstrapper.SceneLoader.GetSceneGroup(currentSceneGroupIndex);
            if (group == null) return;

            string activeSceneName = group.FindSceneNameByType(SceneType.ActiveScene);
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
            int waited = 0;
            int delayStep = 100;

            while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                await Task.Delay(delayStep);

                waited += delayStep;

                if (waited >= timeoutMs)
                {
                    Debug.LogWarning("Timeout esperando NetworkManager parar antes de carregar MainMenu.");
                    break;
                }
            }

            await sceneBootstrapper.SceneLoader.LoadSceneGroup(0, useNetworkScene: false);
            //GameUtils.ShowCursor(true);
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
                _ = FireAndForgetLoadMainMenu();
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
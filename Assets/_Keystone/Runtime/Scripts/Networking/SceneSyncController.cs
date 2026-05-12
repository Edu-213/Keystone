using System.Threading.Tasks;
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

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            }
        }

        public async void HostLoadSceneGroupAsync(int sceneIndex)
        {
            if (!IsServer) return;

            currentSceneGroupIndex = sceneIndex;
            playersSpawnedForCurrentLoad = false;

            await sceneBootstrapper.SceneLoader.LoadSceneGroup(sceneIndex, useNetworkScene: true);
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

        public async Task LoadMainMenuAfterShutdownAsync()
        {
            while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                await Task.Delay(100);
            }

            await sceneBootstrapper.SceneLoader.LoadSceneGroup(0, useNetworkScene: false);
            //GameUtils.ShowCursor(true);
        }

        [ClientRpc]
        public void ReturnAllClientsToMainMenuClientRpc()
        {
            if (IsServer) return;
            _ = LoadMainMenuAfterShutdownAsync();
        }
    }
}
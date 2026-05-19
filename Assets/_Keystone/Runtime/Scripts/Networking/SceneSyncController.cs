using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets._Keystone.Runtime.Scripts.Events;
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

        private readonly HashSet<ulong> _spawnedClients = new();
        private bool _isLoadingGroup = false;

        private SceneManagementBootstrapper _bootstrapper;
        private NetworkManager _networkManager;

        private SceneLoader SceneLoader => _bootstrapper?.SceneLoader ?? throw new InvalidOperationException("SceneManagementBootstrapper ou SceneLoader não inicializado.");

        void Awake()
        {
            _networkManager = NetworkManager.Singleton;
            NetworkEvents.OnHostGameplayReady += HandleHostGameplaySceneReady;
            NetworkEvents.OnClientGameplayReady += HandleClientReadyLocalContext;

            if (_networkManager != null)
                _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
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
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            NetworkEvents.OnHostGameplayReady -= HandleHostGameplaySceneReady;
            NetworkEvents.OnClientGameplayReady -= HandleClientReadyLocalContext;

            if (_networkManager != null)
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
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
                _spawnedClients.Clear();

                await SceneLoader.LoadSceneGroup(group, useNetworkScene: true);
            }
            finally
            {
                _isLoadingGroup = false;
            }
        }

        private void HandleClientReadyLocalContext(ulong clientId)
        {
            if (IsServer)
            {
                HandleClientSynchronized(clientId);
            }
            else if (IsClient)
            {
                NotifyServerClientReadyRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void NotifyServerClientReadyRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[SceneSync] Servidor recebeu aviso de que o Client {senderClientId} está pronto.");
            HandleClientSynchronized(senderClientId);
        }

        private void HandleHostGameplaySceneReady()
        {
            if (!IsServer) return;

            ulong hostClientId = _networkManager.LocalClientId;
            TryDispatchSpawn(hostClientId);
        }

        private void HandleClientSynchronized(ulong clientId)
        {
            if (!IsServer) return;

            TryDispatchSpawn(clientId);
        }

        private void TryDispatchSpawn(ulong clientId)
        {
            if (!_spawnedClients.Add(clientId))
            {
                Debug.Log($"[SceneSync] Client {clientId} já teve spawn disparado.");
                return;
            }

            Debug.Log($"[SceneSync] Disparando spawn para client {clientId}");
            NetworkEvents.RaisePlayerSpawnRequested(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            _spawnedClients.Remove(clientId);
            Debug.Log($"[SceneSync] Client {clientId} removido de _spawnedClients após disconnect.");
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
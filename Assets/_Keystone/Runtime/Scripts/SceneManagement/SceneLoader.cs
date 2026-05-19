using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets._Keystone.Runtime.Scripts.Events;
using Assets._Keystone.Runtime.Scripts.SceneManagement.Exceptions;
using Assets._Keystone.Runtime.Scripts.SceneManagement.Extensions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets._Keystone.Runtime.Scripts.SceneManagement
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private Image _loadingBar;
        [SerializeField] private float _fillSpeed = 2f;
        [SerializeField] private Canvas _loadingCanvas;
        [SerializeField] private Camera _loadingCamera;

        [Header("Scene Groups")]
        [SerializeField] private SceneGroup _mainMenuGroup;

        private bool _isFirstLoad = true;
        private float _targetProgress;
        private bool isLoading;
        private bool _isLoadingGroup = false;

        private SceneGroup _pendingNetworkGroup;
        private TaskCompletionSource<bool> _networkSceneLoadedTcs;
        private bool _networkCallbacksRegistered;

        private SceneGroupManager _manager;
        private NetworkManager _networkManager;

        public SceneGroup MainMenuGroup => _mainMenuGroup;

        private void Awake()
        {
            _manager ??= new SceneGroupManager();
            _networkManager = NetworkManager.Singleton;
        }

        private void Start()
        {
            if (_networkManager == null)
            {
                _networkManager = NetworkManager.Singleton;
            }

            _networkManager.OnClientConnectedCallback += OnLocalClientConnected;
            UnityTaskRunner.RunSafe(StartMainMenuLoad());
        }

        private void Update()
        {
            if (!isLoading) return;

            _loadingBar.fillAmount = Mathf.MoveTowards(_loadingBar.fillAmount, _targetProgress, _fillSpeed * Time.deltaTime);
        }

        private void OnDestroy()
        {
            UnregisterNetworkCallbacks();
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= OnLocalClientConnected;
            }
        }

        private void OnLocalClientConnected(ulong id)
        {
            if (id != _networkManager.LocalClientId) return;

            RegisterNetworkCallbacks();
        }

        private async Task StartMainMenuLoad()
        {
            try
            {
                await LoadSceneGroup(_mainMenuGroup, useNetworkScene: false);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public async Task LoadSceneGroup(SceneGroup group, bool useNetworkScene)
        {
            if (_isLoadingGroup)
            {
                Debug.LogWarning("LoadSceneGroup chamado enquanto outro carregamento está em andamento!");
                return;
            }

            Time.timeScale = 1f;
            _isLoadingGroup = true;

            bool isMainMenu = group == _mainMenuGroup;
            bool showLoading = !isMainMenu || !_isFirstLoad;

            if (showLoading)
            {
                ShowLoading();
                await Task.Yield();
            }

            try
            {
                _ = GetValidatedActiveSceneName(group);

                if (useNetworkScene)
                {
                    await LoadSceneGroupMultiplayer(group);
                }
                else
                {
                    await LoadSceneGroupSingleplayer(group);
                }

                if (!isMainMenu)
                {
                    // DataPersistenceManager.instance?.ForceReloadAndLoadGame();
                }
            }
            finally
            {
                if (showLoading && _loadingBar != null)
                {
                    while (_loadingBar.fillAmount < 0.98f)
                    {
                        await Task.Yield();
                    }
                }

                if (showLoading) HideLoading();

                _isFirstLoad = false;
                _isLoadingGroup = false;
            }
        }

        private async Task LoadSceneGroupSingleplayer(SceneGroup group)
        {
            string activeSceneName = GetValidatedActiveSceneName(group);
            _targetProgress = 0.05f;

            var singleLoad = SceneManager.LoadSceneAsync(activeSceneName, LoadSceneMode.Single) ?? throw new InvalidOperationException($"Falha ao iniciar carregamento da cena ativa: {activeSceneName}");

            singleLoad.allowSceneActivation = false;

            while (singleLoad.progress < 0.9f)
            {
                float realProgress = Mathf.Clamp01(singleLoad.progress / 0.9f);
                _targetProgress = Mathf.Lerp(0.05f, 0.50f, realProgress);
                await Task.Yield();
            }

            singleLoad.allowSceneActivation = true;

            while (!singleLoad.isDone)
            {
                await Task.Yield();
            }

            var progress = new LoadingProgress();
            progress.Progressed += value => _targetProgress = Mathf.Lerp(0.50f, 0.95f, value);

            await _manager.LoadScenes(group, s => s.SceneType != SceneType.ActiveScene, progress, reloadDupScenes: false, unloadExisting: false);
            _targetProgress = 1f;
        }

        private async Task LoadSceneGroupMultiplayer(SceneGroup group)
        {
            RegisterNetworkCallbacks();

            if (!_networkManager.IsListening)
                throw new NetworkSceneLoadException("NetworkManager não está em estado de escuta.");

            if (_networkManager.SceneManager == null)
                throw new NetworkSceneLoadException("NetworkSceneManager é nulo.");

            _pendingNetworkGroup = group;
            _networkSceneLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            string networkSceneName = GetValidatedActiveSceneName(group);

            if (_networkManager.IsServer)
                _networkManager.SceneManager.LoadScene(networkSceneName, LoadSceneMode.Single);

            int timeoutMs = Mathf.Max(1, _networkManager.NetworkConfig.LoadSceneTimeOut) * 1000;
            try
            {
                await _networkSceneLoadedTcs.Task.TimeoutAfter(timeoutMs, $"Timeout ao carregar a cena de rede '{networkSceneName}'.");
            }
            catch
            {
                ClearPendingNetworkLoad();
                throw;
            }
        }

        private void HandleNetworkSceneEventSafe(SceneEvent sceneEvent)
        {
            UnityTaskRunner.RunSafe(HandleNetworkSceneEvent(sceneEvent));
        }

        private async Task HandleNetworkSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != _networkManager.LocalClientId)
                return;

            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                    if (!isLoading) ShowLoading();
                    _targetProgress = Mathf.Max(_targetProgress, 0.05f);

                    while (sceneEvent.AsyncOperation != null && !sceneEvent.AsyncOperation.isDone)
                    {
                        float rawProgress = Mathf.Clamp01(sceneEvent.AsyncOperation.progress / 0.9f);
                        _targetProgress = Mathf.Lerp(0.05f, 0.50f, rawProgress);
                        await Task.Yield();
                    }
                    break;
                case SceneEventType.LoadComplete:
                    _targetProgress = 0.50f;
                    break;
                case SceneEventType.SynchronizeComplete:
                    _targetProgress = 1f;

                    while (_loadingBar.fillAmount < 0.98f)
                    {
                        await Task.Yield();
                    }

                    HideLoading();
                    Debug.Log($"[SceneLoader] Client synchronized: {sceneEvent.ClientId}");
                    NetworkEvents.RaiseClientGameplayReady(sceneEvent.ClientId);

                    ClearPendingNetworkLoad();
                    break;
            }
        }

        private void HandleNetworkLoadEventCompletedSafe(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            UnityTaskRunner.RunSafe(HandleNetworkLoadEventCompleted(sceneName, loadSceneMode, clientsCompleted, clientsTimedOut));
        }

        private async Task HandleNetworkLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (_pendingNetworkGroup == null) return;

            string expectedScene = _pendingNetworkGroup.FindSceneNameByType(SceneType.ActiveScene);
            if (sceneName != expectedScene) return;

            var progress = new LoadingProgress();
            progress.Progressed += value => _targetProgress = Mathf.Lerp(0.50f, 0.95f, value);

            try
            {
                await _manager.LoadScenes(_pendingNetworkGroup, s => s.SceneType != SceneType.ActiveScene, progress, reloadDupScenes: false, unloadExisting: false);

                _targetProgress = 1f;
                _networkSceneLoadedTcs?.TrySetResult(true);

                if (_networkManager.IsServer)
                {
                    Debug.Log("[SceneLoader] Host gameplay scene ready.");
                    NetworkEvents.RaiseHostGameplayReady();
                }
            }
            catch (Exception ex)
            {
                _networkSceneLoadedTcs?.TrySetException(ex);
                Debug.LogException(ex);
            }
            finally
            {
                ClearPendingNetworkLoad();
            }
        }

        private static string GetValidatedActiveSceneName(SceneGroup group)
        {
            if (group == null)
                throw new InvalidSceneGroupException("SceneGroup é nulo.");

            string activeSceneName = group.GetActiveSceneName();

            if (string.IsNullOrWhiteSpace(activeSceneName))
                throw new InvalidSceneGroupException($"SceneGroup '{group.GroupName}' sem ActiveScene definida.");

            return activeSceneName;
        }

        public void RegisterNetworkCallbacks()
        {
            if (_networkCallbacksRegistered) return;

            var sceneManager = _networkManager.SceneManager;
            if (sceneManager == null) return;

            sceneManager.OnSceneEvent += HandleNetworkSceneEventSafe;
            sceneManager.OnLoadEventCompleted += HandleNetworkLoadEventCompletedSafe;
            _networkCallbacksRegistered = true;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (!_networkCallbacksRegistered) return;

            var sceneManager = _networkManager.SceneManager;
            if (sceneManager == null) return;

            sceneManager.OnSceneEvent -= HandleNetworkSceneEventSafe;
            sceneManager.OnLoadEventCompleted -= HandleNetworkLoadEventCompletedSafe;
            _networkCallbacksRegistered = false;
        }

        private void ShowLoading()
        {
            _loadingBar.fillAmount = 0f;
            _targetProgress = 0f;
            EnableLoadingCanvas(true);
        }

        private void HideLoading()
        {
            EnableLoadingCanvas(false);
            //AudioManager.instance.StopMusic();
        }

        private void EnableLoadingCanvas(bool enable = true)
        {
            isLoading = enable;
            _loadingCanvas.gameObject.SetActive(enable);
            _loadingCamera.gameObject.SetActive(enable);
        }

        public void ResetNetworkLoadingState()
        {
            _networkSceneLoadedTcs?.TrySetCanceled();
            ClearPendingNetworkLoad();
            _targetProgress = 0f;
            isLoading = false;
            UnregisterNetworkCallbacks();
            _networkCallbacksRegistered = false;
        }

        private void ClearPendingNetworkLoad()
        {
            _pendingNetworkGroup = null;
            _networkSceneLoadedTcs = null;
        }
    }

    public class LoadingProgress : IProgress<float>
    {
        public event Action<float> Progressed;
        public void Report(float value)
        {
            Progressed?.Invoke(value);
        }
    }
}
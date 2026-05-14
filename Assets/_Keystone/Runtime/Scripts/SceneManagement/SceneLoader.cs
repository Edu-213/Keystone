using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets._Keystone.Runtime.Scripts.SceneManagement
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private Image _loadingBar;
        [SerializeField] private float _fillSpeed = 0.5f;
        [SerializeField] private Canvas _loadingCanvas;
        [SerializeField] private Camera _loadingCamera;

        [Header("Scene Groups")]
        [SerializeField] private SceneGroup _mainMenuGroup;

        private bool _isFirstLoad = true;
        private float _targetProgress;
        private bool isLoading;
        private bool _isLoadingGroup = false;

        public readonly SceneGroupManager manager = new SceneGroupManager();

        private SceneGroup _pendingNetworkGroup;
        private TaskCompletionSource<bool> _networkSceneLoadedTcs;
        private bool _networkCallbacksRegistered;

        public SceneGroup MainMenuGroup => _mainMenuGroup;

        private async void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnLocalClientConnected;
            await LoadSceneGroup(_mainMenuGroup, useNetworkScene: false);
        }

        private void Update()
        {
            if (!isLoading) return;

            _loadingBar.fillAmount = Mathf.MoveTowards(_loadingBar.fillAmount, _targetProgress, _fillSpeed * Time.deltaTime);
        }

        private void OnDestroy()
        {
            UnregisterNetworkCallbacks();
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnLocalClientConnected;
            }
        }

        private void OnLocalClientConnected(ulong id)
        {
            if (id != NetworkManager.Singleton.LocalClientId) return;

            RegisterNetworkCallbacks();
        }

        public async Task LoadSceneGroup(SceneGroup group, bool useNetworkScene)
        {
            if (_isLoadingGroup)
            {
                Debug.LogWarning("LoadSceneGroup chamado enquanto outro carregamento está em andamento!");
                return;
            }

            Time.timeScale = 1f;

            if (group == null)
            {
                Debug.LogError("Invalid scene group index");
                return;
            }

            _isLoadingGroup = true;
            bool isMainMenu = group == _mainMenuGroup;
            bool showLoading = !isMainMenu || !_isFirstLoad;

            if (showLoading)
            {
                ShowLoading();
            }

            try
            {
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
                if (showLoading)
                {
                    HideLoading();
                }

                _isFirstLoad = false;
                _isLoadingGroup = false;
            }
        }

        private async Task LoadSceneGroupSingleplayer(SceneGroup group)
        {
            var progress = new LoadingProgress();
            progress.Progressed += value =>
            {
                _targetProgress = Mathf.Max(_targetProgress, value);
            };

            string activeSceneName = group.FindSceneNameByType(SceneType.ActiveScene);

            if (string.IsNullOrEmpty(activeSceneName))
            {
                Debug.LogError("SceneGroup sem ActiveScene definida.");
                return;
            }

            _targetProgress = 0.05f;

            var singleLoad = SceneManager.LoadSceneAsync(activeSceneName, LoadSceneMode.Single);
            if (singleLoad == null)
            {
                Debug.LogError($"Falha ao iniciar carregamento da cena ativa: {activeSceneName}");
                return;
            }

            while (!singleLoad.isDone)
            {
                _targetProgress = Mathf.Clamp(singleLoad.progress, 0f, 0.8f);
                await Task.Yield();
            }

            _targetProgress = 0.85f;

            await manager.LoadScenes(
                group,
                s => s.SceneType != SceneType.ActiveScene,
                progress,
                reloadDupScenes: false,
                unloadExisting: false
            );

            _targetProgress = 1f;
        }

        private async Task LoadSceneGroupMultiplayer(SceneGroup group)
        {
            RegisterNetworkCallbacks();

            if (NetworkManager.Singleton == null)
            {
                return;
            }

            if (!NetworkManager.Singleton.IsListening)
            {
                return;
            }

            _pendingNetworkGroup = group;
            _networkSceneLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            string networkSceneName = group.FindSceneNameByType(SceneType.ActiveScene);

            if (string.IsNullOrEmpty(networkSceneName))
            {
                _pendingNetworkGroup = null;
                _networkSceneLoadedTcs = null;
                return;
            }

            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(networkSceneName, LoadSceneMode.Single);
            }

            await _networkSceneLoadedTcs.Task;
        }

        public void ResetNetworkLoadingState()
        {
            _pendingNetworkGroup = null;
            _networkSceneLoadedTcs?.TrySetCanceled();
            _networkSceneLoadedTcs = null;
            _targetProgress = 0f;
            isLoading = false;
            HideLoading();
            UnregisterNetworkCallbacks();
            _networkCallbacksRegistered = false;
        }

        private async void HandleNetworkSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.Load)
            {
                if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId)
                    return;

                if (!isLoading)
                    ShowLoading();

                _targetProgress = Mathf.Max(_targetProgress, 0.15f);

                while (sceneEvent.AsyncOperation != null && !sceneEvent.AsyncOperation.isDone)
                {
                    _targetProgress = Mathf.Clamp01(Mathf.Max(_targetProgress, sceneEvent.AsyncOperation.progress));
                    await Task.Yield();
                }

                return;
            }

            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
            {
                if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId)
                    return;

                _targetProgress = 0.8f;
                return;
            }

            if (sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
            {
                if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId)
                    return;

                if (_networkSceneLoadedTcs == null)
                {
                    _targetProgress = 1f;
                    HideLoading();
                    return;
                }
            }
        }

        private async void HandleNetworkLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            try
            {
                if (_pendingNetworkGroup == null) return;

                string expectedScene = _pendingNetworkGroup.FindSceneNameByType(SceneType.ActiveScene);
                if (sceneName != expectedScene) return;

                var progress = new LoadingProgress();
                progress.Progressed += value => _targetProgress = Mathf.Lerp(0.8f, 1f, value);

                await manager.LoadScenes(_pendingNetworkGroup, s => s.SceneType != SceneType.ActiveScene, progress, reloadDupScenes: false, unloadExisting: false);

                _targetProgress = 1f;
                _networkSceneLoadedTcs?.TrySetResult(true);

                _pendingNetworkGroup = null;
                _networkSceneLoadedTcs = null;
            }
            catch (Exception ex)
            {
                _networkSceneLoadedTcs?.TrySetException(ex);
                _pendingNetworkGroup = null;
                _networkSceneLoadedTcs = null;
                Debug.LogException(ex);
            }
        }

        public void RegisterNetworkCallbacks()
        {
            if (_networkCallbacksRegistered) return;
            if (NetworkManager.Singleton == null) return;

            var sceneManager = NetworkManager.Singleton.SceneManager;
            if (sceneManager == null) return;

            sceneManager.OnSceneEvent += HandleNetworkSceneEvent;
            sceneManager.OnLoadEventCompleted += HandleNetworkLoadEventCompleted;
            _networkCallbacksRegistered = true;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (!_networkCallbacksRegistered) return;
            if (NetworkManager.Singleton == null) return;

            var sceneManager = NetworkManager.Singleton.SceneManager;

            sceneManager.OnSceneEvent -= HandleNetworkSceneEvent;
            sceneManager.OnLoadEventCompleted -= HandleNetworkLoadEventCompleted;
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
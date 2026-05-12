using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.VisualScripting;
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
        [SerializeField] private SceneGroup[] _sceneGroups;

        private bool _isFirstLoad = true;
        private float _targetProgress;
        private bool isLoading;

        public readonly SceneGroupManager manager = new SceneGroupManager();

        private SceneGroup _pendingNetworkGroup;
        private TaskCompletionSource<bool> _networkSceneLoadedTcs;
        private bool _networkCallbacksRegistered;

        /*
                private void Awake()
                {
                    manager.OnSceneLoaded += sceneName => Debug.Log("Loaded:" + sceneName);
                    manager.OnSceneUnloaded += sceneName => Debug.Log("Unloaded:" + sceneName);
                    manager.OnSceneGroupLoaded += () => Debug.Log("Scene group loaded");

                }
        */

        private void OnLocalClientConnected(ulong id)
        {
            Debug.Log("teste");
            if (id == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[SceneLoader] Cliente conectado. Registrando escuta de cenas...");
                // O Netcode garante que agora o SceneManager existe
                RegisterNetworkCallbacksIfPossible();
            }
        }

        private void OnDisable()
        {
            UnregisterNetworkCallbacks();
        }

        private async void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnLocalClientConnected;
            await LoadSceneGroup(0, useNetworkScene: false);
        }

        private void Update()
        {
            if (!isLoading) return;

            float currentFillAmount = _loadingBar.fillAmount;
            float progressDifference = Mathf.Abs(currentFillAmount - _targetProgress);

            float dynamicFillSpeed = progressDifference * _fillSpeed;

            _loadingBar.fillAmount = Mathf.Lerp(currentFillAmount, _targetProgress, Time.deltaTime * dynamicFillSpeed);
        }

        public SceneGroup GetSceneGroup(int index)
        {
            if (index < 0 || index >= _sceneGroups.Length)
                return null;

            return _sceneGroups[index];
        }

        public async Task LoadSceneGroup(int index, bool useNetworkScene)
        {
            Time.timeScale = 1f;

            var group = GetSceneGroup(index);
            if (group == null)
            {
                Debug.LogError("Invalid scene group index:" + index);
                return;
            }

            bool isMainMenu = index == 0;
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
            }
        }

        private async Task LoadSceneGroupSingleplayer(SceneGroup group)
        {
            var progress = new LoadingProgress();
            progress.Progressed += value => _targetProgress = Mathf.Max(_targetProgress, value);

            await manager.LoadAllScenes(group, progress);
        }

        private async Task LoadSceneGroupMultiplayer(SceneGroup group)
        {
            RegisterNetworkCallbacksIfPossible();

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton is null.");
                return;
            }

            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.LogError("NetworkManager is not listening. Start Host/Client first.");
                return;
            }

            _pendingNetworkGroup = group;
            _networkSceneLoadedTcs = new TaskCompletionSource<bool>();

            string networkSceneName = group.FindSceneNameByType(SceneType.ActiveScene);

            if (string.IsNullOrEmpty(networkSceneName))
            {
                Debug.LogError($"SceneGroup {group.GroupName} has no ActiveScene.");
                return;
            }

            if (NetworkManager.Singleton.IsServer)
            {
                var status = NetworkManager.Singleton.SceneManager.LoadScene(networkSceneName, LoadSceneMode.Single);
                Debug.Log($"[SceneLoader] Network LoadScene({networkSceneName}) status = {status}");
            }

            await _networkSceneLoadedTcs.Task;
        }

        private async void HandleNetworkSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId)
                return;

            if (sceneEvent.SceneEventType == SceneEventType.Load)
            {
                ShowLoading();
                _targetProgress = 0.15f;

                while (sceneEvent.AsyncOperation != null && !sceneEvent.AsyncOperation.isDone)
                {
                    _targetProgress = Mathf.Clamp01(Mathf.Max(_targetProgress, sceneEvent.AsyncOperation.progress));
                    await Task.Yield();
                }
            }

            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
            {
                _targetProgress = 0.8f;
            }
        }

        private async void HandleNetworkLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (_pendingNetworkGroup == null)
                return;

            string expectedScene = _pendingNetworkGroup.FindSceneNameByType(SceneType.ActiveScene);
            if (sceneName != expectedScene)
                return;

            var progress = new LoadingProgress();
            progress.Progressed += value =>
            {
                float localRange = Mathf.Lerp(0.8f, 1f, value);
                _targetProgress = Mathf.Max(_targetProgress, localRange);
            };

            await manager.LoadLocalScenesOnly(_pendingNetworkGroup, progress);

            _targetProgress = 1f;
            _networkSceneLoadedTcs?.TrySetResult(true);
        }

        private void RegisterNetworkCallbacksIfPossible()
        {
            if (_networkCallbacksRegistered) return;
            if (NetworkManager.Singleton == null) return;

            if (NetworkManager.Singleton.SceneManager == null)
            {
                Debug.LogWarning("[SceneLoader] NetworkSceneManager ainda não está pronto.");
                return;
            }

            NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleNetworkSceneEvent;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleNetworkLoadEventCompleted;
            _networkCallbacksRegistered = true;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (!_networkCallbacksRegistered) return;
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleNetworkSceneEvent;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleNetworkLoadEventCompleted;
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
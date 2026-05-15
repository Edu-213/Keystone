using Assets._Keystone.Runtime.Scripts.Networking;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.SceneManagement
{
    public class SceneManagementBootstrapper : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private SteamNetcodeBridge _steamBridge;
        [SerializeField] private SceneSyncController _sceneSyncController;

        public SceneLoader SceneLoader => _sceneLoader;
        public SteamNetcodeBridge SteamBridge => _steamBridge;
        public SceneSyncController SceneSyncController => _sceneSyncController;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (_sceneLoader == null) Debug.LogError("SceneLoader não atribuído.");
            if (_steamBridge == null) Debug.LogError("SteamNetcodeBridge não atribuído.");
            if (_sceneSyncController == null) Debug.LogError("SceneSyncController não atribuído.");

            _steamBridge.Initialize(this);
            _sceneSyncController.Initialize(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Debug.Log("Bootstrapper...");
        }
    }
}
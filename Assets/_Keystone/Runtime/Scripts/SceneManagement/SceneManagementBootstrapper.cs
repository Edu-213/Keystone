using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.SceneManagement
{
    public class SceneManagementBootstrapper : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private SceneLoader _sceneLoader;
        public SceneLoader SceneLoader => _sceneLoader;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Debug.Log("Bootstrapper...");
        }
    }
}
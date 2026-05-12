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
            // Apenas uma linha no objeto pai protege todos os 20 sistemas
            DontDestroyOnLoad(gameObject);

            Debug.Log("Sistemas Globais inicializados e protegidos.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Debug.Log("Bootstrapper...");
        }
    }
}
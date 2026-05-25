using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keystone.Multiplayer.SceneManagement.Exceptions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Keystone.Multiplayer.SceneManagement
{
    public class SceneGroupManager
    {
        public event Action<string> OnSceneLoaded = delegate { };
        public event Action<string> OnSceneUnloaded = delegate { };
        public event Action OnSceneGroupLoaded = delegate { };

        public SceneGroup ActiveSceneGroup { get; private set; }

        public async Task LoadScenes(SceneGroup group, Func<SceneData, bool> sceneFilter = null, IProgress<float> progress = null, bool reloadDupScenes = false, bool unloadExisting = true)
        {
            if (group == null)
                throw new InvalidSceneGroupException("SceneGroup é nulo em LoadScenes.");

            ActiveSceneGroup = group;

            if (unloadExisting) await UnloadScenes();

            var loadedScenes = GetCurrentlyLoadedScenePaths();
            sceneFilter ??= _ => true;

            var scenesToLoad = group.Scenes.Where(scene => sceneFilter(scene) && (reloadDupScenes || !loadedScenes.Contains(scene.Name))).ToList();
            var operationGroup = new AsyncOperationGroup(scenesToLoad.Count);

            foreach (var sceneData in scenesToLoad)
            {
                if (string.IsNullOrWhiteSpace(sceneData.Path))
                    throw new InvalidSceneGroupException($"Cena '{sceneData.Name}' está com Path inválido.");

                var operation = SceneManager.LoadSceneAsync(sceneData.Path, LoadSceneMode.Additive) ?? throw new InvalidOperationException($"Falha ao iniciar LoadSceneAsync para '{sceneData.Name}'.");

                operationGroup.Operations.Add(operation);
                OnSceneLoaded.Invoke(sceneData.Name);
            }

            while (!operationGroup.IsDone)
            {
                progress?.Report(operationGroup.Progress);
                await Task.Yield();
            }

            SetUnityActiveScene(group.GetActiveSceneName());
            OnSceneGroupLoaded.Invoke();
        }

        public async Task UnloadScenes()
        {
            var scenes = new List<string>();
            int sceneCount = SceneManager.sceneCount;

            for (var i = sceneCount - 1; i >= 0; i--)
            {
                var sceneAt = SceneManager.GetSceneAt(i);
                if (!sceneAt.isLoaded) continue;

                scenes.Add(sceneAt.name);
            }

            var operationGroup = new AsyncOperationGroup(scenes.Count);

            foreach (var scene in scenes)
            {
                var operation = SceneManager.UnloadSceneAsync(scene);
                if (operation == null) continue;

                operationGroup.Operations.Add(operation);
                OnSceneUnloaded.Invoke(scene);
            }

            while (!operationGroup.IsDone)
            {
                await Task.Yield();
            }
        }

        public async Task LoadAdditiveScene(string scenePath, Action onComplete = null)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
                throw new InvalidSceneGroupException("scenePath é nulo ou vazio em LoadAdditiveScene.");

            if (SceneManager.GetSceneByPath(scenePath).isLoaded)
            {
                onComplete?.Invoke();
                return;
            }

            var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive) ?? throw new InvalidOperationException($"Falha ao iniciar LoadSceneAsync para '{scenePath}'.");
            while (!operation.isDone)
            {
                await Task.Delay(100);
            }

            OnSceneLoaded.Invoke(scenePath);
            onComplete?.Invoke();
        }

        public async Task UnloadScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
                throw new InvalidSceneGroupException("scenePath é nulo ou vazio em UnloadScene.");

            if (!SceneManager.GetSceneByPath(scenePath).isLoaded) return;

            var operation = SceneManager.UnloadSceneAsync(scenePath) ?? throw new InvalidOperationException($"Falha ao iniciar UnloadSceneAsync para '{scenePath}'.");
            while (!operation.isDone)
            {
                await Task.Delay(100);
            }

            OnSceneUnloaded.Invoke(scenePath);
        }

        private HashSet<string> GetCurrentlyLoadedScenePaths()
        {
            var loadedScenes = new HashSet<string>();

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    loadedScenes.Add(scene.path);
            }

            return loadedScenes;
        }

        private void SetUnityActiveScene(string activeSceneName)
        {
            if (string.IsNullOrEmpty(activeSceneName)) return;

            var activeScene = SceneManager.GetSceneByName(activeSceneName);
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                SceneManager.SetActiveScene(activeScene);
            }
        }
    }

    public readonly struct AsyncOperationGroup
    {
        public readonly List<AsyncOperation> Operations;
        public float Progress
        {
            get
            {
                if (Operations.Count == 0) return 1f;
                float total = 0;
                for (int i = 0; i < Operations.Count; i++)
                {
                    total += Operations[i].progress;
                }

                return total / Operations.Count;
            }
        }
        public bool IsDone => Operations.Count == 0 || Operations.All(o => o.isDone);

        public AsyncOperationGroup(int initialCapacity)
        {
            Operations = new List<AsyncOperation>(initialCapacity);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets._Keystone.Runtime.Scripts.SceneManagement
{
    public class SceneGroupManager
    {
        public event Action<string> OnSceneLoaded = delegate { };
        public event Action<string> OnSceneUnloaded = delegate { };
        public event Action OnSceneGroupLoaded = delegate { };

        public SceneGroup ActiveSceneGroup { get; private set; }

        public async Task LoadScenes(SceneGroup group, Func<SceneData, bool> sceneFilter = null, IProgress<float> progress = null, bool reloadDupScenes = false, bool unloadExisting = true)
        {
            ActiveSceneGroup = group;

            if (unloadExisting) await UnloadScenes();

            var loadedScenes = GetCurrentlyLoadedSceneNames();
            sceneFilter ??= _ => true;

            var scenesToLoad = group.Scenes.Where(scene => sceneFilter(scene) && (reloadDupScenes || !loadedScenes.Contains(scene.Name))).ToList();
            var operationGroup = new AsyncOperationGroup(scenesToLoad.Count);

            foreach (var sceneData in scenesToLoad)
            {
                var operation = SceneManager.LoadSceneAsync(sceneData.Path, LoadSceneMode.Additive);
                if (operation == null) continue;

                operationGroup.Operations.Add(operation);
                OnSceneLoaded.Invoke(sceneData.Name);
            }

            while (!operationGroup.IsDone)
            {
                progress?.Report(operationGroup.Progress);
                await Task.Yield();
            }

            SetUnityActiveScene(group.FindSceneNameByType(SceneType.ActiveScene));
            OnSceneGroupLoaded.Invoke();
        }

        /*
                public async Task LoadScenes(SceneGroup group, IProgress<float> progress, bool reloadDupScenes = false)
                {
                    ActiveSceneGroup = group;
                    var loadedScenes = new List<string>();
                    await UnloadScenes();

                    int sceneCount = SceneManager.sceneCount;

                    for (var i = 0; i < sceneCount; i++)
                    {
                        loadedScenes.Add(SceneManager.GetSceneAt(i).name);
                    }

                    var totalScenesToLoad = ActiveSceneGroup.Scenes.Count;
                    var operationGroup = new AsyncOperationGroup(totalScenesToLoad);

                    for (var i = 0; i < totalScenesToLoad; i++)
                    {
                        var sceneData = group.Scenes[i];
                        //if (reloadDupScenes == false && loadedScenes.Contains(sceneData.Name)) continue;
                        if (sceneData.SceneType == SceneType.ActiveScene || reloadDupScenes || !loadedScenes.Contains(sceneData.Name))
                        {
                            var operation = SceneManager.LoadSceneAsync(sceneData.reference.Path, LoadSceneMode.Additive);
                            operationGroup.Operations.Add(operation);
                            OnSceneLoaded.Invoke(sceneData.Name);
                        }
                        //var operation = SceneManager.LoadSceneAsync(sceneData.reference.Path, LoadSceneMode.Additive);
                        //operationGroup.Operations.Add(operation);
                        // OnSceneLoaded.Invoke(sceneData.Name);
                    }

                    while (!operationGroup.IsDone)
                    {
                        progress?.Report(operationGroup.Progress);
                        await Task.Delay(100);
                    }

                    Scene activeScene = SceneManager.GetSceneByName(ActiveSceneGroup.FindSceneNameByType(SceneType.ActiveScene));

                    if (activeScene.IsValid())
                    {
                        SceneManager.SetActiveScene(activeScene);
                    }

                    OnSceneGroupLoaded.Invoke();
                }
                */

        public async Task UnloadScenes()
        {
            var scenes = new List<string>();

            int sceneCount = SceneManager.sceneCount;

            for (var i = sceneCount - 1; i >= 0; i--)
            {
                var sceneAt = SceneManager.GetSceneAt(i);
                if (!sceneAt.isLoaded) continue;
                if (sceneAt.name == "Bootstrapper") continue;

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

        public async Task LoadAdditiveScene(string sceneName, Action onComplete = null)
        {
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                onComplete?.Invoke();
                return;
            }

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!operation.isDone)
            {
                await Task.Delay(100);
            }

            OnSceneLoaded.Invoke(sceneName);
            onComplete?.Invoke();
        }
        //await Bootstrapper.Instance.SceneLoader.manager.LoadAdditiveScene("teste"); para carregar uma cena de forma aditiva

        //UnLoad Additive Scene type exemplo Character Customization
        public async Task UnloadScene(string sceneName)
        {
            if (!SceneManager.GetSceneByName(sceneName).isLoaded) return;

            var operation = SceneManager.UnloadSceneAsync(sceneName);
            while (!operation.isDone)
            {
                await Task.Delay(100);
            }

            OnSceneUnloaded.Invoke(sceneName);
        }

        private HashSet<string> GetCurrentlyLoadedSceneNames()
        {
            var loadedScenes = new HashSet<string>();

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    loadedScenes.Add(scene.name);
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
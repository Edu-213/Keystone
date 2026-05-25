using System;
using System.Collections.Generic;
using Eflatun.SceneReference;
using UnityEngine;

namespace Keystone.Multiplayer.SceneManagement
{
    [CreateAssetMenu(fileName = "SceneGroup", menuName = "Keystone/Scene Management/Scene Group")]
    public class SceneGroup : ScriptableObject
    {
        public string GroupName;
        public List<SceneData> Scenes;

        private Dictionary<SceneType, string> _sceneCache;
        public void OnEnable() => _sceneCache = null;

        private void PopulateCache()
        {
            _sceneCache = new Dictionary<SceneType, string>();
            foreach (var scene in Scenes)
            {
                if (!_sceneCache.ContainsKey(scene.SceneType))
                {
                    _sceneCache.Add(scene.SceneType, scene.reference.Name);
                }
            }
        }

        public string FindSceneNameByType(SceneType sceneType)
        {
            if (_sceneCache == null) PopulateCache();

            return _sceneCache.TryGetValue(sceneType, out var sceneName) ? sceneName : null;
        }

        public string GetActiveSceneName() => FindSceneNameByType(SceneType.ActiveScene);
    }

    [Serializable]
    public class SceneData
    {
        public SceneReference reference;
        public string Name => reference.Name;
        public string Path => reference.Path;
        public SceneType SceneType;
    }

    public enum SceneType
    {
        ActiveScene,
        MainMenu,
        UserInterface,
        HUD,
        Cinematic,
        Environment,
        Tooling
    }
}
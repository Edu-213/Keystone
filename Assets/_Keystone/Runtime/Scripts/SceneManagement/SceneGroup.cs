using System;
using System.Collections.Generic;
using System.Linq;
using Eflatun.SceneReference;

namespace Assets._Keystone.Runtime.Scripts.SceneManagement
{
    [Serializable]
    public class SceneGroup
    {
        public string GroupName = "New Scene Group";
        public List<SceneData> Scenes;
        private string _activeSceneName;

        public void InitializeCache()
        {
            _activeSceneName = Scenes.FirstOrDefault(s => s.SceneType == SceneType.ActiveScene)?.reference.Name;
        }

        public string FindSceneNameByType(SceneType sceneType)
        {
            if (_activeSceneName == null && sceneType == SceneType.ActiveScene)
                InitializeCache();

            return sceneType == SceneType.ActiveScene ? _activeSceneName : Scenes.FirstOrDefault(s => s.SceneType == sceneType)?.reference.Name;
        }
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
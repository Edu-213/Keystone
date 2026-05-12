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

        public string FindSceneNameByType(SceneType sceneType)
        {
            return Scenes.FirstOrDefault(scene => scene.SceneType == sceneType)?.reference.Name;
        }

        public List<SceneData> GetLocalScenes()
        {
            return Scenes.Where(s => s.SceneType != SceneType.ActiveScene).ToList();
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
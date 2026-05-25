using System;

namespace Keystone.Multiplayer.SceneManagement.Exceptions
{
    public sealed class InvalidSceneGroupException : Exception
    {
        public InvalidSceneGroupException(string message) : base(message) { }
    }

    public sealed class SceneLoadTimeoutException : TimeoutException
    {
        public SceneLoadTimeoutException(string message) : base(message) { }
    }

    public sealed class NetworkSceneLoadException : Exception
    {
        public NetworkSceneLoadException(string message) : base(message) { }
    }
}
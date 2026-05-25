using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Keystone.Multiplayer.SceneManagement.Extensions
{
    public static class UnityTaskRunner
    {
        public static async void RunSafe(Task task, Action<Exception> onException = null)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
                Debug.LogException(ex);
            }
        }
    }
}
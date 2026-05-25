using System;
using System.Threading.Tasks;
using Keystone.Multiplayer.SceneManagement.Exceptions;

namespace Keystone.Multiplayer.SceneManagement.Extensions
{
    public static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, int millisecondsTimeout, string errorMessage)
        {
            if (millisecondsTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));

            var timeoutTask = Task.Delay(millisecondsTimeout);
            var completedTask = await Task.WhenAny(task, timeoutTask);

            if (completedTask == timeoutTask)
                throw new SceneLoadTimeoutException(errorMessage);

            await task;
        }

        public static async Task<T> TimeoutAfter<T>(this Task<T> task, int millisecondsTimeout, string errorMessage)
        {
            if (millisecondsTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));

            var timeoutTask = Task.Delay(millisecondsTimeout);
            var completedTask = await Task.WhenAny(task, timeoutTask);

            if (completedTask == timeoutTask)
                throw new SceneLoadTimeoutException(errorMessage);

            return await task;
        }
    }
}
using System;
using System.Collections.Generic;

namespace Assets._Keystone.Runtime.Scripts.Events
{
    public static class NetworkEvents
    {
        public static event Action OnHostGameplayReady;
        public static event Action<ulong> OnClientGameplayReady;
        public static event Action<ulong> OnPlayerSpawnRequested;
        public static event Action<ulong> OnPlayerSpawned;

        public static void RaiseHostGameplayReady()
        {
            OnHostGameplayReady?.Invoke();
        }

        public static void RaiseClientGameplayReady(ulong clientId)
        {
            OnClientGameplayReady?.Invoke(clientId);
        }

        public static void RaisePlayerSpawnRequested(ulong clientId)
        {
            OnPlayerSpawnRequested?.Invoke(clientId);
        }

        public static void RaisePlayerSpawned(ulong clientId)
        {
            OnPlayerSpawned?.Invoke(clientId);
        }
    }
}
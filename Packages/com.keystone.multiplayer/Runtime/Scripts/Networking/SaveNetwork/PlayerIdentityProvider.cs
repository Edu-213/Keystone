using Steamworks;
using UnityEngine;

namespace Keystone.Multiplayer.Networking.SaveNetwork
{
    public class PlayerIdentityProvider : MonoBehaviour, IPlayerIdentityProvider
    {
        public string GetPersistentPlayerId(ulong ownerClientId)
        {
            if (!SteamClient.IsValid)
            {
                Debug.LogWarning("SteamClient não está válido. Usando fallback temporário.");
                return $"fallback_{ownerClientId}";
            }

            return SteamClient.SteamId.ToString();
        }
    }
}
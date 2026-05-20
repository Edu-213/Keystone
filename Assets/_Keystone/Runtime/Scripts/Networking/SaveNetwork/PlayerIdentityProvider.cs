using Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Core;
using Steamworks;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork
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
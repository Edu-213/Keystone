using Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Core;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork
{
    public class NetcodePlayerIdentityProvider : MonoBehaviour, INetworkPlayerIdentityProvider
    {
        [SerializeField] private string idPrefix = "Player";

        public string GetPersistentPlayerId(ulong ownerClientId)
        {
            return $"{idPrefix}_{ownerClientId}";
        }
    }
}
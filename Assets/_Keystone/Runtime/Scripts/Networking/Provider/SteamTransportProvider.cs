using Assets._Keystone.Runtime.Scripts.Networking.Interface;
using Netcode.Transports.Facepunch;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking.Provider
{
    public class SteamTransportProvider : MonoBehaviour, INetworkTransportProvider
    {
        [SerializeField] private FacepunchTransport _transport;

        public void ConfigureAsHost() { }

        public void ConfigureAsClient(ulong targetId)
        {
            _transport.targetSteamId = targetId;
        }
    }
}
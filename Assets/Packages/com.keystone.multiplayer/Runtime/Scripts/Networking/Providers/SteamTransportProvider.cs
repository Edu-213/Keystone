using Netcode.Transports.Facepunch;
using UnityEngine;

namespace Keystone.Multiplayer.Networking.Providers
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
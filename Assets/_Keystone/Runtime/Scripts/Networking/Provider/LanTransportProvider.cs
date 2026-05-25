using Assets._Keystone.Runtime.Scripts.Networking.Interface;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking.Provider
{
    public class LanTransportProvider : MonoBehaviour, INetworkTransportProvider
    {
        [SerializeField] private UnityTransport _transport;
        [SerializeField] private string _hostAddress = "127.0.0.1";
        [SerializeField] private ushort _port = 7777;

        public void ConfigureAsHost()
        {
            _transport.SetConnectionData("0.0.0.0", _port);
        }

        public void ConfigureAsClient(ulong targetId)
        {
            _transport.SetConnectionData(_hostAddress, _port);
        }
    }
}
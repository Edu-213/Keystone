using System.Collections.Generic;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork
{
    public class NetworkSavePlayerRegistry
    {
        private readonly Dictionary<ulong, string> _clientToPlayerId = new();

        public void Register(ulong clientId, string playerId)
        {
            _clientToPlayerId[clientId] = playerId;
        }

        public bool TryGetPlayerId(ulong clientId, out string playerId)
        {
            return _clientToPlayerId.TryGetValue(clientId, out playerId);
        }

        public void Clear()
        {
            _clientToPlayerId.Clear();
        }
    }
}
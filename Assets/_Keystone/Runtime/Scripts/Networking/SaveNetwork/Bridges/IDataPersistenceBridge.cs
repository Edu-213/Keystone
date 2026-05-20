using System.Collections.Generic;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Bridges
{
    public interface IDataPersistenceBridge
    {
        void RegisterPlayer(ulong clientId, string playerId);
        void UpdatePlayerBlock(string playerId, string saveKey, string jsonBlock);
        string GetPlayerId(ulong clientId);
        IReadOnlyDictionary<string, string> GetBufferedBlocks(string playerId);
        string SelectedProfileId { get; }
    }
}
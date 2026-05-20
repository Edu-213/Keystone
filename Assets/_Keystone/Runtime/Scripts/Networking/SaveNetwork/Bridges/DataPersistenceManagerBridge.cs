using System.Collections.Generic;
using Assets._Keystone.Runtime.Scripts.DataPersistence;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Bridges
{
    public class DataPersistenceManagerBridge : IDataPersistenceBridge
    {
        private DataPersistenceManager Manager => DataPersistenceManager.Instance;

        public void RegisterPlayer(ulong clientId, string playerId)
        {
            Manager.RegisterPlayerId(clientId, playerId);
        }

        public void UpdatePlayerBlock(string playerId, string saveKey, string jsonBlock)
        {
            Manager.UpdateTemporaryPlayerBlock(playerId, saveKey, jsonBlock);
        }

        public string GetPlayerId(ulong clientId)
        {
            return Manager.GetPlayerId(clientId);
        }

        public IReadOnlyDictionary<string, string> GetBufferedBlocks(string playerId)
        {
            return Manager.GetBufferedBlocks(playerId);
        }

        public string SelectedProfileId => Manager.SelectedProfileId;
    }
}
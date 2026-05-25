using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace Keystone.Multiplayer.DataPersistence
{
    [Serializable]
    public class SaveFile
    {
        public int version = 1;
        public long lastUpdate;
        public Dictionary<string, string> globalBlocks = new();
        public Dictionary<string, PlayerBlockCollection> players = new();
    }

    [Serializable]
    public class PlayerBlockCollection
    {
        public Dictionary<string, string> blocks = new();

        [NonSerialized]
        private ReadOnlyDictionary<string, string> _readOnlyBlocks;
        [JsonIgnore]
        public IReadOnlyDictionary<string, string> ReadOnlyBlocks => _readOnlyBlocks ??= new ReadOnlyDictionary<string, string>(blocks);
    }

    public enum SaveScope
    {
        Global,
        Player
    }

    public readonly struct SaveContext
    {
        public readonly string ProfileId;
        public readonly string PlayerGuid;
        public readonly bool IsServer;

        public SaveContext(string profileId, string playerGuid, bool isServer)
        {
            ProfileId = profileId;
            PlayerGuid = playerGuid;
            IsServer = isServer;
        }
    }
}
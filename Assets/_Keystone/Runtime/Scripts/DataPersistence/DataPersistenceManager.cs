using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Assets._Keystone.Runtime.Scripts.DataPersistence.Data;
using Assets._Keystone.Runtime.Scripts.DataPersistence.EncryptSystem;

namespace Assets._Keystone.Runtime.Scripts.DataPersistence
{
    public class DataPersistenceManager : MonoBehaviour
    {
        [Header("Debugging")]
        [SerializeField] private bool disableDataPersistence = false;

        [Header("File Storage Config")]
        [SerializeField] private string fileName = "save.json";
        [SerializeField] private bool useEncryption = true;
        [SerializeField] private bool useCompression = false;

        public static DataPersistenceManager Instance { get; private set; }

        private FileDataHandler dataHandler;
        private SaveFile currentSave;
        private string selectedProfileId = "";
        private List<ISaveModule> modules = new();

        private readonly Dictionary<ulong, string> clientIdToGuid = new();
        private readonly Dictionary<string, PlayerBlockCollection> temporaryPlayerBlocks = new();
        private static readonly IReadOnlyDictionary<string, string> EmptyBlocks = new Dictionary<string, string>();

        public SaveFile CurrentSave => currentSave;
        public string SelectedProfileId => selectedProfileId;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (disableDataPersistence)
                Debug.LogWarning("Data Persistence is disabled.");

            IEncryptor encryptor = new AESEncryptor("GuMW4SBVaLVcKaEUs/6E/D+a5m/6dJlljC+PPw6VRtQ=");

            dataHandler = new FileDataHandler(
                Application.persistentDataPath,
                fileName,
                encryptor,
                useEncryption,
                useCompression
            );
        }

        public void RefreshModules()
        {
            modules = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<ISaveModule>()
                .ToList();
        }

        public void ChangeSelectedProfile(string profileId)
        {
            selectedProfileId = profileId;
            currentSave = null;
            temporaryPlayerBlocks.Clear();
            clientIdToGuid.Clear();
        }

        public void RegisterPlayerGuid(ulong clientId, string playerGuid)
        {
            clientIdToGuid[clientId] = playerGuid;
        }

        public string GetPlayerGuid(ulong clientId)
        {
            return clientIdToGuid.TryGetValue(clientId, out var guid) ? guid : null;
        }

        public void NewGame(string profileId)
        {
            selectedProfileId = profileId;
            currentSave = new SaveFile
            {
                version = 1,
                lastUpdate = DateTime.UtcNow.ToBinary()
            };

            temporaryPlayerBlocks.Clear();
        }

        public void SaveGame()
        {
            if (disableDataPersistence) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (string.IsNullOrWhiteSpace(selectedProfileId)) return;

            RefreshModules();

            currentSave ??= new SaveFile();
            currentSave.lastUpdate = DateTime.UtcNow.ToBinary();

            currentSave.globalBlocks.Clear();

            foreach (var module in modules.Where(m => m.Scope == SaveScope.Global))
            {
                var context = new SaveContext(selectedProfileId, null, true);

                if (!module.CanSave(context))
                    continue;

                currentSave.globalBlocks[module.SaveKey] = module.CaptureAsJson(context);
            }

            currentSave.players.Clear();

            foreach (var kvp in temporaryPlayerBlocks)
            {
                string playerGuid = kvp.Key;
                PlayerBlockCollection tempBlocks = kvp.Value;

                if (!currentSave.players.TryGetValue(playerGuid, out var savePlayerBlocks))
                {
                    savePlayerBlocks = new PlayerBlockCollection();
                    currentSave.players[playerGuid] = savePlayerBlocks;
                }

                savePlayerBlocks.blocks.Clear();

                foreach (var block in tempBlocks.blocks)
                {
                    savePlayerBlocks.blocks[block.Key] = block.Value;
                }
            }

            dataHandler.Save(currentSave, selectedProfileId);
        }

        public void LoadGame()
        {
            if (disableDataPersistence) return;
            if (string.IsNullOrWhiteSpace(selectedProfileId)) return;

            RefreshModules();

            currentSave = dataHandler.Load(selectedProfileId);
            if (currentSave == null) return;

            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            foreach (var module in modules.Where(m => m.Scope == SaveScope.Global))
            {
                var context = new SaveContext(selectedProfileId, null, isServer);

                if (!module.CanLoad(context))
                    continue;

                if (currentSave.globalBlocks.TryGetValue(module.SaveKey, out var json))
                {
                    module.RestoreFromJson(json, context);
                }
            }

            temporaryPlayerBlocks.Clear();
            foreach (var playerKvp in currentSave.players)
            {
                foreach (var blockKvp in playerKvp.Value.blocks)
                {
                    UpdateTemporaryPlayerBlock(playerKvp.Key, blockKvp.Key, blockKvp.Value);
                }
            }
        }

        public void LoadPlayer(string playerGuid)
        {
            if (currentSave == null) return;
            if (!currentSave.players.TryGetValue(playerGuid, out var playerBlocks)) return;

            RefreshModules();

            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            foreach (var module in modules.Where(m => m.Scope == SaveScope.Player))
            {
                var context = new SaveContext(selectedProfileId, playerGuid, isServer);

                if (!module.CanLoad(context))
                    continue;

                if (playerBlocks.blocks.TryGetValue(module.SaveKey, out var json))
                {
                    module.RestoreFromJson(json, context);
                }
            }
        }

        public void UpdateTemporaryPlayerBlock(string playerGuid, string saveKey, string jsonBlock)
        {
            if (!temporaryPlayerBlocks.TryGetValue(playerGuid, out var collection))
            {
                collection = new PlayerBlockCollection();
                temporaryPlayerBlocks[playerGuid] = collection;
            }
            collection.blocks[saveKey] = jsonBlock;
        }

        public bool TryGetPlayerBlock(string playerGuid, string saveKey, out string json)
        {
            json = null;

            if (currentSave == null) return false;
            if (!currentSave.players.TryGetValue(playerGuid, out var playerBlocks)) return false;

            return playerBlocks.blocks.TryGetValue(saveKey, out json);
        }

        public IReadOnlyDictionary<string, string> GetBufferedBlocks(string playerGuid)
        {
            if (temporaryPlayerBlocks.TryGetValue(playerGuid, out var collection))
            {
                return collection.ReadOnlyBlocks;
            }
            return EmptyBlocks;
        }
    }
}
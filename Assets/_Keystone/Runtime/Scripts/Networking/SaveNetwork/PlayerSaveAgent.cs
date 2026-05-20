using System.Linq;
using Assets._Keystone.Runtime.Scripts.DataPersistence.Data;
using Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Bridges;
using Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Core;
using Unity.Netcode;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork
{
    [DisallowMultipleComponent]
    public class PlayerSaveAgent : NetworkBehaviour
    {
        private string _playerId;
        private ISaveModule[] _modules;
        private bool _initialized;

        private IPlayerIdentityProvider _identityProvider;
        private ISaveModuleLocator _moduleLocator;
        private IDataPersistenceBridge _bridge;

        private void Awake()
        {
            _identityProvider = GetComponent<IPlayerIdentityProvider>();
            _moduleLocator = GetComponent<ISaveModuleLocator>();

            if (_identityProvider == null)
                _identityProvider = GetComponentInChildren<IPlayerIdentityProvider>();

            if (_moduleLocator == null)
                _moduleLocator = GetComponentInChildren<ISaveModuleLocator>();

            _bridge = new DataPersistenceManagerBridge();
        }

        public override void OnNetworkSpawn()
        {
            if (_initialized)
                return;

            Initialize();
        }

        private void Initialize()
        {
            if (!IsOwner)
                return;

            if (_identityProvider == null)
            {
                Debug.LogError("[NetworkPlayerSaveAgent] Missing INetworkPlayerIdentityProvider.");
                return;
            }

            _modules = _moduleLocator != null ? _moduleLocator.GetModules() : GetComponentsInChildren<ISaveModule>(true);

            _playerId = _identityProvider.GetPersistentPlayerId(OwnerClientId);
            _initialized = true;

            Debug.Log($"[NetworkPlayerSaveAgent] Initialized with PlayerId={_playerId}, OwnerClientId={OwnerClientId}, Modules={_modules.Length}");

            RegisterPlayerRpc(_playerId);
            RequestLoadRpc(_playerId);
        }

        public void PushLocalModulesToServer()
        {
            if (!_initialized)
            {
                Debug.LogWarning("[NetworkPlayerSaveAgent] Not initialized yet.");
                return;
            }

            if (!IsOwner)
            {
                Debug.LogWarning("[NetworkPlayerSaveAgent] Apenas o owner pode enviar módulos locais.");
                return;
            }

            var context = new SaveContext(_bridge.SelectedProfileId, _playerId, false);

            foreach (var module in _modules)
            {
                if (!module.CanSave(context))
                    continue;

                string jsonBlock = module.CaptureAsJson(context);
                UpdateBufferRpc(module.SaveKey, jsonBlock);
            }
        }

        [Rpc(SendTo.Server)]
        private void RegisterPlayerRpc(string playerId, RpcParams rpcParams = default)
        {
            _bridge.RegisterPlayer(rpcParams.Receive.SenderClientId, playerId);
        }

        [Rpc(SendTo.Server)]
        private void UpdateBufferRpc(string saveKey, string jsonBlock, RpcParams rpcParams = default)
        {
            var guid = _bridge.GetPlayerId(rpcParams.Receive.SenderClientId);

            if (guid == null)
            {
                Debug.LogWarning($"[NetworkPlayerSaveAgent] GUID não encontrado para client {rpcParams.Receive.SenderClientId}");
                return;
            }

            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning($"[NetworkPlayerSaveAgent] Client {rpcParams.Receive.SenderClientId} tentou atualizar buffer de objeto owned por {OwnerClientId}");
                return;
            }

            _bridge.UpdatePlayerBlock(guid, saveKey, jsonBlock);
        }

        [Rpc(SendTo.Server)]
        private void RequestLoadRpc(string playerId, RpcParams rpcParams = default)
        {
            var blocks = _bridge.GetBufferedBlocks(playerId);
            if (blocks == null || blocks.Count == 0)
                return;

            var targetClientId = rpcParams.Receive.SenderClientId;

            foreach (var block in blocks)
            {
                ApplyLoadRpc(block.Key, block.Value, playerId, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ApplyLoadRpc(string saveKey, string jsonBlock, string playerId, RpcParams rpcParams = default)
        {
            if (!_initialized)
                return;

            var context = new SaveContext(_bridge.SelectedProfileId, playerId, false);

            foreach (var module in _modules.Where(m => m.SaveKey == saveKey))
            {
                if (!module.CanLoad(context))
                    continue;

                module.RestoreFromJson(jsonBlock, context);
            }
        }
    }
}
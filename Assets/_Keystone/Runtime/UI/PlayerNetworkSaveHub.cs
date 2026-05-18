using Unity.Netcode;
using UnityEngine;
using System.Linq;
using Assets._Keystone.Runtime.Scripts.DataPersistence;
using Assets._Keystone.Runtime.Scripts.DataPersistence.Data;

public class PlayerNetworkSaveHub : NetworkBehaviour
{
    private string _myGuid;
    private ISaveModule[] _localModules;
    private bool _isInitialized = false;

    public override void OnNetworkSpawn()
    {
        if (_isInitialized) return;
        // Se a rede ligar (Start Host/Client), pegamos o ID de rede real do Netcode
        InitializeWithNetwork();
    }

    private void InitializeWithNetwork()
    {
        _localModules = GetComponentsInChildren<ISaveModule>();

        // Em um objeto na cena que o Host acabou de ligar, usamos o ID do local client
        _myGuid = "Player_NetID_" + OwnerClientId;
        _isInitialized = true;

        Debug.Log($"<color=green>[HUB] Conectado à rede Netcode! Novo GUID definitivo: {_myGuid}</color>");

        // Se o Netcode estiver ativo, fazemos o registro padrão de rede
        if (NetworkManager.Singleton.IsClient)
        {
            RegisterGuidServerRpc(_myGuid);
            RequestLoadServerRpc(_myGuid);
        }
    }

    [ServerRpc]
    private void RegisterGuidServerRpc(string guid, ServerRpcParams rpcParams = default)
    {
        DataPersistenceManager.Instance.RegisterPlayerGuid(rpcParams.Receive.SenderClientId, guid);
    }

    // Método chamado pela sua UI/Botão para mandar o inventário/stats pro Servidor
    public void SaveLocalModulesToBuffer()
    {
        var context = new SaveContext(DataPersistenceManager.Instance.SelectedProfileId, _myGuid, false);
        
        Debug.Log($"[HUB] guid={_myGuid}, modules={_localModules?.Length ?? 0}, isOwner={IsOwner}, ownerClientId={OwnerClientId}");
        foreach (var module in _localModules)
        {
            if (module.CanSave(context))
            {
                // Captura os dados reais através da sua interface ISaveModule!
                string jsonBlock = module.CaptureAsJson(context);

                // Envia a string gerada para o buffer do servidor
                UpdateBufferServerRpc(_myGuid, module.SaveKey, jsonBlock);
            }
        }
    }

    [ServerRpc]
    private void UpdateBufferServerRpc(string guid, string saveKey, string jsonBlock)
    {
        DataPersistenceManager.Instance.UpdateTemporaryPlayerBlock(guid, saveKey, jsonBlock);
        Debug.Log($"[SERVER] Buffer atualizado para a chave '{saveKey}'.");
    }

    [ServerRpc]
    private void RequestLoadServerRpc(string guid, ServerRpcParams rpcParams = default)
    {
        var blocks = DataPersistenceManager.Instance.GetBufferedBlocks(guid);
        if (blocks == null) return;

        foreach (var block in blocks)
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } }
            };
            ApplyLoadClientRpc(block.Key, block.Value, guid, clientRpcParams);
        }
    }

    [ClientRpc]
    private void ApplyLoadClientRpc(string saveKey, string jsonBlock, string guid, ClientRpcParams rpcParams = default)
    {
        var context = new SaveContext(DataPersistenceManager.Instance.SelectedProfileId, guid, false);

        // Acha o módulo correto na nossa própria máquina e aplica o carregamento
        foreach (var module in _localModules.Where(m => m.SaveKey == saveKey))
        {
            if (module.CanLoad(context))
            {
                module.RestoreFromJson(jsonBlock, context);
            }
        }
    }
}
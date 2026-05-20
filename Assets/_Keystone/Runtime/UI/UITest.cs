using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Assets._Keystone.Runtime.Scripts.DataPersistence;
using Assets._Keystone.Runtime.Scripts.Events;
using Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork;

public class UITest : MonoBehaviour
{
    [Header("Configuração de Spawn")]
    [SerializeField] private GameObject playerPrefab;

    [SerializeField] private Button btnGanharMoeda;
    [SerializeField] private Button btnEnviarProBuffer;
    [SerializeField] private Button btnSalvarNoHD;

    void Awake()
    {
        KeystoneEvents.OnPlayerSpawnRequested += HandlePlayersReadyToSpawn;
    }

    private void Start()
    {
        DataPersistenceManager.Instance.ChangeSelectedProfile("Profile_Teste_Real");

        btnGanharMoeda.onClick.AddListener(AddCoins);

        btnEnviarProBuffer.onClick.AddListener(SendToBuffer);

        btnSalvarNoHD.onClick.AddListener(SaveToDisk);
    }

    void OnDestroy()
    {
        KeystoneEvents.OnPlayerSpawnRequested -= HandlePlayersReadyToSpawn;
    }
 
    private void HandlePlayersReadyToSpawn(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;


        SpawnPlayerForClient(clientId);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab não configurado.");
            return;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
        {
            Debug.LogWarning($"Client {clientId} não está conectado.");
            return;
        }

        if (clientData.PlayerObject != null)
        {
            Debug.LogWarning($"Client {clientId} já possui PlayerObject.");
            return;
        }

        GameObject playerInstance = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        netObj.SpawnAsPlayerObject(clientId);

        Debug.Log($"[UI] Player spawnado para client {clientId}");
    }

    private void AddCoins()
    {
        var stats = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponentInChildren<PlayerStatsModule>();
        if (stats == null)
        {
            Debug.LogWarning("PlayerStatsModule não encontrado.");
            return;
        }

        stats.coins++;
        Debug.Log($"Moedas locais do Player: {stats.coins}");
    }

    private void SendToBuffer()
    {
        KeystoneEvents.RaisePlayerSyncRequested();
    }

    private void SaveToDisk()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Somente o servidor pode salvar no HD.");
            return;
        }

        DataPersistenceManager.Instance.SaveGame();
        Debug.Log("SALVO NO HD COM SUCESSO!");
    }
}
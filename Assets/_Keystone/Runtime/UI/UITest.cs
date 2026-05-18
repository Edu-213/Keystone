using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Assets._Keystone.Runtime.Scripts.DataPersistence;

public class UITest : MonoBehaviour
{
    [Header("Configuração de Spawn")]
    [SerializeField] private GameObject playerPrefab;

    [SerializeField] private Button btnSpawnarPlayer;
    [SerializeField] private Button btnGanharMoeda;
    [SerializeField] private Button btnEnviarProBuffer;
    [SerializeField] private Button btnSalvarNoHD;

    private PlayerNetworkSaveHub _cachedPlayer;

    private void Start()
    {
        DataPersistenceManager.Instance.ChangeSelectedProfile("Profile_Teste_Real");

        btnSpawnarPlayer.onClick.AddListener(() =>
        {
            // O Spawn só pode ser comandado por quem é o Servidor/Host
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("Apenas o Host/Server pode spawnar objetos na rede! Inicie o Host primeiro.");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("Esqueceu de arrastar o Prefab do Player no componente UITest!");
                return;
            }

            // 1. Instancia o objeto normalmente no Unity
            GameObject playerInstance = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

            // 2. Pega o componente NetworkObject dele
            NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

            // 3. Spawna na rede dando a posse (Ownership) para o Host local (ID 0)
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            netObj.SpawnWithOwnership(localClientId);

            Debug.Log($"<color=lime>[UI] Player spawnado dinamicamente com sucesso para o Cliente {localClientId}!</color>");
        });

        btnGanharMoeda.onClick.AddListener(() =>
        {
            var player = FindLocalPlayerHub();
            if (player != null)
            {
                var stats = player?.GetComponentInChildren<PlayerStatsModule>();
                stats.coins++;
                Debug.Log($"Moedas locais do Player: {stats.coins}");
            }
        });

        btnEnviarProBuffer.onClick.AddListener(() =>
        {
            var player = FindLocalPlayerHub();
            player?.SaveLocalModulesToBuffer();
        });

        btnSalvarNoHD.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton.IsServer)
            {
                DataPersistenceManager.Instance.SaveGame();
                Debug.Log("SALVO NO HD COM SUCESSO!");
            }
        });
    }

    private PlayerNetworkSaveHub FindLocalPlayerHub()
    {
        if (_cachedPlayer != null) return _cachedPlayer;

        // Procura o script na hierarquia da cena ativa
        _cachedPlayer = Object.FindFirstObjectByType<PlayerNetworkSaveHub>();

        if (_cachedPlayer == null)
        {
            Debug.LogWarning("Nenhum objeto com o script 'PlayerNetworkSaveHub' foi encontrado na cena ainda!");
        }

        return _cachedPlayer;
    }
}
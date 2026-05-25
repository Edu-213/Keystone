using UnityEngine;
using Keystone.Multiplayer.DataPersistence;

public class PlayerStatsModule : MonoBehaviour, ISaveModule
{
    private void OnEnable() => DataPersistenceManager.Instance?.RegisterModule(this);
    private void OnDisable() => DataPersistenceManager.Instance?.UnregisterModule(this);

    public string SaveKey => "player_stats";
    public SaveScope Scope => SaveScope.Player;

    // A variável real do jogo que queremos salvar
    public int coins = 0;

    public bool CanSave(in SaveContext context) => true;
    public bool CanLoad(in SaveContext context) => true;

    // Transforma os dados reais do jogo em string JSON
    public string CaptureAsJson(in SaveContext context)
    {
        // Cria um objeto anônimo rápido e converte para JSON
        return JsonUtility.ToJson(new DataWrapper { savedCoins = coins });
    }

    // Pega a string JSON e devolve para as variáveis do jogo
    public void RestoreFromJson(string json, in SaveContext context)
    {
        DataWrapper data = JsonUtility.FromJson<DataWrapper>(json);
        coins = data.savedCoins;
        Debug.Log($"[ISaveModule] Dados restaurados no jogo! Moedas atuais: {coins}");
    }

    // Uma classe interna simples só para ajudar o JsonUtility a serializar o int
    [System.Serializable]
    private class DataWrapper
    {
        public int savedCoins;
    }
}
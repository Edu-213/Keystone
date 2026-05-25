using Keystone.Multiplayer.Networking;
using Keystone.Multiplayer.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Botões do Menu")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _clientButton;
    [SerializeField] private Button _singleplayerButton;
    [SerializeField] private SceneGroup _faseInicial;

    private bool IsSteam => SteamLobbyHandler.Instance != null;

    private void Start()
    {
        if (_hostButton != null)
        {
            _hostButton.onClick.RemoveAllListeners();
            _hostButton.onClick.AddListener(OnHostClicked);
        }

        if (_clientButton != null)
        {
            _clientButton.onClick.RemoveAllListeners();
            _clientButton.onClick.AddListener(OnClientClicked);
        }

        _singleplayerButton?.onClick.AddListener(OnSingleplayerClicked);
    }

    private void OnHostClicked()
    {
        if (IsSteam)
        {
            _ = SteamLobbyHandler.Instance.CreateLobbyAsync(4, "Teste");
        }
        else
        {
            SteamNetcodeBridge.Instance.StartHost(_faseInicial);
        }
    }

    private void OnClientClicked()
    {
        if (IsSteam)
        {
            //string lobbyIdText = "dasd";
            // if (ulong.TryParse(lobbyIdText, out ulong lobbyId))
            //     _ = SteamLobbyHandler.Instance.JoinLobbyByIdAsync(lobbyId);
        }
        else
        {
            Debug.Log("[DEBUG] Iniciando Client via UnityTransport (Conectando em 127.0.0.1)");
            SteamNetcodeBridge.Instance.StartClient(0);
        }
    }

    private void OnSingleplayerClicked()
    {
        SteamNetcodeBridge.Instance.StartSingleplayer(_faseInicial);
    }
}
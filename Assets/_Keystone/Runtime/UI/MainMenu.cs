using Assets._Keystone.Runtime.Scripts.Networking;
using Assets._Keystone.Runtime.Scripts.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Botões do Menu")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _clientButton;
    [SerializeField] private SceneGroup faseInicial;

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
    }

    private void OnHostClicked()
    {
        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is Netcode.Transports.Facepunch.FacepunchTransport)
        {
            _ = SteamLobbyHandler.Instance.CreateLobbyAsync(4, "Teste");
        }
        else
        {
            Debug.Log("[DEBUG] Iniciando Host via UnityTransport (Sem Steam)");
            SteamNetcodeBridge.Instance.StartHost(faseInicial);
        }
    }

    private void OnClientClicked()
    {
        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is Netcode.Transports.Facepunch.FacepunchTransport)
        {
            string lobbyIdText = "dasd";
            if (ulong.TryParse(lobbyIdText, out ulong lobbyId))
                _ = SteamLobbyHandler.Instance.JoinLobbyByIdAsync(lobbyId);
        }
        else
        {
            Debug.Log("[DEBUG] Iniciando Client via UnityTransport (Conectando em 127.0.0.1)");
            SteamNetcodeBridge.Instance.StartClient(0);
        }
    }
}
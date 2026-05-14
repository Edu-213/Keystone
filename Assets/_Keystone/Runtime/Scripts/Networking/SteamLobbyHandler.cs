using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking
{
    public class SteamLobbyHandler : MonoBehaviour
    {
        public static SteamLobbyHandler Instance { get; private set; }
        public Lobby? CurrentLobby { get; private set; }
        public bool IsLobbyOwner => CurrentLobby.HasValue && CurrentLobby.Value.Owner.Id == SteamClient.SteamId;

        private const string HostAddressKey = "HostSteamId";
        private const string LobbyNameKey = "LobbyName";

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeft;
            SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberDisconnected;
            SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
            SteamFriends.OnGameRichPresenceJoinRequested += HandleGameRichPresenceJoinRequested;
        }

        // ==========================================================
        //Functions tests

        public void OnClickLeave()
        {
            SteamLobbyHandler.Instance.LeaveCurrentLobby();
        }
        // ==========================================================

        public async Task CreateLobbyAsync(int maxPlayers = 4, string lobbyName = "Nova Sala")
        {
            if (!CanUseSteam())
                return;

            if (CurrentLobby.HasValue)
                LeaveCurrentLobby();

            _pendingLobbyName = lobbyName;
            await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        }

        public async Task<bool> JoinLobbyByIdAsync(ulong lobbyId)
        {
            if (!CanUseSteam())
                return false;

            if (CurrentLobby.HasValue && CurrentLobby.Value.Id == lobbyId)
            {
                Debug.LogWarning("[Lobby] Cliente já está nesse lobby.");
                return false;
            }

            Lobby? lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            if (!lobby.HasValue)
            {
                Debug.LogError($"[Lobby] Falha ao entrar no lobby {lobbyId}.");
                return false;
            }

            return true;
        }

        public void LeaveCurrentLobby()
        {
            if (CurrentLobby.HasValue)
            {
                Debug.Log($"[Lobby] Saindo do lobby {CurrentLobby.Value.Id}.");
                CurrentLobby.Value.Leave();
                CurrentLobby = null;
            }

            ClearRichPresence();
            SteamNetcodeBridge.Instance?.Shutdown();
        }

        private string _pendingLobbyName = "Nova Sala";

        private void OnLobbyCreated(Result res, Lobby lobby)
        {
            if (res != Result.OK)
            {
                Debug.LogError("Falha ao criar lobby");
                return;
            }

            CurrentLobby = lobby;

            lobby.SetPublic();
            lobby.SetJoinable(true);
            lobby.SetData(LobbyNameKey, _pendingLobbyName);
            lobby.SetData(HostAddressKey, SteamClient.SteamId.ToString());

            SteamFriends.SetRichPresence("connect", lobby.Id.ToString());
            SteamFriends.SetRichPresence("status", "No lobby");
            SteamFriends.SetRichPresence("steam_player_group", lobby.Id.ToString());
            SteamFriends.SetRichPresence("steam_player_group_size", lobby.MemberCount.ToString());

            bool hostStarted = SteamNetcodeBridge.Instance != null && SteamNetcodeBridge.Instance.StartHost();
            if (!hostStarted)
            {
                Debug.LogError("[Lobby] Lobby criado, mas o host do Netcode falhou ao iniciar.");
                return;
            }

            Debug.Log($"[Lobby] Lobby criado com sucesso. Id: {lobby.Id}");
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;
            Debug.Log($"[Lobby] Entrou no lobby {lobby.Id}");

            SteamFriends.SetRichPresence("connect", lobby.Id.ToString());
            SteamFriends.SetRichPresence("status", "No lobby");
            SteamFriends.SetRichPresence("steam_player_group", lobby.Id.ToString());
            SteamFriends.SetRichPresence("steam_player_group_size", lobby.MemberCount.ToString());

            if (lobby.Owner.Id == SteamClient.SteamId)
                return;

            string hostSteamIdRaw = lobby.GetData(HostAddressKey);

            if (string.IsNullOrWhiteSpace(hostSteamIdRaw))
            {
                Debug.LogError("[Lobby] HostSteamId não encontrado nos dados do lobby.");
                return;
            }

            if (!ulong.TryParse(hostSteamIdRaw, out ulong hostSteamIdValue))
            {
                Debug.LogError($"[Lobby] HostSteamId inválido: {hostSteamIdRaw}");
                return;
            }

            SteamNetcodeBridge.Instance?.StartClient(hostSteamIdValue);
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            if (!CurrentLobby.HasValue || lobby.Id != CurrentLobby.Value.Id)
                return;

            Debug.Log($"[Lobby] {friend.Name} entrou no lobby.");
            UpdateRichPresenceGroupSize(lobby);
        }

        private void OnLobbyMemberLeft(Lobby lobby, Friend friend)
        {
            if (!CurrentLobby.HasValue || lobby.Id != CurrentLobby.Value.Id)
                return;

            Debug.Log($"[Lobby] {friend.Name} saiu do lobby.");
            UpdateRichPresenceGroupSize(lobby);
        }

        private void OnLobbyMemberDisconnected(Lobby lobby, Friend friend)
        {
            if (!CurrentLobby.HasValue || lobby.Id != CurrentLobby.Value.Id)
                return;

            Debug.Log($"[Lobby] {friend.Name} desconectou.");
            UpdateRichPresenceGroupSize(lobby);
        }

        private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId id)
        {
            _ = OnGameLobbyJoinRequested(lobby, id);
        }

        private async Task OnGameLobbyJoinRequested(Lobby lobby, SteamId id)
        {
            try
            {
                await JoinLobbyByIdAsync(lobby.Id);

            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao entrar no lobby: {e}");
            }
        }

        private void HandleGameRichPresenceJoinRequested(Friend friend, string connect)
        {
            _ = OnGameRichPresenceJoinRequested(friend, connect);
        }

        private async Task OnGameRichPresenceJoinRequested(Friend friend, string connect)
        {
            try
            {
                if (!ulong.TryParse(connect, out ulong lobbyId))
                {
                    Debug.LogError($"[Lobby] Connect string inválida: {connect}");
                    return;
                }

                await JoinLobbyByIdAsync(lobbyId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] Erro ao entrar no lobby por rich presence: {e}");
            }
        }

        private void UpdateRichPresenceGroupSize(Lobby lobby)
        {
            SteamFriends.SetRichPresence("steam_player_group", lobby.Id.ToString());
            SteamFriends.SetRichPresence("steam_player_group_size", lobby.MemberCount.ToString());
        }

        private void ClearRichPresence()
        {
            SteamFriends.SetRichPresence("connect", null);
            SteamFriends.SetRichPresence("status", null);
            SteamFriends.SetRichPresence("steam_player_group", null);
            SteamFriends.SetRichPresence("steam_player_group_size", null);
        }

        private bool CanUseSteam()
        {
            if (GameNetworkManager.Instance == null || !GameNetworkManager.Instance.IsSteamInitialized)
            {
                Debug.LogError("[Lobby] Steam não está inicializado.");
                return false;
            }

            return true;
        }

        private void OnDestroy()
        {
            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeft;
            SteamMatchmaking.OnLobbyMemberDisconnected -= OnLobbyMemberDisconnected;
            SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
            SteamFriends.OnGameRichPresenceJoinRequested -= HandleGameRichPresenceJoinRequested;
        }
    }
}
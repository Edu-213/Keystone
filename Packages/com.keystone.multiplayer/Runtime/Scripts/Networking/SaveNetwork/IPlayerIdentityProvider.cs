namespace Keystone.Multiplayer.Networking.SaveNetwork
{
    public interface IPlayerIdentityProvider
    {
        string GetPersistentPlayerId(ulong ownerClientId);
    }
}
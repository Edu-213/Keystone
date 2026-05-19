namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Core
{
    public interface INetworkPlayerIdentityProvider
    {
        string GetPersistentPlayerId(ulong ownerClientId);
    }
}
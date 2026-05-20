namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Core
{
    public interface IPlayerIdentityProvider
    {
        string GetPersistentPlayerId(ulong ownerClientId);
    }
}
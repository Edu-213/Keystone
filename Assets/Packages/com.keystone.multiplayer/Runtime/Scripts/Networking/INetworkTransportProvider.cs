namespace Keystone.Multiplayer.Networking
{
    public interface INetworkTransportProvider
    {
        void ConfigureAsHost();
        void ConfigureAsClient(ulong targetId);
    }
}
namespace Assets._Keystone.Runtime.Scripts.Networking.Interface
{
    public interface INetworkTransportProvider
    {
        void ConfigureAsHost();
        void ConfigureAsClient(ulong targetId);
    }
}
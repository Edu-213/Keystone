using Keystone.Multiplayer.DataPersistence;

namespace Keystone.Multiplayer.Networking.SaveNetwork
{
    public interface ISaveModuleLocator
    {
        ISaveModule[] GetModules();
    }
}
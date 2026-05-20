using Assets._Keystone.Runtime.Scripts.DataPersistence.Data;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork.Core
{
    public interface ISaveModuleLocator
    {
        ISaveModule[] GetModules();
    }
}
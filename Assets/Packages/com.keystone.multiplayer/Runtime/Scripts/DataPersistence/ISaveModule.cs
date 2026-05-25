namespace Keystone.Multiplayer.DataPersistence
{
    public interface ISaveModule
    {
        string SaveKey { get; }
        SaveScope Scope { get; }

        bool CanSave(in SaveContext context);
        bool CanLoad(in SaveContext context);

        string CaptureAsJson(in SaveContext context);
        void RestoreFromJson(string json, in SaveContext context);
    }
}
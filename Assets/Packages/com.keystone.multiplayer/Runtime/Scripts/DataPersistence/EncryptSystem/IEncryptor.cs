namespace Keystone.Multiplayer.DataPersistence.Encryption
{
    public interface IEncryptor
    {
        string Encrypt(string data);
        string Decrypt(string data);
    }
}
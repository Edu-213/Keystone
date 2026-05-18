namespace Assets._Keystone.Runtime.Scripts.DataPersistence.EncryptSystem
{
    public interface IEncryptor
    {
        string Encrypt(string data);
        string Decrypt(string data);
    }
}
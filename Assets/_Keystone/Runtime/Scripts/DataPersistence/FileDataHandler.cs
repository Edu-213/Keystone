using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using Assets._Keystone.Runtime.Scripts.DataPersistence.EncryptSystem;
using Assets._Keystone.Runtime.Scripts.DataPersistence.Data;

namespace Assets._Keystone.Runtime.Scripts.DataPersistence
{
    public class FileDataHandler
    {
        private readonly string _dataDirPath;
        private readonly string _dataFileName;
        private readonly IEncryptor _encryptor;
        private readonly bool _useEncryption;
        private readonly bool _useCompression;
        private const string BackupExtension = ".bak";

        public FileDataHandler(string dataDirPath, string dataFileName, IEncryptor encryptor, bool useEncryption, bool useCompression)
        {
            _dataDirPath = dataDirPath;
            _dataFileName = dataFileName;
            _encryptor = encryptor;
            _useEncryption = useEncryption;
            _useCompression = useCompression;
        }

        public SaveFile Load(string profileId, bool allowRestoreFromBackup = true)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return null;

            string fullPath = GetProfilePath(profileId);
            if (!File.Exists(fullPath)) return null;

            try
            {
                string dataToLoad = ReadFile(fullPath);
                return JsonConvert.DeserializeObject<SaveFile>(dataToLoad);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load save '{profileId}' from '{fullPath}': {e}");

                if (allowRestoreFromBackup && AttemptRollback(fullPath))
                    return Load(profileId, false);
            }

            return null;
        }

        public void Save(SaveFile data, string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || data == null) return;

            string fullPath = GetProfilePath(profileId);
            string dir = Path.GetDirectoryName(fullPath);
            string tempPath = fullPath + ".tmp";
            string backupPath = fullPath + BackupExtension;

            try
            {
                if (string.IsNullOrWhiteSpace(dir))
                    throw new InvalidOperationException($"Invalid directory for save path: {fullPath}");

                Directory.CreateDirectory(dir);

                string dataToStore = JsonConvert.SerializeObject(data, Formatting.Indented);
                WriteFile(tempPath, dataToStore);

                _ = JsonConvert.DeserializeObject<SaveFile>(ReadFile(tempPath));

                if (File.Exists(fullPath))
                    File.Replace(tempPath, fullPath, backupPath);
                else
                    File.Move(tempPath, fullPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving data to file {fullPath}: {e}");
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        public void Delete(string profileId)
        {
            if (string.IsNullOrEmpty(profileId)) return;

            string profilePath = Path.Combine(_dataDirPath, profileId);
            if (Directory.Exists(profilePath))
                Directory.Delete(profilePath, true);
        }

        public Dictionary<string, SaveFile> LoadAllProfiles()
        {
            var profileDictionary = new Dictionary<string, SaveFile>();

            if (!Directory.Exists(_dataDirPath))
                return profileDictionary;

            foreach (var dir in Directory.EnumerateDirectories(_dataDirPath))
            {
                string profileId = Path.GetFileName(dir);
                SaveFile profileData = Load(profileId);

                if (profileData != null)
                    profileDictionary[profileId] = profileData;
            }

            return profileDictionary;
        }

        private bool AttemptRollback(string fullPath)
        {
            string backupFilePath = fullPath + BackupExtension;
            if (!File.Exists(backupFilePath)) return false;

            File.Copy(backupFilePath, fullPath, true);
            return true;
        }

        private string GetProfilePath(string profileId)
        {
            return Path.Combine(_dataDirPath, profileId, _dataFileName);
        }

        private string ReadFile(string path)
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            string data = _useCompression ? Decompress(fileBytes) : Encoding.UTF8.GetString(fileBytes);
            return _useEncryption ? _encryptor.Decrypt(data) : data;
        }

        private void WriteFile(string path, string data)
        {
            if (_useEncryption) data = _encryptor.Encrypt(data);
            byte[] fileBytes = _useCompression ? Compress(data) : Encoding.UTF8.GetBytes(data);
            File.WriteAllBytes(path, fileBytes);
        }

        private byte[] Compress(string input)
        {
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
            {
                writer.Write(input);
            }

            return outputStream.ToArray();
        }

        private string Decompress(byte[] compressedData)
        {
            using var inputStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
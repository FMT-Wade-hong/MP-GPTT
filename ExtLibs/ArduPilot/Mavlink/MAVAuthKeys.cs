using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using log4net;
using MissionPlanner.Utilities;

namespace MissionPlanner.Mavlink
{
    public class MAVAuthKeys
    {
        private static readonly ILog log =
    LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static string keyfile = Settings.GetUserDataDirectory() + "authkeys.xml";

        static Crypto Rij = new Crypto();
        static readonly object storageLock = new object();
        public static string LoadError { get; private set; }

        public static AuthKeys Keys = new AuthKeys();

        //https://msdn.microsoft.com/en-us/library/aa347850(v=vs.110).aspx

        [CollectionDataContract(ItemName = "AuthKeys", Namespace = "")]
        public class AuthKeys : Dictionary<string, AuthKey>
        {
        }

        [DataContract(Name = "AuthKey", Namespace = "")]
        public struct AuthKey
        {
            [DataMember()]
            public string Name;
            [DataMember()]
            public byte[] Key;
        }

        static MAVAuthKeys()
        {
            Load();
        }

        public static void AddKey(string name, string seed)
        {
            // sha the user input string
            using (SHA256CryptoServiceProvider signit = new SHA256CryptoServiceProvider())
            {
                var shauser = signit.ComputeHash(Encoding.UTF8.GetBytes(seed));
                Array.Resize(ref shauser, 32);
                
                Keys[name] = new AuthKey() {Key = shauser, Name = name};
            }
        }

        public static void Save()
        {
            lock (storageLock)
            {
                if (LoadError != null)
                    throw new IOException("原有簽署金鑰未能完整讀取，已保留原檔並停止覆寫。請恢復原網路介面或金鑰備份後重新啟動。");
                var writer = new DataContractSerializer(typeof(AuthKeys), new Type[] { typeof(AuthKey) });
                byte[] encrypted;
                using (var buffer = new MemoryStream())
                {
                    using (var cipher = new CryptoStream(buffer, Rij.algorithm.CreateEncryptor(), CryptoStreamMode.Write))
                        writer.WriteObject(cipher, Keys);
                    encrypted = buffer.ToArray();
                }
                var temporary = keyfile + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { output.Write(encrypted, 0, encrypted.Length); output.Flush(true); }
                    if (File.Exists(keyfile)) File.Replace(temporary, keyfile, null);
                    else File.Move(temporary, keyfile);
                }
                finally
                {
                    try { if (File.Exists(temporary)) File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        internal static void Load()
        {
            lock (storageLock)
            {
            LoadError = null;
            if (!File.Exists(keyfile))
                return;

            try
            {

                DataContractSerializer reader =
                    new DataContractSerializer(typeof (AuthKeys),
                        new Type[] {typeof (AuthKey)});

                byte[] encrypted = File.ReadAllBytes(keyfile);
                if (TryRead(encrypted, Rij, reader)) return;
                // Older files depend on the first network adapter. Try all currently installed
                // adapters, plus the no-adapter default, without changing the file or logging keys.
                var addresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Select(nic => nic.GetPhysicalAddress().GetAddressBytes()).Concat(new[] { new byte[0] });
                foreach (var address in addresses)
                {
                    var candidate = new Crypto(address);
                    if (TryRead(encrypted, candidate, reader))
                    { Rij.Dispose(); Rij = candidate; return; }
                    candidate.Dispose();
                }
                throw new CryptographicException("Unable to decrypt the existing signing key file.");
            }
            catch (Exception ex)
            {
                LoadError = ex.GetType().Name;
                log.Error(ex);
            }
            }
        }

        private static bool TryRead(byte[] encrypted, Crypto cipher, DataContractSerializer reader)
        {
            try
            {
                // Decrypt the complete buffer, including its final padding, before deserializing.
                var plaintext = cipher.DecryptBuffer(encrypted);
                using (var input = new MemoryStream(plaintext)) Keys = (AuthKeys)reader.ReadObject(input);
                return true;
            }
            catch (CryptographicException) { return false; }
            catch (SerializationException) { return false; }
            catch (System.Xml.XmlException) { return false; }
        }
    }
}

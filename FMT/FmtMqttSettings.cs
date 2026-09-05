using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MissionPlanner.FMT
{
    // Compatible with the standalone bridge's .fmt files. No password property.
    internal sealed class FmtMqttSettings
    {
        internal const string ExportHeader = "FMT-UAV-BRIDGE-SETTINGS/1";
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string ClientId { get; set; } = "";
        public string Username { get; set; } = "";
        public string InboundTopic { get; set; } = "";
        public string OutboundTopic { get; set; } = "";
        public string TcpAddress { get; set; } = "";
        public int TcpPort { get; set; }
        public bool UseTls { get; set; } = true;

        // Regression harnesses override this path so tests never read or delete a user's saved broker data.
        internal static string StorageDirectoryOverride { get; set; }
        internal static string StorageDirectory => string.IsNullOrWhiteSpace(StorageDirectoryOverride)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FMTPlanner", "Mqtt")
            : StorageDirectoryOverride;
        private static string RememberConsentPath => Path.Combine(StorageDirectory, "remember-settings.optin");
        private static string SettingsPath => Path.Combine(StorageDirectory, "settings.json");

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Host) || Host.Contains("://") || Host.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0)
                throw new InvalidDataException("請輸入 MQTT 主機名稱或 IP，不要包含協定或路徑。");
            if (Port < 1 || Port > 65535 || TcpPort < 1 || TcpPort > 65535)
                throw new InvalidDataException("MQTT 與 TCP Port 必須介於 1～65535。");
            ValidateString(ClientId, "Client ID");
            ValidateString(Username, "MQTT 帳號");
            ValidateString(InboundTopic, "飛控 → MP Topic");
            ValidateString(OutboundTopic, "MP → 飛控 Topic");
            if (InboundTopic.IndexOfAny(new[] { '+', '#' }) >= 0 || OutboundTopic.IndexOfAny(new[] { '+', '#' }) >= 0)
                throw new InvalidDataException("一對一橋接 Topic 不可包含 + 或 # 萬用字元。");
            if (InboundTopic == OutboundTopic)
                throw new InvalidDataException("上下行 Topic 必須不同，避免資料回送循環。");
            IPAddress address;
            if (!IPAddress.TryParse(TcpAddress, out address))
                throw new InvalidDataException("請輸入有效的本機 TCP 監聽 IP。同機 MP 可使用 127.0.0.1。");
        }

        internal static void ValidateString(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(value) > 65535)
                throw new InvalidDataException(label + " 不可為空、包含空字元或超過 MQTT 長度限制。");
        }

        internal static FmtMqttSettings LoadRemembered()
        {
            // Older builds wrote connection data on every start, even when the user did not
            // opt in to persistence. Do not surface that legacy state as product defaults.
            if (!File.Exists(RememberConsentPath) || !File.Exists(SettingsPath)) return new FmtMqttSettings();
            return JsonConvert.DeserializeObject<FmtMqttSettings>(File.ReadAllText(SettingsPath)) ?? new FmtMqttSettings();
        }

        internal void SaveRemembered(string password)
        {
            Directory.CreateDirectory(StorageDirectory);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            SavePassword(password, true);
            File.WriteAllText(RememberConsentPath, "1");
        }

        internal static void ClearRemembered()
        {
            DeleteIfPresent(RememberConsentPath);
            DeleteIfPresent(SettingsPath);
            DeleteIfPresent(SecretPath);
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        internal void Export(string path)
        {
            Validate();
            File.WriteAllText(path, ExportHeader + Environment.NewLine + JsonConvert.SerializeObject(new
            {
                Product = "FeimaoUavBridge", FormatVersion = 1, ExportedAtUtc = DateTime.UtcNow, Settings = this
            }, Formatting.Indented));
        }

        internal static FmtMqttSettings Import(string path)
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("設定檔過大。");
            using (var reader = File.OpenText(path))
            {
                if (reader.ReadLine()?.Trim() != ExportHeader) throw new InvalidDataException("不是支援的 .fmt 橋接設定檔。");
                var package = JObject.Parse(reader.ReadToEnd());
                if ((string)package["Product"] != "FeimaoUavBridge" || (int?)package["FormatVersion"] != 1)
                    throw new InvalidDataException("設定檔版本不受支援。");
                var settings = package["Settings"]?.ToObject<FmtMqttSettings>();
                if (settings == null) throw new InvalidDataException("設定檔缺少連線資料。");
                settings.Validate();
                return settings;
            }
        }

        // Separate from the standalone bridge. Bind the saved secret to host/port/user
        // so importing another broker's settings never reuses an unrelated password.
        private byte[] SecretScope => Encoding.UTF8.GetBytes(Host + "\n" + Port + "\n" + Username);
        private static string SecretPath => Path.Combine(StorageDirectory, "mqtt-password.dpapi");

        internal string LoadPassword()
        {
            if (!File.Exists(SecretPath)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(SecretPath), SecretScope, DataProtectionScope.CurrentUser)); }
            catch (CryptographicException) { return ""; }
        }

        internal void SavePassword(string password, bool remember)
        {
            if (!remember)
            {
                if (File.Exists(SecretPath)) File.Delete(SecretPath);
                return;
            }
            Directory.CreateDirectory(StorageDirectory);
            var bytes = Encoding.UTF8.GetBytes(password);
            try { File.WriteAllBytes(SecretPath, ProtectedData.Protect(bytes, SecretScope, DataProtectionScope.CurrentUser)); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
    }
}

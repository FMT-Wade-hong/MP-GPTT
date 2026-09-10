using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MissionPlanner.FMT
{
    public sealed class P400FrequencyPlan
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public string[] Frequencies { get; set; } = new string[50];

        public static string Normalize(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return "";
            decimal value;
            if (!Regex.IsMatch(text, @"^\d+(\.\d{1,6})?$") ||
                !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) || value <= 0 || value > 10000)
                throw new FormatException("頻率請輸入正數 MHz（最多六位小數，使用小數點），不可含單位或千分位：" + text);
            return value.ToString("0.000000", CultureInfo.InvariantCulture);
        }

        public void Validate()
        {
            if (Version != 1 || Frequencies == null || Frequencies.Length != 50 || Name == null || Name.Length > 80)
                throw new FormatException("頻率組合格式錯誤；必須是版本 1、50 筆資料，名稱最多 80 字。");
            // Validate before mutating, so invalid imports never partly replace the editor.
            var normalized = Frequencies.Select(Normalize).ToArray();
            Frequencies = normalized;
        }

        public string ToCsv()
        {
            Validate();
            var result = new StringBuilder("Channel,FrequencyMHz\r\n");
            for (int i = 0; i < 50; i++) result.Append(i + 1).Append(',').Append(Frequencies[i]).Append("\r\n");
            return result.ToString();
        }

        public void ValidateForRadio()
        {
            Validate();
            if (Frequencies.Any(f => string.IsNullOrEmpty(f) || decimal.Parse(f, CultureInfo.InvariantCulture) < 410m || decimal.Parse(f, CultureInfo.InvariantCulture) > 480m))
                throw new FormatException("寫入需完整 50 筆，且每筆介於 410–480 MHz；仍須自行確認設備與使用頻段。");
        }

        // Accept exported CSV, two-column TSV/AT table, or a single frequency per line.
        public static P400FrequencyPlan Parse(string text)
        {
            if (text == null || text.Length > 65536) throw new FormatException("輸入資料過大。");
            var plan = new P400FrequencyPlan();
            var used = new bool[50]; int sequential = 0; bool? indexed = null;
            foreach (var raw in text.TrimStart('\uFEFF').Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line == "Channel,FrequencyMHz" || Regex.IsMatch(line, @"^Ch\s+Freq\(MHz\)$", RegexOptions.IgnoreCase)) continue;
                var parts = line.Contains(",") ? line.Split(',') : Regex.Split(line, @"\s+");
                int index; string frequency;
                if (parts.Length == 1)
                {
                    if (indexed == true) throw new FormatException("不可混用單欄與編號格式。");
                    indexed = false; index = ++sequential; frequency = parts[0];
                }
                else if (parts.Length == 2 && int.TryParse(parts[0], out index))
                {
                    if (indexed == false) throw new FormatException("不可混用單欄與編號格式。");
                    indexed = true; frequency = parts[1];
                }
                else throw new FormatException("無法辨識列：" + line);
                if (index < 1 || index > 50 || used[index - 1]) throw new FormatException("編號須為 1–50 且不可重複；不會截斷超過 50 筆的資料。");
                used[index - 1] = true; plan.Frequencies[index - 1] = Normalize(frequency);
            }
            if (!used.Any(b => b)) throw new FormatException("沒有可匯入的頻率資料。");
            plan.Validate(); return plan;
        }

        public void Save(string path)
        {
            Validate();
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static P400FrequencyPlan Load(string path)
        {
            if (new FileInfo(path).Length > 65536) throw new FormatException("組合檔案過大。");
            var plan = Newtonsoft.Json.JsonConvert.DeserializeObject<P400FrequencyPlan>(File.ReadAllText(path));
            if (plan == null) throw new FormatException("無效組合檔案。");
            plan.Validate(); return plan;
        }
    }
}

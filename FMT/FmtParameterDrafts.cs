using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MissionPlanner.FMT
{
    /// <summary>Offline, source-matched drafts; never used to change parameter IDs or values.</summary>
    internal static class FmtParameterDrafts
    {
        private static readonly Lazy<Dictionary<string, string>> Catalog =
            new Lazy<Dictionary<string, string>>(() =>
            {
                using (var stream = typeof(FmtParameterDrafts).Assembly.GetManifestResourceStream("FMT.Parameters.zh-TW.draft.json"))
                {
                    if (stream == null) return new Dictionary<string, string>(StringComparer.Ordinal);
                    using (var reader = new StreamReader(stream))
                        return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
                }
            });

        internal static string Translate(string source)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                !CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return source;
            // Numbers, baud rates and unit-only options are not prose.
            if (Regex.IsMatch(source.Trim(), @"^[+-]?\d+(?:\.\d+)?(?:\s*(?:Hz|kHz|MHz|MBaud|kBaud|Baud|ms|s|m|cm|mm|m/s|V|A|%))?$", RegexOptions.IgnoreCase))
                return source;
            string draft;
            if (!Catalog.Value.TryGetValue(source.Trim(), out draft)) return source;
            const string numberPattern = @"(?<![A-Za-z0-9_])[+-]?\d+(?:\.\d+)?(?![A-Za-z0-9_])";
            var sourceNumbers = Regex.Matches(source, numberPattern).Cast<Match>().Select(m => m.Value);
            var draftNumbers = new HashSet<string>(Regex.Matches(draft, numberPattern).Cast<Match>().Select(m => m.Value));
            if (sourceNumbers.Any(number => !draftNumbers.Contains(number)))
                return "〔機譯：數值請依原文核對〕" + draft;
            return "〔機譯待核對〕" + draft;
        }
    }
}

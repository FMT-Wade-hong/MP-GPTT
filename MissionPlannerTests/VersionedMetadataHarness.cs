using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using MissionPlanner.Utilities;

internal static class VersionedMetadataHarness
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static int Main(string[] args)
    {
        int versions = 0, parameters = 0, fieldsChecked = 0, helicopterVariants = 0;
        foreach (var path in Directory.GetFiles(args[0], "*.apm.pdef.xml.gz").OrderBy(x => x))
        {
            var key = Path.GetFileName(path).Replace(".apm.pdef.xml.gz", "");
            Check(VersionedParameterMetadata.ContainsVersion(key), "Unbundled release " + key);
            using (var input = File.OpenRead(path))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                var seen = new HashSet<string>();
                var nodes = XDocument.Load(gzip).Descendants("param").ToList();
                foreach (var node in nodes)
                {
                    var name = ((string)node.Attribute("name")).Split(':').Last();
                    if (!seen.Add(name)) continue;
                    var expected = new Dictionary<string, string>
                    {
                        ["DisplayName"] = (string)node.Attribute("humanName") ?? "",
                        ["Description"] = (string)node.Attribute("documentation") ?? "",
                        ["User"] = (string)node.Attribute("user") ?? "",
                        ["Values"] = string.Concat(node.Elements("values").Elements("value").Select(
                            v => (string)v.Attribute("code") + ":" + v.Value + ","))
                    };
                    foreach (var field in node.Elements("field"))
                    {
                        var id = (string)field.Attribute("name");
                        if (!expected.ContainsKey(id)) expected.Add(id, field.Value);
                    }
                    foreach (var missing in new[] { "Units", "Range", "Increment", "Bitmask", "ReadOnly", "RebootRequired" })
                        if (!expected.ContainsKey(missing)) expected.Add(missing, "");
                    foreach (var field in expected)
                    {
                        var actual = ParameterMetaDataRepository.GetParameterMetaData(name, field.Key, key);
                        Check(actual == field.Value, key + ": " + name + "/" + field.Key + " mismatch");
                        fieldsChecked++;
                    }
                    parameters++;
                }
                if (key.StartsWith("Copter"))
                {
                    var heliKey = key.Replace("Copter", "Heli");
                    foreach (var node in nodes.Where(n => ((string)n.Attribute("source") ?? "").EndsWith("AC_AttitudeControl_Heli.cpp") ||
                                                         ((string)n.Attribute("source") ?? "").EndsWith("AC_AutoTune_Heli.cpp")))
                    {
                        var name = ((string)node.Attribute("name")).Split(':').Last();
                        foreach (var field in node.Elements("field"))
                            Check(ParameterMetaDataRepository.GetParameterMetaData(name, (string)field.Attribute("name"), heliKey) == field.Value,
                                heliKey + ": wrong helicopter field " + name + "/" + (string)field.Attribute("name"));
                        helicopterVariants++;
                    }
                    Check(ParameterMetaDataRepository.GetParameterMetaData("ATC_RAT_RLL_P", "Range", heliKey) == "0.0 0.35", "Helicopter PID range mixed with multirotor");
                    Check(ParameterMetaDataRepository.GetParameterMetaData("ATC_RAT_RLL_P", "Range", key) == "0.01 0.5", "Multirotor PID range mixed with helicopter");
                }
            }
            Check(ParameterMetaDataRepository.GetParameterMetaData("FMT_MISSING_PARAM", "Range", key) == "", "Missing parameter leaked metadata");
            versions++;
            Console.WriteLine("PASS: " + key);
        }
        Check(versions == 31, "Expected 31 published 4.6/4.7 snapshots");
        var vehicleNames = new[] { "ArduCopter2", "ArduPlane", "ArduRover", "ArduSub", "ArduTracker" };
        var families = new[] { "Copter", "Plane", "Rover", "Sub", "Tracker" };
        for (int i = 0; i < vehicleNames.Length; i++)
            Check(ParameterMetaDataRepository.GetFirmwareKey(vehicleNames[i], "ArduPilot V4.7.2") == families[i] + "4.7.2", "Wrong firmware family");
        // Simulate changing active vehicles without restarting or waiting for cache expiry.
        foreach (var version in new[] { "4.6.3", "4.7.2", "4.6.1", "4.7.0" })
        {
            var key = "Copter" + version;
            ParameterMetaDataRepository.VehicleTypeResolver = x => x == "ArduCopter2" ? key : x;
            var name = version.StartsWith("4.6") ? "WPNAV_SPEED" : "WP_SPD";
            Check(ParameterMetaDataRepository.GetParameterMetaData(name, "Units", "ArduCopter2") ==
                (version.StartsWith("4.6") ? "cm/s" : "m/s"), "Version switch used stale units");
            Check(ParameterMetaDataRepository.GetParameterMetaData("WP_SPD", "Units", "Copter4.7.2") == "m/s", "Explicit key overridden");
        }
        ParameterMetaDataRepository.VehicleTypeResolver = null;
        Check(ParameterMetaDataRepository.GetParameterMetaData("ARMING_CHECK", "Bitmask", "Copter4.7.2") == "", "Old arming semantics leaked into 4.7");
        Check(ParameterMetaDataRepository.GetParameterMetaData("ARMING_SKIPCHK", "Bitmask", "Copter4.7.2").Length > 0, "New skip-check bitmask absent");
        Check(!VersionedParameterMetadata.ContainsVersion("Copter4.6.99"), "Unknown version silently substituted");
        Console.WriteLine($"PASS: {versions} releases; {parameters} parameter definitions; {fieldsChecked} metadata fields; {helicopterVariants} helicopter variants; version isolation; missing fields; arming bitmask semantics");
        return 0;
    }
}

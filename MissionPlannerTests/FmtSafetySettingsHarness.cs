using MissionPlanner.FMT;
using System;
using System.Linq;

internal static class FmtSafetySettingsHarness
{
    private static int passed;

    private static int Main()
    {
        try
        {
            var definitions = FmtSafetyParameterCatalog.Definitions;
            Check(definitions.Count >= 13, "catalog covers independent safety categories");
            Check(definitions.Select(item => item.Title).Distinct().Count() == definitions.Count,
                "card titles are unique");

            var radio = definitions.Single(item => item.Title == "遙控器失聯動作");
            Check(radio.Resolve(new[] { "THR_FAILSAFE" }) == "THR_FAILSAFE",
                "Plane radio failsafe alias resolves");
            Check(radio.Resolve(new[] { "FS_THR_ENABLE", "THR_FAILSAFE" }) == "FS_THR_ENABLE",
                "current Copter radio failsafe alias has priority");
            Check(radio.Resolve(new[] { "UNRELATED" }) == null,
                "unsupported parameter stays hidden");

            var gcs = definitions.Single(item => item.Title == "地面站失聯動作");
            Check(gcs.Resolve(new[] { "FS_GCS_ENABL" }) == "FS_GCS_ENABL",
                "legacy Plane GCS alias resolves");
            Check(FmtSafetyParameterCatalog.TranslateOption("Disabled") == "停用",
                "disabled option translated");
            Check(FmtSafetyParameterCatalog.TranslateOption("SmartRTL or RTL").Contains("返航"),
                "return action translated");
            Check(FmtSafetyParameterCatalog.TranslateOption("Continue if in Auto on RC failsafe") ==
                  "遙控失聯時繼續 AUTO 任務", "FS_OPTIONS RC exception translated");
            Check(FmtSafetyParameterCatalog.TranslateOption("Release Gripper") ==
                  "失效保護時釋放夾爪", "FS_OPTIONS gripper exception translated without case sensitivity");
            Check(definitions.All(item => item.Title != "遙控器油門門檻" &&
                                          item.Title != "低容量門檻" &&
                                          item.Title != "嚴重容量門檻"),
                "removed threshold cards stay absent");

            Console.WriteLine("PASS: " + passed + " safety-setting checks.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Check(bool result, string label)
    {
        if (!result)
            throw new Exception("FAIL " + label);
        passed++;
        Console.WriteLine("PASS " + label);
    }
}

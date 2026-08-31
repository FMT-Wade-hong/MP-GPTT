using System;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    // Keep the decision independent of the main form's destructive cleanup.
    internal sealed class FmtConnectionCloseGuard
    {
        private bool prompting;

        internal bool ShouldCancel(CloseReason reason, bool telemetryConnected, bool mqttRunning,
            Func<string, DialogResult> confirm)
        {
            if (reason == CloseReason.WindowsShutDown) return false;
            if (prompting) return true;
            if (!telemetryConnected && !mqttRunning) return false;

            string status = telemetryConnected && mqttRunning
                ? "MP 目前仍有連線，MQTT 橋接也正在執行。"
                : telemetryConnected ? "MP 目前仍有連線。" : "MQTT 橋接仍在執行（可能正在連線或重連）。";
            string message = status + "\r\n\r\n關閉程式會中斷連線與資料傳輸，確定要關閉嗎？" +
                "\r\n\r\n選擇「否」可返回程式並保持連線。";
            prompting = true;
            try { return confirm(message) != DialogResult.Yes; }
            finally { prompting = false; }
        }
    }
}

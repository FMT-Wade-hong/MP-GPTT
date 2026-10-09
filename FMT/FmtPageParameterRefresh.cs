using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner
{
    // Page refresh is read-only, single-flight and never requests PARAM_REQUEST_LIST.
    internal static class FmtPageParameterRefresh
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        internal const int IntervalMilliseconds = 300;

        internal sealed class EditorLock : IDisposable
        {
            private readonly List<Action> restore = new List<Action>();
            internal EditorLock(Control page) { LockChildren(page); }
            private void LockChildren(Control parent)
            {
                foreach (Control control in parent.Controls)
                {
                    if (control is TextBoxBase text)
                    {
                        var previous = text.ReadOnly;
                        restore.Add(() => { if (!text.IsDisposed) text.ReadOnly = previous; });
                        text.ReadOnly = true;
                    }
                    else if (control is DataGridView grid)
                    {
                        var previous = grid.ReadOnly;
                        restore.Add(() => { if (!grid.IsDisposed) grid.ReadOnly = previous; });
                        grid.ReadOnly = true;
                    }
                    else if (control is NumericUpDown || control is ComboBox || control is Button ||
                        control is CheckBox || control is RadioButton || control is TrackBar || control is ListBox)
                    {
                        var previous = control.Enabled;
                        restore.Add(() => { if (!control.IsDisposed) control.Enabled = previous; });
                        control.Enabled = false;
                    }
                    else LockChildren(control);
                }
            }
            public void Dispose()
            {
                foreach (var action in restore) action();
                restore.Clear();
            }
        }

        // Explicit recovery/full-table action only; ordinary tuning pages remain scoped.
        internal static void ReloadAll(Control page, Control button, Action reload, bool hasChanges = false)
        {
            var link = MainV2.comPort;
            if (page.IsDisposed || link?.BaseStream == null || !link.BaseStream.IsOpen) return;
            if (!Gate.Wait(0))
            {
                CustomMessageBox.Show("已有參數讀取進行中，請等待完成或取消後再試。", "重新載入全部參數");
                return;
            }
            var target = link.MAV;
            var enabled = button.Enabled;
            try
            {
                if (link.giveComport || link.IsParameterListLoading || link.IsLogDownloadActive || target.cs.armed)
                {
                    CustomMessageBox.Show("請先上鎖並結束 LOG 下載或其他通訊作業，再重新載入全部參數。", "重新載入全部參數");
                    return;
                }
                if (CustomMessageBox.Show("此操作會重新載入飛控全部參數，並顯示可取消的進度視窗。" +
                    (hasChanges ? "\r\n成功載入後將捨棄此頁尚未寫入的修改。" : "") + "\r\n是否繼續？",
                    "重新載入全部參數", MessageBoxButtons.YesNo) != (int)DialogResult.Yes) return;
                if (MainV2.comPort != link || link.MAV != target || !link.BaseStream.IsOpen ||
                    target.cs.armed || link.IsParameterListLoading || link.IsLogDownloadActive || link.giveComport) return;
                button.Enabled = false;
                // Existing modal progress runner performs the transfer in the background.
                link.getParamList();
                if (page.IsDisposed || MainV2.comPort != link || link.MAV != target || !link.BaseStream.IsOpen) return;
                if (link.LastParamListSucceeded && target.param.TotalReported > 0 &&
                    target.param.TotalReceived >= target.param.TotalReported)
                    reload();
                else CustomMessageBox.Show("全部參數尚未載入完成（可能已取消或連線逾時），可再次按重新載入。", "參數載入未完成");
            }
            catch (Exception ex)
            {
                if (!page.IsDisposed) CustomMessageBox.Show("載入失敗，可重試：\r\n" + ex.Message, "重新載入全部參數");
            }
            finally
            {
                if (!button.IsDisposed) button.Enabled = enabled;
                Gate.Release();
            }
        }

        internal static string[] BoundNames(Control root, bool visibleOnly = false)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Control control in root.Controls)
            {
                if (visibleOnly && !control.Visible) continue;
                string name;
                if (control is MavlinkNumericUpDown number) name = number.ParamName;
                else if (control is MavlinkComboBox combo) name = combo.ParamName;
                else if (control is MavlinkCheckBox check) name = check.ParamName;
                else if (control is MavlinkCheckBoxBitMask bits) name = bits.ParamName;
                else name = control is NumericUpDown || control is ComboBox || control is TextBox ||
                    control is RangeControl || control is ValuesControl ? control.Name : null;
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                foreach (var child in BoundNames(control, visibleOnly)) names.Add(child);
            }
            return names.ToArray();
        }

        // Large parameter browsers must not silently turn a refresh into a full download.
        internal static string[] DisplayedRows(DataGridView grid, int nameColumn = 0)
        {
            return grid.Rows.Cast<DataGridViewRow>()
                .Where(row => !row.IsNewRow && row.Visible && row.Displayed)
                .Select(row => Convert.ToString(row.Cells[nameColumn].Value).Trim())
                .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        }

        internal static string[] DisplayedControls(Control root)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Control control in root.Controls)
            {
                if (!control.Visible || !root.ClientRectangle.IntersectsWith(control.Bounds)) continue;
                foreach (var name in BoundNames(control, true)) names.Add(name);
                if (!string.IsNullOrWhiteSpace(control.Name)) names.Add(control.Name);
                foreach (var name in DisplayedControls(control)) names.Add(name);
            }
            return names.ToArray();
        }

        internal static bool TryRefreshVisible(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (!control.Visible || !control.Enabled) continue;
                if (control is Button button && (control.Name == "BUT_rerequestparams" ||
                    control.Name == "BUT_refreshpart" || control.Name == "btnRefreshParameters" ||
                    control.Name == "BUT_FmtRefreshPage"))
                {
                    button.PerformClick();
                    return true;
                }
                if (TryRefreshVisible(control)) return true;
            }
            return false;
        }

        internal static async Task RefreshAsync(Control page, Control button, IEnumerable<string> requested,
            Action reload, bool hasChanges = false, CancellationToken cancellation = default(CancellationToken),
            bool allowUnloaded = false)
        {
            var link = MainV2.comPort;
            if (page.IsDisposed || !page.Visible || link?.BaseStream == null || !link.BaseStream.IsOpen) return;
            if (!Gate.Wait(0))
            {
                CustomMessageBox.Show("已有參數更新進行中；請等待完成或離開原頁面停止更新。", "更新當頁參數");
                return;
            }
            var vehicle = link.MAV;
            var oldText = button?.Text;
            EditorLock editors = null;
            bool refreshing = false;
            using (var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                EventHandler hidden = (s, e) => { if (!page.Visible) source.Cancel(); };
                EventHandler disposed = (s, e) => source.Cancel();
                page.VisibleChanged += hidden;
                page.Disposed += disposed;
                try
                {
                    var names = requested.Where(name => !string.IsNullOrWhiteSpace(name) &&
                            (allowUnloaded || vehicle.param.ContainsKey(name)))
                        .Distinct(StringComparer.Ordinal).ToArray();
                    if (link.giveComport || link.IsParameterListLoading || link.IsLogDownloadActive || vehicle.cs.armed)
                    {
                        CustomMessageBox.Show("飛控已解鎖或連線正在處理其他作業（例如下載 LOG）；請結束後再更新。", "更新當頁參數");
                        return;
                    }
                    if (names.Length == 0)
                    {
                        CustomMessageBox.Show("此頁沒有可更新的已載入參數；不會改為下載完整參數表。", "更新當頁參數");
                        return;
                    }
                    if (hasChanges && CustomMessageBox.Show("更新將捨棄此頁尚未寫入的修改，是否繼續？",
                        "更新當頁參數", MessageBoxButtons.YesNo) != (int)DialogResult.Yes) return;
                    editors = new EditorLock(page);
                    refreshing = true;
                    if (button != null) button.Text = "讀取當頁中…";
                    Func<bool> current = () => MainV2.comPort == link && link.MAV == vehicle &&
                        link.BaseStream.IsOpen && !vehicle.cs.armed;
                    var progress = new Progress<int>(done =>
                    {
                        if (refreshing && !page.IsDisposed && !source.IsCancellationRequested && button != null)
                            button.Text = "讀取 " + done + "/" + names.Length;
                    });
                    await Task.Run(() => ReadBatchAsync(names, name =>
                    {
                        if (link.giveComport || link.IsParameterListLoading || link.IsLogDownloadActive)
                            throw new InvalidOperationException("連線正在處理其他作業，已停止更新。");
                        return link.GetParamAsync(vehicle.sysid, vehicle.compid, name);
                    }, current, source.Token, progress));
                    editors.Dispose();
                    editors = null;
                    if (!source.IsCancellationRequested && current() && !page.IsDisposed) reload();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (!page.IsDisposed && !source.IsCancellationRequested)
                        CustomMessageBox.Show("更新已停止，保留原畫面及未寫入修改：\r\n" + ex.Message, "更新當頁參數");
                }
                finally
                {
                    refreshing = false;
                    page.VisibleChanged -= hidden;
                    page.Disposed -= disposed;
                    editors?.Dispose();
                    if (!page.IsDisposed)
                    {
                        if (button != null && !button.IsDisposed) button.Text = oldText;
                    }
                    Gate.Release();
                }
            }
        }

        internal static async Task ReadBatchAsync(string[] names, Func<string, Task> read,
            Func<bool> current, CancellationToken cancellation, IProgress<int> progress = null)
        {
            var elapsed = Stopwatch.StartNew();
            for (int i = 0; i < names.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!current()) throw new InvalidOperationException("連線、機體或解鎖狀態已變更。");
                if (elapsed.Elapsed > TimeSpan.FromMinutes(2))
                    throw new TimeoutException("更新已超過 2 分鐘，請縮小顯示範圍再試。");
                // Stop at the first timeout. The transport's bounded retry remains in effect.
                await read(names[i]).ConfigureAwait(false);
                progress?.Report(i + 1);
                await Task.Delay(IntervalMilliseconds, cancellation).ConfigureAwait(false);
            }
        }
    }
}

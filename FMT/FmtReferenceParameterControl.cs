using System;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.FMT
{
    // Numeric editor for reference-only metadata. No inherited unit scaling,
    // slider range or enumerated/bitmask semantics are applied to a new firmware.
    internal sealed class FmtReferenceParameterControl : MyUserControl, IDynamicParameterControl
    {
        public event EventValueChanged ValueChanged;
        private readonly TextBox editor = new TextBox();
        private readonly ErrorProvider errors;
        private readonly bool integerOnly;
        private string committed;
        public string LabelText { get; }
        public string DescriptionText { get; }
        public string Value
        {
            get => committed;
            set
            {
                committed = double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)
                    ? number.ToString("R", CultureInfo.InvariantCulture) : value;
                editor.Text = value;
            }
        }

        public FmtReferenceParameterControl(string name, string title, string description, string value, bool integer)
        {
            Name = name; LabelText = title; DescriptionText = description; integerOnly = integer;
            Height = 100; MinimumSize = new Size(250, 100);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            editor.Dock = DockStyle.Fill;
            layout.Controls.Add(editor, 1, 0);
            var explanation = new Label { Text = description, Dock = DockStyle.Fill, AutoEllipsis = true };
            layout.Controls.Add(explanation, 0, 1); layout.SetColumnSpan(explanation, 2);
            Controls.Add(layout);
            errors = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };
            Value = value;
            editor.Validating += (sender, args) =>
            {
                if (!double.TryParse(editor.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number) ||
                    double.IsNaN(number) || double.IsInfinity(number) || float.IsInfinity((float)number) ||
                    (integerOnly && (number != Math.Truncate(number) || (double)(float)number != number)))
                {
                    errors.SetError(editor, integerOnly ? "請輸入可精確傳送的有效整數" : "請輸入有效數值");
                    args.Cancel = true; return;
                }
                errors.SetError(editor, "");
                var text = ((float)number).ToString("R", CultureInfo.InvariantCulture);
                if (double.TryParse(committed, NumberStyles.Float, CultureInfo.InvariantCulture, out var previous) && (float)previous == (float)number) return;
                committed = text;
                ValueChanged?.Invoke(this, Name, text);
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) errors?.Dispose();
            base.Dispose(disposing);
        }
    }
}

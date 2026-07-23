namespace POS.Desktop.CustomControls;
using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;

public class RtlDataGridView : DataGridView
{
    public RtlDataGridView()
    {
        RightToLeft = RightToLeft.Yes;
        BackgroundColor = DesignTokens.Colors.Surface;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        RowHeadersVisible = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        GridColor = DesignTokens.Colors.Border;
        Font = DesignTokens.Typography.Table;
        RowTemplate.Height = 38;
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = DesignTokens.Typography.TableHeader,
            BackColor = DesignTokens.Colors.TableHeader,
            ForeColor = DesignTokens.Colors.TextPrimary,
            SelectionBackColor = DesignTokens.Colors.TableHeader,
            SelectionForeColor = DesignTokens.Colors.TextPrimary,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            Padding = new Padding(DesignTokens.Spacing.Small)
        };
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = DesignTokens.Colors.TableRowAlt };
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = DesignTokens.Colors.Surface,
            ForeColor = DesignTokens.Colors.TextPrimary,
            SelectionBackColor = Color.FromArgb(37, 99, 235, 30),
            SelectionForeColor = DesignTokens.Colors.TextPrimary,
            Padding = new Padding(DesignTokens.Spacing.Small)
        };
        EnableHeadersVisualStyles = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 40;
        ScrollBars = ScrollBars.Both;
    }

    public void ShowEmptyMessage(string message)
    {
        if (Rows.Count == 0)
        {
            var lbl = new Label
            {
                Text = message,
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = DesignTokens.Colors.Surface
            };
            Controls.Add(lbl);
        }
        else
        {
            var lbl = Controls.OfType<Label>().FirstOrDefault();
            if (lbl != null) Controls.Remove(lbl);
        }
    }
}
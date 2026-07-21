namespace POS.Desktop.CustomControls;
using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;

public class RtlDataGridView : DataGridView
{
    public RtlDataGridView()
    {
        RightToLeft = RightToLeft.Yes;
        BackgroundColor = Color.White;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        RowHeadersVisible = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        GridColor = Colors.Border;
        Font = Typography.Table;
        RowTemplate.Height = 36;
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = Typography.TableHeader,
            BackColor = Colors.TableHeader,
            ForeColor = Colors.TextPrimary,
            SelectionBackColor = Colors.TableHeader,
            SelectionForeColor = Colors.TextPrimary,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            Padding = new Padding(DesignTokens.Spacing.Small)
        };
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Colors.TableRowAlt };
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Colors.TextPrimary,
            SelectionBackColor = Color.FromArgb(41, 98, 255, 30),
            SelectionForeColor = Colors.TextPrimary,
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
                Font = Typography.Body,
                ForeColor = Colors.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.White
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
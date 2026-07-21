using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Columns;
using DevExpress.Utils;
using DevExpress.XtraEditors.Repository;
using POS.Desktop.Themes;

namespace POS.Desktop.CustomControls;

/// <summary>
/// DevExpress GridControl wrapper with RTL layout, POS design tokens, and helper methods.
/// Replaces the standard WinForms DataGridView used in list forms.
/// </summary>
public class RtlGridControl : GridControl
{
    private GridView _view;

    /// <summary>
    /// The underlying GridView with RTL and styling already configured.
    /// </summary>
    public GridView GridViewCore => _view;

    /// <summary>
    /// The focused row's data object (cast to your type).
    /// </summary>
    public object? FocusedRow => _view.GetFocusedRow();

    /// <summary>
    /// Fired when the actions button column is clicked (column name "Actions").
    /// </summary>
    public event EventHandler<ActionButtonClickArgs>? ActionButtonClick;

    public RtlGridControl()
    {
        RightToLeft = RightToLeft.Yes;
        Font = Typography.Table;
        Dock = DockStyle.Fill;

        // Create and configure the GridView
        _view = new GridView();
        _view.GridControl = this;

        // Header appearance
        _view.Appearance.HeaderPanel.TextOptions.HAlignment = HorzAlignment.Center;
        _view.Appearance.HeaderPanel.TextOptions.VAlignment = VertAlignment.Center;
        _view.Appearance.HeaderPanel.Font = Typography.TableHeader;
        _view.Appearance.HeaderPanel.BackColor = Colors.TableHeader;
        _view.Appearance.HeaderPanel.ForeColor = Colors.TextPrimary;
        _view.Appearance.HeaderPanel.Options.UseFont = true;
        _view.Appearance.HeaderPanel.Options.UseBackColor = true;
        _view.Appearance.HeaderPanel.Options.UseForeColor = true;

        // Row appearance
        _view.Appearance.Row.Font = Typography.Table;
        _view.Appearance.Row.ForeColor = Colors.TextPrimary;
        _view.Appearance.Row.BackColor = Color.White;
        _view.Appearance.Row.Options.UseFont = true;
        _view.Appearance.Row.Options.UseForeColor = true;
        _view.Appearance.Row.Options.UseBackColor = true;

        // Alternating row (EvenRow instead of AlternatingRow)
        _view.Appearance.EvenRow.BackColor = Colors.TableRowAlt;
        _view.Appearance.EvenRow.Options.UseBackColor = true;

        // Selected row
        _view.Appearance.FocusedRow.BackColor = Color.FromArgb(41, 98, 255, 30);
        _view.Appearance.FocusedRow.ForeColor = Colors.TextPrimary;
        _view.Appearance.FocusedRow.Options.UseBackColor = true;
        _view.Appearance.FocusedRow.Options.UseForeColor = true;

        // Odd row (white)
        _view.Appearance.OddRow.BackColor = Color.White;
        _view.Appearance.OddRow.Options.UseBackColor = true;

        // Grid lines
        _view.OptionsView.ShowHorizontalLines = DevExpress.Utils.DefaultBoolean.True;
        _view.OptionsView.ShowVerticalLines = DevExpress.Utils.DefaultBoolean.False;
        _view.OptionsView.ShowGroupPanel = false;
        _view.OptionsView.ShowIndicator = false;
        _view.OptionsView.EnableAppearanceEvenRow = true;
        _view.OptionsView.EnableAppearanceOddRow = true;
        _view.OptionsView.ColumnAutoWidth = false;
        _view.HorzScrollVisibility = DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto;
        _view.ColumnPanelRowHeight = 40;
        _view.RowHeight = 36;

        // Selection
        _view.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
        _view.OptionsSelection.EnableAppearanceFocusedRow = true;
        _view.OptionsSelection.EnableAppearanceFocusedCell = false;
        _view.OptionsBehavior.Editable = false;
        _view.OptionsBehavior.ReadOnly = true;
        _view.OptionsNavigation.EnterMoveNextColumn = false;

        // Look & feel
        _view.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;

        // Handle row cell click to detect actions column
        _view.RowCellClick += OnRowCellClick;
    }

    /// <summary>
    /// Adds a text column to the grid view.
    /// </summary>
    public GridColumn AddTextColumn(string fieldName, string caption, int width = 100,
        HorzAlignment alignment = HorzAlignment.Default, string format = "")
    {
        var col = new GridColumn
        {
            FieldName = fieldName,
            Caption = caption,
            Width = width,
            Visible = true,
            Name = $"col{fieldName}"
        };
        col.AppearanceHeader.TextOptions.HAlignment = HorzAlignment.Center;
        col.AppearanceCell.TextOptions.HAlignment = alignment;
        col.AppearanceCell.Font = Typography.Table;
        col.AppearanceCell.Options.UseFont = true;

        if (!string.IsNullOrEmpty(format))
        {
            col.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            col.DisplayFormat.FormatString = format;
        }

        _view.Columns.Add(col);
        return col;
    }

    /// <summary>
    /// Adds an actions button column (displays a button per row).
    /// </summary>
    public GridColumn AddActionsColumn(string caption = "إجراءات", int width = 80)
    {
        var col = new GridColumn
        {
            FieldName = "Actions",
            Caption = caption,
            Width = width,
            Visible = true,
            Name = "colActions",
            ColumnEdit = CreateActionsButtonEdit()
        };
        col.AppearanceHeader.TextOptions.HAlignment = HorzAlignment.Center;
        col.AppearanceCell.TextOptions.HAlignment = HorzAlignment.Center;
        _view.Columns.Add(col);
        return col;
    }

    private RepositoryItemButtonEdit CreateActionsButtonEdit()
    {
        var repo = new RepositoryItemButtonEdit
        {
            TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor,
            AutoHeight = false
        };
        repo.Buttons.Clear();
        var btn = new DevExpress.XtraEditors.Controls.EditorButton(
            DevExpress.XtraEditors.Controls.ButtonPredefines.Glyph,
            "⋮",
            20,
            true,
            true,
            true,
            DevExpress.Utils.HorzAlignment.Center,
            null,
            null,
            "إجراءات");
        repo.Buttons.Add(btn);
        repo.ButtonClick += (s, e) =>
        {
            var rowHandle = _view.FocusedRowHandle;
            if (rowHandle >= 0)
            {
                ActionButtonClick?.Invoke(this, new ActionButtonClickArgs(rowHandle, _view.GetRow(rowHandle)));
            }
        };
        return repo;
    }

    /// <summary>
    /// Sets the data source (typically a DataTable or List).
    /// Preserves manually-defined columns (added via AddTextColumn/AddActionsColumn).
    /// Hidden internal fields (__Id, Raw*) are not exposed because PopulateColumns is NOT called.
    /// </summary>
    public void SetDataSource(object dataSource)
    {
        _view.GridControl.BeginUpdate();
        // Preserve existing manually-defined column list
        var existingColumns = _view.Columns.Cast<GridColumn>().ToList();
        _view.Columns.Clear();
        // Restore the manually-defined columns before binding
        foreach (var col in existingColumns)
            _view.Columns.Add(col);
        DataSource = dataSource;
        // PopulateColumns is NOT called — only our manually-defined columns appear.
        // DataTable fields auto-bind to matching FieldName properties.
        _view.GridControl.EndUpdate();
    }

    /// <summary>
    /// Clears the data source.
    /// </summary>
    public void ClearDataSource()
    {
        DataSource = null;
    }

    /// <summary>
    /// Gets the data row at the specified visible index.
    /// </summary>
    public DataRowView? GetRow(int visibleIndex)
    {
        var rowHandle = _view.GetVisibleRowHandle(visibleIndex);
        if (rowHandle >= 0)
            return _view.GetRow(rowHandle) as DataRowView;
        return null;
    }

    /// <summary>
    /// Creates a DataTable with typed columns matching the specified schema.
    /// </summary>
    public static DataTable CreateTable(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        table.Columns.Add("__Id", typeof(int)); // hidden ID column
        foreach (var (name, type) in columns)
        {
            table.Columns.Add(name, type);
        }
        return table;
    }

    private void OnRowCellClick(object? sender, RowCellClickEventArgs e)
    {
        if (e.Column?.FieldName == "Actions" && e.RowHandle >= 0)
        {
            ActionButtonClick?.Invoke(this, new ActionButtonClickArgs(e.RowHandle, _view.GetRow(e.RowHandle)));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _view?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Event args for the action button click event.
/// </summary>
public class ActionButtonClickArgs : EventArgs
{
    public int RowHandle { get; }
    public object? RowData { get; }

    public ActionButtonClickArgs(int rowHandle, object? rowData)
    {
        RowHandle = rowHandle;
        RowData = rowData;
    }
}

namespace POS.Desktop.Themes;
using System.Windows.Forms;

public static class RtlMessageBox
{
    private const MessageBoxOptions RtlOptions = MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading;

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        => MessageBox.Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, RtlOptions);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        => MessageBox.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, RtlOptions);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxOptions options)
        => MessageBox.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, RtlOptions | options);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxOptions options)
        => MessageBox.Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, RtlOptions | options);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
        => MessageBox.Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, RtlOptions | options);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
        => MessageBox.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, RtlOptions | options);
}

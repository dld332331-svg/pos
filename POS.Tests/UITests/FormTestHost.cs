using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace POS.Tests.UITests;

/// <summary>
/// Hosts a WinForms Form (or UserControl inside a Form) on a dedicated STA thread
/// with a running Windows message pump. Provides reflection-based control access
/// and thread-marshalled interaction methods for UI testing.
/// </summary>
/// <typeparam name="TForm">The Form or UserControl type to test</typeparam>
public sealed class FormTestHost<TForm> : IDisposable where TForm : Control
{
    private readonly Thread _uiThread;
    private Form? _hostForm;
    private TForm? _control;
    private Exception? _creationException;
    private readonly AutoResetEvent _ready = new(false);
    private bool _disposed;

    /// <summary>The hosted control instance (Form or UserControl).</summary>
    public TForm Control => _control
        ?? throw new InvalidOperationException("Control not created yet.");

    /// <summary>The host Form (same as Control if Control is a Form).</summary>
    public Form HostForm => _hostForm
        ?? throw new InvalidOperationException("Host form not created yet.");

    /// <summary>
    /// Creates the form/control on an STA thread with a message pump.
    /// </summary>
    /// <param name="constructorArgs">Arguments passed to the TForm constructor.</param>
    public FormTestHost(params object?[] constructorArgs)
    {
        _uiThread = new Thread(() =>
        {
            try
            {
                // Create the control
                var args = constructorArgs.Select(a => a).ToArray();
                _control = (TForm)Activator.CreateInstance(typeof(TForm), args)!;

                if (_control is Form form)
                {
                    _hostForm = form;
                    _hostForm.Shown += (_, _) => _ready.Set();
                    System.Windows.Forms.Application.Run(_hostForm);
                }
                else
                {
                    // UserControl — host inside a Form
                    _hostForm = new Form
                    {
                        ClientSize = new Size(1200, 800),
                        RightToLeft = RightToLeft.Yes,
                        RightToLeftLayout = true
                    };
                    _control.Dock = DockStyle.Fill;
                    _hostForm.Controls.Add(_control);
                    _hostForm.Shown += (_, _) => _ready.Set();
                    System.Windows.Forms.Application.Run(_hostForm);
                }
            }
            catch (Exception ex)
            {
                _creationException = ex;
                _ready.Set();
            }
        })
        {
            Name = "FormTestHost-STA",
            IsBackground = true
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        // Wait for the form to be shown (up to 10 seconds)
        if (!_ready.WaitOne(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Form did not show within 10 seconds.");
        }

        if (_creationException != null)
        {
            throw new InvalidOperationException(
                "Form creation failed on STA thread.", _creationException);
        }
    }

    // ========================================================================
    // Thread-safe control access via Invoke
    // ========================================================================

    /// <summary>Invokes an action on the UI thread.</summary>
    public void InvokeOnUI(Action action)
    {
        if (HostForm.IsDisposed) return;
        HostForm.Invoke(action);
    }

    /// <summary>Invokes a function on the UI thread and returns the result.</summary>
    public T InvokeOnUI<T>(Func<T> func)
    {
        return (T)HostForm.Invoke(func);
    }

    /// <summary>
    /// Invokes an async Task method on the UI thread and awaits its completion.
    /// Use this instead of calling async methods directly from test threads to avoid
    /// cross-thread InvalidOperationExceptions on WinForms controls.
    /// </summary>
    public async Task InvokeAsync(Func<Task> asyncAction)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (HostForm.IsDisposed) return;
        HostForm.Invoke((Action)(async () =>
        {
            try
            {
                await asyncAction();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }));
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    // ========================================================================
    // Reflection-based field access
    // ========================================================================

    /// <summary>Gets a private field value from the control by reflection.</summary>
    public TField GetField<TField>(string fieldName) where TField : class
    {
        return InvokeOnUI(() =>
        {
            var field = typeof(TForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{fieldName}' not found on {typeof(TForm).Name}.");
            return (TField?)field.GetValue(_control)
                ?? throw new InvalidOperationException($"Field '{fieldName}' is null.");
        });
    }

    /// <summary>Gets a nested field from a parent field by traversing the path.</summary>
    public TField GetNestedField<TField>(string parentFieldName, string childFieldName) where TField : class
    {
        return InvokeOnUI(() =>
        {
            var parent = typeof(TForm).GetField(parentFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{parentFieldName}' not found.");
            var parentValue = parent.GetValue(_control)
                ?? throw new InvalidOperationException($"Field '{parentFieldName}' is null.");
            var child = parentValue.GetType().GetField(childFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{childFieldName}' not found on '{parentValue.GetType().Name}'.");
            return (TField?)child.GetValue(parentValue)
                ?? throw new InvalidOperationException($"Field '{childFieldName}' is null.");
        });
    }

    // ========================================================================
    // Common UI interactions
    // ========================================================================

    /// <summary>Clicks a Button field by name.</summary>
    public void ClickButton(string fieldName)
    {
        InvokeOnUI(() =>
        {
            var btn = GetField<Button>(fieldName);
            btn.PerformClick();
        });
    }

    /// <summary>Sets Text on a TextBox or RtlTextBox field by name.</summary>
    public void SetTextBox(string fieldName, string text)
    {
        InvokeOnUI(() =>
        {
            var field = typeof(TForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{fieldName}' not found.");
            var value = field.GetValue(_control)
                ?? throw new InvalidOperationException($"Field '{fieldName}' is null.");
            switch (value)
            {
                case TextBox tb:
                    tb.Text = text;
                    break;
                case POS.Desktop.CustomControls.RtlTextBox rtb:
                    rtb.Text = text;
                    break;
                default:
                    throw new InvalidOperationException($"Field '{fieldName}' is not a text input control.");
            }
        });
    }

    /// <summary>Reads Text from a Label or Button field by name.</summary>
    public string GetText(string fieldName)
    {
        return InvokeOnUI(() =>
        {
            var field = typeof(TForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{fieldName}' not found.");
            var value = field.GetValue(_control);
            return value switch
            {
                Label lbl => lbl.Text,
                Button btn => btn.Text,
                TextBox tb => tb.Text,
                POS.Desktop.CustomControls.RtlTextBox rtb => rtb.Text,
                _ => value?.ToString() ?? ""
            };
        });
    }

    /// <summary>Checks if a control field is visible.</summary>
    public bool IsVisible(string fieldName)
    {
        return InvokeOnUI(() =>
        {
            var field = typeof(TForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{fieldName}' not found.");
            var value = field.GetValue(_control) as Control;
            return value?.Visible ?? false;
        });
    }

    /// <summary>Checks if a control field is enabled.</summary>
    public bool IsEnabled(string fieldName)
    {
        return InvokeOnUI(() =>
        {
            var field = typeof(TForm).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ArgumentException($"Field '{fieldName}' not found.");
            var value = field.GetValue(_control) as Control;
            return value?.Enabled ?? false;
        });
    }

    /// <summary>Gets the numeric value of a NumericUpDown field.</summary>
    public decimal GetNumericUpDownValue(string fieldName)
    {
        return InvokeOnUI(() =>
        {
            var nud = GetField<NumericUpDown>(fieldName);
            return nud.Value;
        });
    }

    // ========================================================================
    // Event monitoring helpers
    // ========================================================================

    /// <summary>
    /// Wires an event and waits for it to fire, returning the event args.
    /// Only works for EventHandler&lt;TEventArgs&gt; pattern.
    /// Uses 10 second default timeout to accommodate full test suite load.
    /// </summary>
    public async Task<TEventArgs?> AwaitEvent<TEventArgs>(
        string eventName, int timeoutMs = 10000) where TEventArgs : class
    {
        var tcs = new TaskCompletionSource<TEventArgs?>();

        // Subscribe to the event on the UI thread to avoid cross-thread issues
        // with WinForms controls when _control is a Form created on the UI thread.
        InvokeOnUI(() =>
        {
            var eventInfo = typeof(TForm).GetEvent(eventName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"Event '{eventName}' not found on {typeof(TForm).Name}.");

            EventHandler<TEventArgs>? handler = null;
            handler = (sender, args) =>
            {
                // Unsubscribe to avoid leaks
                try { eventInfo.RemoveEventHandler(_control, handler); } catch { }
                tcs.TrySetResult(args);
            };

            eventInfo.AddEventHandler(_control, handler);
        });

        using var cts = new CancellationTokenSource(timeoutMs);
        using var registration = cts.Token.Register(() =>
            tcs.TrySetResult(null));

        return await tcs.Task;
    }

    /// <summary>Waits for an EventHandler (non-generic) to fire.</summary>
    public async Task<bool> AwaitSimpleEvent(string eventName, int timeoutMs = 10000)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Subscribe on the UI thread to avoid cross-thread issues
        InvokeOnUI(() =>
        {
            var eventInfo = typeof(TForm).GetEvent(eventName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"Event '{eventName}' not found.");

            EventHandler? handler = null;
            handler = (sender, args) =>
            {
                try { eventInfo.RemoveEventHandler(_control, handler); } catch { }
                tcs.TrySetResult(true);
            };

            eventInfo.AddEventHandler(_control, handler);
        });

        using var cts = new CancellationTokenSource(timeoutMs);
        using var registration = cts.Token.Register(() =>
            tcs.TrySetResult(false));

        return await tcs.Task;
    }

    // ========================================================================
    // Cleanup
    // ========================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hostForm is { IsDisposed: false })
        {
            try
            {
                _hostForm.Invoke(() => _hostForm.Close());
            }
            catch
            {
                // Form might already be closing
            }
        }

        if (_uiThread.IsAlive)
        {
            // Send a WM_CLOSE to help the message pump shut down
            _uiThread.Join(TimeSpan.FromSeconds(3));
            if (_uiThread.IsAlive)
                _uiThread.Interrupt();
        }

        _control?.Dispose();
        _hostForm?.Dispose();
        _ready.Dispose();
    }
}

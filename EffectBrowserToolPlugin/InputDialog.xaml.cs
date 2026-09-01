using System.Windows;
using System.Windows.Input;

namespace EffectBrowserToolPlugin;

public partial class InputDialog : Window
{
    public string InputText => InputTextBox.Text;

    public InputDialog(string title, string message, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        InputTextBox.Text = defaultValue;
        InputTextBox.Focus();
        if (!string.IsNullOrEmpty(defaultValue))
        {
            InputTextBox.SelectAll();
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            Close();
        }
    }

    public static string? ShowDialog(Window owner, string title, string message, string defaultValue = "")
    {
        var dialog = new InputDialog(title, message, defaultValue)
        {
            Owner = owner
        };
        if (dialog.ShowDialog() == true)
        {
            return dialog.InputText;
        }
        return null;
    }
}

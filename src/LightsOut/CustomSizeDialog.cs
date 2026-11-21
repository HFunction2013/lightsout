using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LightsOut;

/// <summary>Small modal dialog asking for "WxH", returns a nullable tuple.</summary>
internal sealed class CustomSizeDialog : Window
{
    private const int MinSide = 2;
    private const int MaxSide = 40;

    private readonly TextBox _box = new();
    private readonly TextBlock _error = new();

    public CustomSizeDialog(int cols, int rows)
    {
        Title = "Board size";
        Width = 360;
        Height = 220;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1F212A"));

        var label = new TextBlock
        {
            Text = $"Board size, {MinSide} to {MaxSide}  (for example 9x9)",
            Foreground = new SolidColorBrush(Color.Parse("#B2B6C6")),
            Margin = new Thickness(0, 0, 0, 6)
        };

        _box.Text = $"{cols}x{rows}";
        _box.Width = 300;

        _error.Foreground = new SolidColorBrush(Color.Parse("#E06C6C"));
        _error.FontSize = 12;
        _error.Height = 18;

        var ok = new Button
        {
            Content = "OK",
            Width = 90,
            IsDefault = true
        };
        ok.Click += (_, _) => OnOk();

        var cancel = new Button
        {
            Content = "Cancel",
            Width = 90,
            IsCancel = true
        };
        cancel.Click += (_, _) => Close(null);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, ok }
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 6,
            Children = { label, _box, _error, buttons }
        };

        Content = panel;
        Opened += (_, _) => _box.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                OnOk();
                e.Handled = true;
            }
        };
    }

    private void OnOk()
    {
        string text = (_box.Text ?? string.Empty)
            .Replace('x', ' ').Replace('X', ' ').Replace('*', ' ').Replace('×', ' ');
        string[] parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        int rows = 0;
        int cols = 0;
        if (parts.Length == 1 && int.TryParse(parts[0], out int both))
        {
            rows = cols = both;
        }
        else if (parts.Length == 2)
        {
            int.TryParse(parts[0], out rows);
            int.TryParse(parts[1], out cols);
        }
        else
        {
            _error.Text = "Enter a size such as 7x7.";
            return;
        }

        if (rows < MinSide || cols < MinSide || rows > MaxSide || cols > MaxSide)
        {
            _error.Text = $"Each side must be between {MinSide} and {MaxSide}.";
            return;
        }

        Close(((int rows, int cols)?)(rows, cols));
    }
}

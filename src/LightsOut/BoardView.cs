using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace LightsOut;

/// <summary>
/// Draws the board and turns pointer input into cell presses.  All painting
/// is done with primitives so the control needs no XAML and stays friendly
/// to NativeAOT trimming.
/// </summary>
internal sealed class BoardView : Control
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#16171E"));
    private static readonly IBrush OnBrush = new SolidColorBrush(Color.Parse("#F6BA42"));
    private static readonly IBrush OnCoreBrush = new SolidColorBrush(Color.Parse("#FFEAB2"));
    private static readonly IBrush OffBrush = new SolidColorBrush(Color.Parse("#2D2F3C"));
    private static readonly IPen OnPen = new Pen(new SolidColorBrush(Color.Parse("#FFD174")), 1);
    private static readonly IPen OffPen = new Pen(new SolidColorBrush(Color.Parse("#3E4150")), 1);
    private static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.Parse("#96AAD2")), 2);
    private static readonly IPen HintPen = new Pen(new SolidColorBrush(Color.Parse("#60DEBA")), 2);
    private static readonly IPen FramePen = new Pen(new SolidColorBrush(Color.Parse("#3A3D4C")), 1);

    private int _rows = 5;
    private int _cols = 5;
    private byte[] _cells = Array.Empty<byte>();
    private byte[]? _hint;
    private int _hoverRow = -1;
    private int _hoverCol = -1;

    public event Action<int, int>? CellPressed;

    public void SetBoard(int rows, int cols, byte[] cells, byte[]? hint)
    {
        _rows = rows;
        _cols = cols;
        _cells = cells;
        _hint = hint;
        _hoverRow = -1;
        _hoverCol = -1;
        InvalidateVisual();
    }

    private bool GetLayout(out double cell, out double left, out double top)
    {
        cell = 0;
        left = 0;
        top = 0;
        if (_rows <= 0 || _cols <= 0)
            return false;

        double w = Bounds.Width;
        double h = Bounds.Height;
        double margin = 16;
        double availW = Math.Max(1, w - 2 * margin);
        double availH = Math.Max(1, h - 2 * margin);

        cell = Math.Min(availW / _cols, availH / _rows);
        if (cell < 8)
            cell = 8;
        left = (w - cell * _cols) / 2;
        top = margin + (availH - cell * _rows) / 2;
        if (top < margin)
            top = margin;
        return true;
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(BackgroundBrush, new Rect(Bounds.Size));
        if (!GetLayout(out double cell, out double left, out double top))
            return;

        ctx.DrawRectangle(null, FramePen,
            new Rect(left - 2, top - 2, cell * _cols + 4, cell * _rows + 4));

        bool haveCells = _cells.Length == _rows * _cols;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                var rect = new Rect(left + c * cell, top + r * cell, cell, cell);
                var body = rect.Deflate(new Thickness(2));
                double rad = Math.Max(2, cell / 5);

                bool lit = haveCells && _cells[r * _cols + c] != 0;
                bool hint = _hint is { } hb && hb[r * _cols + c] != 0;

                if (lit)
                {
                    ctx.DrawRectangle(OnBrush, OnPen, new RoundedRect(body, rad));
                    double inset = Math.Min(3, cell / 8);
                    if (inset > 0)
                    {
                        var core = body.Deflate(new Thickness(inset));
                        if (core.Width > 1 && core.Height > 1)
                            ctx.DrawRectangle(OnCoreBrush, OnPen,
                                new RoundedRect(core, Math.Max(1, rad / 2)));
                    }
                }
                else
                {
                    ctx.DrawRectangle(OffBrush, OffPen, new RoundedRect(body, rad));
                }

                if (r == _hoverRow && c == _hoverCol)
                    ctx.DrawRectangle(null, HoverPen,
                        new RoundedRect(body.Inflate(new Thickness(1)), rad));

                if (hint)
                {
                    double ring = Math.Max(3, cell / 6);
                    ctx.DrawEllipse(null, HintPen, body.Center, ring, ring);
                }
            }
        }
    }

    private bool HitTest(Point p, out int row, out int col)
    {
        row = -1;
        col = -1;
        if (!GetLayout(out double cell, out double left, out double top))
            return false;

        int c = (int)Math.Floor((p.X - left) / cell);
        int r = (int)Math.Floor((p.Y - top) / cell);
        if (c < 0 || c >= _cols || r < 0 || r >= _rows)
            return false;

        row = r;
        col = c;
        return true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;
        if (!HitTest(point.Position, out int r, out int c))
            return;

        e.Handled = true;
        CellPressed?.Invoke(r, c);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!HitTest(e.GetPosition(this), out int r, out int c))
        {
            r = -1;
            c = -1;
        }

        if (r != _hoverRow || c != _hoverCol)
        {
            _hoverRow = r;
            _hoverCol = c;
            Cursor = r >= 0 ? new Cursor(StandardCursorType.Hand) : Cursors.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverRow != -1 || _hoverCol != -1)
        {
            _hoverRow = -1;
            _hoverCol = -1;
            Cursor = Cursors.Default;
            InvalidateVisual();
        }
    }
}

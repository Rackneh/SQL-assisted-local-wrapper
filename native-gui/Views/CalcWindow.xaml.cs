using System.Globalization;
using System.Windows.Controls;

namespace DoryNative.Views;

/// <summary>Windows XP Calculator (standard view).</summary>
public partial class CalcWindow : UserControl
{
    private double _acc;
    private string _pendingOp = "";
    private bool _freshEntry = true;

    public CalcWindow()
    {
        InitializeComponent();
        // XpButtonStyle lives in XpDesktop resources; provide fallback inline styles
        foreach (var b in FindVisualChildren<Button>(this))
        {
            if (b.Style == null)
            {
                b.Padding = new System.Windows.Thickness(4, 2, 4, 2);
                b.Margin = new System.Windows.Thickness(2);
                b.Background = System.Windows.Media.Brushes.White;
                b.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x71, 0x6F, 0x64));
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject dep) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(dep); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(dep, i);
            if (child is T t) yield return t;
            foreach (var d in FindVisualChildren<T>(child)) yield return d;
        }
    }

    private void Btn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tag) return;
        try
        {
            if (tag.Length == 1 && char.IsDigit(tag[0])) { Digit(tag[0]); return; }
            switch (tag)
            {
                case ".": Dot(); break;
                case "+": case "-": case "*": case "/": Op(tag); break;
                case "=": Equals(); break;
                case "c": Display.Text = "0"; _acc = 0; _pendingOp = ""; _freshEntry = true; break;
                case "ce": Display.Text = "0"; _freshEntry = true; break;
                case "back": Back(); break;
                case "neg": Negate(); break;
                case "%": Percent(); break;
                case "sqrt": Sqrt(); break;
                case "inv": Inv(); break;
            }
        }
        catch { Display.Text = "Cannot divide by zero"; _freshEntry = true; }
    }

    private double Cur() => double.TryParse(Display.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void Digit(char c)
    {
        if (_freshEntry) { Display.Text = c.ToString(); _freshEntry = false; }
        else if (Display.Text.Replace("-", "").Replace(".", "").Length < 16)
            Display.Text += c;
    }

    private void Dot()
    {
        if (_freshEntry) { Display.Text = "0."; _freshEntry = false; }
        else if (!Display.Text.Contains('.')) Display.Text += ".";
    }

    private void Op(string op)
    {
        if (_pendingOp.Length > 0 && !_freshEntry) Equals();
        _acc = Cur(); _pendingOp = op; _freshEntry = true;
    }

    private void Equals()
    {
        if (_pendingOp.Length == 0) return;
        double b = Cur(), r = _pendingOp switch
        {
            "+" => _acc + b, "-" => _acc - b, "*" => _acc * b, "/" => _acc / b, _ => b
        };
        Display.Text = r.ToString("G15", CultureInfo.InvariantCulture);
        _pendingOp = ""; _freshEntry = true;
    }

    private void Back()
    {
        var t = Display.Text;
        Display.Text = t.Length <= 1 || (t.Length == 2 && t[0] == '-') ? "0" : t[..^1];
    }

    private void Negate()
    {
        if (Cur() != 0) Display.Text = (-Cur()).ToString(CultureInfo.InvariantCulture);
    }

    private void Percent() => Display.Text = (Cur() / 100).ToString("G15", CultureInfo.InvariantCulture);
    private void Sqrt() { var v = Cur(); if (v >= 0) Display.Text = Math.Sqrt(v).ToString("G15", CultureInfo.InvariantCulture); }
    private void Inv() { var v = Cur(); if (v != 0) Display.Text = (1 / v).ToString("G15", CultureInfo.InvariantCulture); }
}

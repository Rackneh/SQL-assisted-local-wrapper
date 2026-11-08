using MessageBox = DoryNative.XpSim.SimMessageBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DoryNative.XpSim;

/// <summary>
/// XP welcome/logon screen: user cards, optional password row,
/// credentials post-it note, turn-off button.
/// </summary>
public partial class LogonWindow : Window
{
    private readonly XpSetupConfig _cfg;
    private readonly Action<XpSetupConfig>? _onLoggedOn;
    private readonly Action? _onTurnOff;
    private bool _loggingIn;

    public LogonWindow(XpSetupConfig cfg, Action<XpSetupConfig>? onLoggedOn = null, Action? onTurnOff = null)
    {
        InitializeComponent();
        _cfg = cfg;
        _onLoggedOn = onLoggedOn;
        _onTurnOff = onTurnOff;
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        LogoImage.Source = XpAssets.Image("logon/bmp_100.png");
        SetTile(PrimaryAvatar, _cfg.UserTile);
        SetTile(GuestAvatar, "usertiles/guest.bmp");

        PrimaryName.Text = _cfg.UserName;
        PrimarySub.Text = $"Computer: {_cfg.ComputerName} • Online";
        PostItUser.Text = $"User: {_cfg.UserName}";
        PostItPw.Text = $"Password: {(_cfg.AdminPassword.Length > 0 ? _cfg.AdminPassword : "(No password set)")}";
        PostItComp.Text = $"Computer: {_cfg.ComputerName}";

        PrimaryCard.MouseLeftButtonDown += (_, _) => OnPrimaryClick();
        GuestCard.MouseLeftButtonDown += (_, _) => DoLogin();
        PwSubmit.Click += (_, _) => TryPassword();
        PwInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) TryPassword(); };
        TurnOffBtn.Click += (_, _) => _onTurnOff?.Invoke();
    }

    private static void SetTile(System.Windows.Controls.Image img, string tile)
    {
        // tile may be "usertiles/x.png" or a full "xp_assets/..." path
        var rel = tile.StartsWith("usertiles/") || tile.StartsWith("icons/") ? tile : "usertiles/" + tile;
        var src = XpAssets.ImageAuto(rel) ?? XpAssets.ImageAuto("usertiles/" + System.IO.Path.GetFileName(tile));
        if (src != null) img.Source = src;
    }

    private void OnPrimaryClick()
    {
        if (_loggingIn) return;
        if (_cfg.AdminPassword.Length > 0)
        {
            if (PwRow.Visibility == Visibility.Collapsed)
            {
                PwRow.Visibility = Visibility.Visible;
                PwInput.Password = _cfg.AdminPassword; // pre-fill like web GUI
                PwInput.Focus();
                PwInput.SelectAll();
            }
            else TryPassword();
        }
        else DoLogin();
    }

    private void TryPassword()
    {
        if (_cfg.AdminPassword.Length == 0 || PwInput.Password == _cfg.AdminPassword)
            DoLogin();
        else
        {
            XpAudio.Error();
            DoryNative.XpSim.SimMessageBox.Show(this, "The password you typed is incorrect. Please check the post-it note and try again.",
                "Logon Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DoLogin()
    {
        if (_loggingIn) return;
        _loggingIn = true;
        XpAudio.Logon();

        // "Loading your personal settings..."
        var sp = (System.Windows.Controls.StackPanel)PrimaryCard.Child;
        sp.Children.Clear();
        sp.Children.Add(new TextBlock
        {
            Text = "Loading your personal settings...",
            Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold,
            Padding = new Thickness(16)
        });

        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            Opacity = 0;
            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Close();
                _onLoggedOn?.Invoke(_cfg);
            };
            timer.Start();
        };
        t.Start();
    }
}

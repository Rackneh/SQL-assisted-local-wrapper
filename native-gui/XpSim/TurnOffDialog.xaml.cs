using System.Windows;

namespace DoryNative.XpSim;

public enum TurnOffAction { None, StandBy, TurnOff, Restart }

/// <summary>XP "Turn off computer" dialog: Stand By / Turn Off / Restart.</summary>
public partial class TurnOffDialog : Window
{
    public TurnOffAction Action { get; private set; } = TurnOffAction.None;

    public TurnOffDialog()
    {
        InitializeComponent();
        StandByImg.Source = XpAssets.Image("icons/xp_btn_standby.png");
        TurnOffImg.Source = XpAssets.Image("icons/xp_btn_turnoff.png");
        RestartImg.Source = XpAssets.Image("icons/xp_btn_restart.png");

        StandByBtn.Click += (_, _) => Pick(TurnOffAction.StandBy);
        TurnOffBtn.Click += (_, _) => Pick(TurnOffAction.TurnOff);
        RestartBtn.Click += (_, _) => Pick(TurnOffAction.Restart);
        CancelBtn.Click += (_, _) => { Action = TurnOffAction.None; Close(); };
    }

    private void Pick(TurnOffAction a)
    {
        Action = a;
        Close();
    }
}

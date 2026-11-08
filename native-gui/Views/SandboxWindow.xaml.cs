using MessageBox = DoryNative.XpSim.SimMessageBox;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DoryNative.Services;

namespace DoryNative.Views;

public partial class SandboxWindow : UserControl
{
    private readonly BackendApi _backend;

    public SandboxWindow(BackendApi backend)
    {
        InitializeComponent();
        _backend = backend;
        Loaded += async (s, e) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var status = await _backend.GetSandboxStatusAsync();
            StatusEnabled.Text = $"Enabled: {(status.Enabled ? "Yes" : "No")}";
            StatusContainer.Text = $"Container: {status.ContainerId ?? "—"}";
            StatusImage.Text = $"Image: {status.Image ?? "—"}";
            StatusState.Text = $"State: {status.Status}";

            MountList.ItemsSource = status.MountedPaths;

            if (status.Policy != null)
            {
                PolicyNetwork.IsChecked = GetBool(status.Policy, "network", "allow_network");
                PolicyFilesystem.IsChecked = GetBool(status.Policy, "filesystem", "allow_filesystem_write", "fs_write");
                PolicyPrivileged.IsChecked = GetBool(status.Policy, "privileged", "allow_privileged");
            }
        }
        catch (Exception ex)
        {
            StatusState.Text = $"State: error — {ex.Message}";
        }
    }

    private static bool GetBool(Dictionary<string, object> policy, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (policy.TryGetValue(key, out var v))
            {
                if (v is bool b) return b;
                if (v is JsonElement je && je.ValueKind == JsonValueKind.True) return true;
                if (v is string s && bool.TryParse(s, out var sb)) return sb;
            }
        }
        return false;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void ApplyPolicy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var policy = new Dictionary<string, object>
            {
                ["network"] = PolicyNetwork.IsChecked == true,
                ["filesystem"] = PolicyFilesystem.IsChecked == true,
                ["privileged"] = PolicyPrivileged.IsChecked == true
            };
            await _backend.SetSandboxPolicyAsync(policy);
            StatusState.Text = "State: policy applied";
        }
        catch (Exception ex)
        {
            DoryNative.XpSim.SimMessageBox.Show($"Failed to apply policy: {ex.Message}");
        }
    }
}

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using WLMRevivalV2Desktop.Protocol;
using WLMRevivalV2Desktop.Services;
using DoryNative.XpSim;
using MessageBox = DoryNative.XpSim.SimMessageBox;

namespace WLMRevivalV2Desktop.Views
{
    public partial class OptionsDialog : Window
    {
        private readonly MessengerService _messenger;

        private string _currentTab;

        // Personal
        private TextBox _displayNameBox;
        private TextBox _personalMessageBox;
        private CheckBox _nowPlayingCheck;
        private CheckBox _showDisplayPictureCheck;
        private CheckBox _awayWhenInactiveCheck;
        private TextBox _awayMinutesBox;
        private CheckBox _busyWhenFullscreenCheck;
        private CheckBox _showWebcamCheck;

        // Layout
        private CheckBox _showFavoritesCheck;
        private CheckBox _showGroupsCheck;
        private CheckBox _showTabBarCheck;
        private CheckBox _showWhatsNewListCheck;
        private ComboBox _labelContactsByCombo;
        private CheckBox _showStatusInLabelsCheck;
        private ComboBox _organizeContactsByCombo;
        private RadioButton _sortByStatusRadio;
        private RadioButton _sortAlphabeticallyRadio;
        private CheckBox _showOfflineContactsCheck;
        private CheckBox _showOfflineSeparateCheck;
        private CheckBox _showOfflineSmsSeparateCheck;
        private ComboBox _styleCombo;

        // Sign in
        private CheckBox _startWithWindowsCheck;
        private CheckBox _openMainWindowOnSignInCheck;
        private CheckBox _showOtherSignInLocationsCheck;
        private TextBox _signInLocationNameBox;

        // Sounds
        private CheckBox _muteAllSoundsCheck;
        private CheckBox _soundContactSignInCheck;
        private CheckBox _soundNewInstantMessageCheck;
        private CheckBox _soundNewEmailCheck;
        private CheckBox _soundIncomingCallCheck;
        private CheckBox _soundOutgoingCallCheck;
        private CheckBox _soundNewAlertCheck;
        private CheckBox _soundNudgeCheck;
        private CheckBox _soundMultipleChoiceQuestionCheck;
        private Dictionary<string, ComboBox> _soundSelectionCombos;

        // Alerts (existing)
        private CheckBox _showNotificationsCheck;

        // Privacy
        private CheckBox _dontSaveContactListCheck;
        private CheckBox _askPasswordForHotmailCheck;
        private CheckBox _allowVoiceClipsCheck;
        private CheckBox _openMessengerForWebsiteLinksCheck;
        private CheckBox _allowMicrosoftDataCollectionCheck;

        // IBA
        private TextBox _ibaUrlBox;
        private ComboBox _ibaCuriosityCombo;
        private TextBox _ibaQuestionDelayBox;
        private TextBox _ibaInitIntervalBox;
        private TextBox _ibaLogLimitBox;
        private TextBox _ibaApiBaseUrlBox;
        private TextBox _ibaModelBox;
        private TextBox _ibaSystemPromptBox;
        private TextBlock _ibaStatusText;
        private CheckBox _ibaShowReasoningCheck;
        private CheckBox _ibaAutoUncollapseToolsCheck;

        // Server (port 5000)
        private TextBox _gui5000UrlBox;
        private TextBlock _gui5000StatusText;
        private StackPanel _serverStatusPanel;
        private ItemsControl _profilesList;
        private ItemsControl _botsList;

        // Updates
        private CheckBox _updateAutoCheckCheck;
        private CheckBox _updateAutoDownloadCheck;
        private CheckBox _updateAutoInstallCheck;
        private ComboBox _updateChannelCombo;
        private TextBlock _updateStatusText;
        private TextBlock _updateCurrentVersionText;
        private Button _updateCheckNowButton;

        // Debug
        private CheckBox _debugModeCheck;
        private CheckBox _softErrorHandlingCheck;
        private TextBlock _debugStatusText;

        // Tailscale
        private CheckBox _tailscaleEnabledCheck;
        private TextBox _tailscaleAuthKeyBox;
        private TextBox _tailscaleServerHostnameBox;
        private TextBox _tailscaleServerPortBox;
        private TextBox _tailscaleLoginServerBox;
        private CheckBox _tailscaleAcceptRoutesCheck;
        private TextBox _tailscaleCliPathBox;
        private TextBlock _tailscaleStatusText;
        private Button _tailscaleTestButton;

        // PIP (subagent picture-in-picture)
        private RadioButton _pipBasicRadio;
        private RadioButton _pipAdvancedRadio;

        private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x6B, 0x7B));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        private static readonly Brush BorderBrushColor = new SolidColorBrush(Color.FromRgb(0xB8, 0xC8, 0xD8));
        private static readonly Brush GroupHeaderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

        public OptionsDialog() : this(null) { }

        public OptionsDialog(MessengerService messenger)
        {
            InitializeComponent();
            _messenger = messenger;
            UpdateContent("Personal");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptionsList.SelectedItem is ListBoxItem item)
            {
                CommitCurrentTab();
                UpdateContent(item.Content.ToString());
            }
        }

        private void UpdateContent(string header)
        {
            if (ContentPanel == null) return;

            ClearTabRefs();
            ContentPanel.Children.Clear();
            _currentTab = header;

            ContentPanel.Children.Add(new TextBlock
            {
                Text = header,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                Margin = new Thickness(0, 0, 0, 12)
            });

            switch (header)
            {
                case "Personal": AddPersonalContent(); break;
                case "Layout": AddLayoutContent(); break;
                case "Sign in": AddSignInContent(); break;
                case "Messages": AddMessagesContent(); break;
                case "Alerts": AddAlertsContent(); break;
                case "Sounds": AddSoundsContent(); break;
                case "File Transfer": AddFileTransferContent(); break;
                case "Privacy": AddPrivacyContent(); break;
                case "Security": AddSecurityContent(); break;
                case "Connection": AddConnectionContent(); break;
                case "IBA": AddIbaContent(); break;
                case "PIP": AddPipContent(); break;
                case "Server": AddServerContent(); break;
                case "Updates": AddUpdatesContent(); break;
                case "Debug": AddDebugContent(); break;
                default:
                    ContentPanel.Children.Add(Placeholder());
                    break;
            }
        }

        // ===== UI helpers =====
        private TextBlock Label(string text) => new TextBlock
        {
            Text = text, FontSize = 12, Foreground = LabelBrush, Margin = new Thickness(0, 0, 0, 4)
        };

        private TextBlock BoldLabel(string text) => new TextBlock
        {
            Text = text, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = TextBrush,
            Margin = new Thickness(0, 10, 0, 4)
        };

        private TextBlock GroupLabel(string text) => new TextBlock
        {
            Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = GroupHeaderBrush, Margin = new Thickness(0, 12, 0, 6)
        };

        private TextBox Field(string value) => new TextBox
        {
            Text = value ?? string.Empty,
            Height = 24, FontSize = 12, BorderBrush = BorderBrushColor,
            BorderThickness = new Thickness(1), Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(0, 0, 0, 10)
        };

        private TextBox SmallField(string value, int width) => new TextBox
        {
            Text = value ?? string.Empty,
            Width = width, Height = 24, FontSize = 12, BorderBrush = BorderBrushColor,
            BorderThickness = new Thickness(1), Padding = new Thickness(4, 2, 4, 2),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        private CheckBox Check(string text, bool isChecked) => new CheckBox
        {
            Content = text, FontSize = 12, Foreground = TextBrush,
            Margin = new Thickness(0, 5, 0, 5), IsChecked = isChecked
        };

        private CheckBox IndentedCheck(string text, bool isChecked) => new CheckBox
        {
            Content = text, FontSize = 12, Foreground = TextBrush,
            Margin = new Thickness(18, 3, 0, 3), IsChecked = isChecked
        };

        private ComboBox DropDown(string[] items, string selected)
        {
            var cb = new ComboBox
            {
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            foreach (var item in items) cb.Items.Add(item);
            cb.SelectedItem = selected;
            return cb;
        }

        private Button ActionButton(string text)
        {
            return new Button
            {
                Content = text,
                Style = (Style)FindResource("WlmDialogButton"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 10)
            };
        }

        private TextBlock Placeholder() => new TextBlock
        {
            Text = "There are no configurable options for this section on this revival server.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = LabelBrush
        };

        private Border Separator() => new Border
        {
            Height = 1,
            Background = BorderBrushColor,
            Margin = new Thickness(0, 8, 0, 8)
        };

        // ===== Personal =====
        private void AddPersonalContent()
        {
            string name = _messenger?.Owner?.Name ?? string.Empty;
            string pm = _messenger?.GetPersonalMessage() ?? string.Empty;

            ContentPanel.Children.Add(Label("Display Name:"));
            _displayNameBox = Field(name);
            ContentPanel.Children.Add(_displayNameBox);

            ContentPanel.Children.Add(Label("Type a personal message for your contacts to see:"));
            _personalMessageBox = Field(pm);
            ContentPanel.Children.Add(_personalMessageBox);

            _nowPlayingCheck = Check(
                "Show song information from Windows Media Player as a personal message",
                AppSettings.NowPlayingEnabled);
            ContentPanel.Children.Add(_nowPlayingCheck);

            ContentPanel.Children.Add(Separator());

            // Display Picture
            ContentPanel.Children.Add(BoldLabel("Display Picture"));
            var dpPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _showDisplayPictureCheck = Check(
                "Show my display picture and allow others to see it", AppSettings.ShowDisplayPicture);
            dpPanel.Children.Add(_showDisplayPictureCheck);
            var changePicBtn = ActionButton("Change Picture...");
            changePicBtn.Margin = new Thickness(15, 0, 0, 0);
            changePicBtn.Click += (s, e) =>
            {
                var dialog = new DisplayPictureDialog { Owner = this };
                if (dialog.ShowDialogSim() == true && !string.IsNullOrEmpty(dialog.SelectedPicturePath) && _messenger != null)
                {
                    try { _messenger.SetDisplayPicture(dialog.SelectedPicturePath); }
                    catch (Exception ex) { DoryNative.XpSim.SimMessageBox.Show("Could not change picture: " + ex.Message, "Windows Live Messenger"); }
                }
            };
            dpPanel.Children.Add(changePicBtn);
            ContentPanel.Children.Add(dpPanel);

            // Public Profile
            ContentPanel.Children.Add(BoldLabel("Public Profile"));
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Create or modify your public profile (the description of yourself that others can see).",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });
            var editProfileBtn = ActionButton("Edit Profile...");
            editProfileBtn.Click += (s, e) => DoryNative.XpSim.SimMessageBox.Show(
                "Public profiles are not available on this revival server.",
                "Windows Live Messenger", MessageBoxButton.OK, MessageBoxImage.Information);
            ContentPanel.Children.Add(editProfileBtn);

            ContentPanel.Children.Add(Separator());

            // Status
            ContentPanel.Children.Add(BoldLabel("Status"));
            var awayPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            _awayWhenInactiveCheck = new CheckBox
            {
                Content = "Show me as \"Away\" when I'm inactive for",
                FontSize = 12, Foreground = TextBrush,
                IsChecked = AppSettings.ShowAsAwayWhenInactive,
                VerticalAlignment = VerticalAlignment.Center
            };
            awayPanel.Children.Add(_awayWhenInactiveCheck);
            _awayMinutesBox = SmallField(AppSettings.AwayAfterMinutes.ToString(), 40);
            _awayMinutesBox.Margin = new Thickness(6, 0, 6, 0);
            _awayMinutesBox.VerticalAlignment = VerticalAlignment.Center;
            awayPanel.Children.Add(_awayMinutesBox);
            awayPanel.Children.Add(new TextBlock { Text = "minutes", FontSize = 12, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
            ContentPanel.Children.Add(awayPanel);

            _busyWhenFullscreenCheck = Check(
                "Show me as \"Busy\" and block my alerts when I'm running a full-screen program or presentation settings are turned on",
                AppSettings.ShowAsBusyWhenFullscreen);
            ContentPanel.Children.Add(_busyWhenFullscreenCheck);

            ContentPanel.Children.Add(Separator());

            // Webcam
            ContentPanel.Children.Add(BoldLabel("Webcam"));
            _showWebcamCheck = Check("Allow others to see that I have a webcam", AppSettings.ShowWebcamStatus);
            ContentPanel.Children.Add(_showWebcamCheck);
        }

        // ===== Layout =====
        private void AddLayoutContent()
        {
            // Layout checkboxes
            _showFavoritesCheck = Check("Show favorites", AppSettings.ShowFavorites);
            _showGroupsCheck = Check("Show groups", AppSettings.ShowGroups);
            _showTabBarCheck = Check("Show the tab bar", AppSettings.ShowTabBar);
            _showWhatsNewListCheck = Check("Show the what's new list", AppSettings.ShowWhatsNewList);
            ContentPanel.Children.Add(_showFavoritesCheck);
            ContentPanel.Children.Add(_showGroupsCheck);
            ContentPanel.Children.Add(_showTabBarCheck);
            ContentPanel.Children.Add(_showWhatsNewListCheck);

            var whatsNewBtn = ActionButton("What's new list options...");
            whatsNewBtn.Margin = new Thickness(18, 0, 0, 10);
            whatsNewBtn.HorizontalAlignment = HorizontalAlignment.Left;
            whatsNewBtn.Click += (s, e) => DoryNative.XpSim.SimMessageBox.Show(
                "What's new list options are not configurable on this revival server.",
                "Windows Live Messenger", MessageBoxButton.OK, MessageBoxImage.Information);
            ContentPanel.Children.Add(whatsNewBtn);

            ContentPanel.Children.Add(Separator());

            // Contact list section
            ContentPanel.Children.Add(BoldLabel("Contact list"));

            ContentPanel.Children.Add(Label("Favorites"));
            ContentPanel.Children.Add(ViewStyleRow(AppSettings.FavoritesViewStyle, v => AppSettings.FavoritesViewStyle = v, isFavorites: true));

            ContentPanel.Children.Add(Label("Other contacts"));
            ContentPanel.Children.Add(ViewStyleRow(AppSettings.OtherContactsViewStyle, v => AppSettings.OtherContactsViewStyle = v, isFavorites: false));

            ContentPanel.Children.Add(Label("Label contacts by:"));
            _labelContactsByCombo = DropDown(new[] { "Display name", "Nickname", "E-mail address" }, AppSettings.LabelContactsBy);
            ContentPanel.Children.Add(_labelContactsByCombo);

            _showStatusInLabelsCheck = Check("Show status information in labels", AppSettings.ShowStatusInLabels);
            ContentPanel.Children.Add(_showStatusInLabelsCheck);

            ContentPanel.Children.Add(Label("Organize contacts by:"));
            _organizeContactsByCombo = DropDown(new[] { "Categories", "Online/Offline", "None" }, AppSettings.OrganizeContactsBy);
            ContentPanel.Children.Add(_organizeContactsByCombo);

            ContentPanel.Children.Add(Label("Sort contacts within categories:"));
            var sortPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _sortByStatusRadio = new RadioButton
            {
                Content = "By status", FontSize = 12, Foreground = TextBrush,
                Margin = new Thickness(0, 0, 15, 0),
                IsChecked = AppSettings.SortContactsBy == "Status"
            };
            _sortAlphabeticallyRadio = new RadioButton
            {
                Content = "Alphabetically", FontSize = 12, Foreground = TextBrush,
                IsChecked = AppSettings.SortContactsBy == "Alphabetically"
            };
            sortPanel.Children.Add(_sortByStatusRadio);
            sortPanel.Children.Add(_sortAlphabeticallyRadio);
            ContentPanel.Children.Add(sortPanel);

            _showOfflineContactsCheck = Check("Show offline contacts", AppSettings.ShowOfflineContacts);
            _showOfflineSeparateCheck = IndentedCheck("Show offline contacts in a separate category", AppSettings.ShowOfflineInSeparateCategory);
            _showOfflineSmsSeparateCheck = IndentedCheck("Show \"Offline, SMS\" contacts in a separate category", AppSettings.ShowOfflineSmsInSeparateCategory);
            ContentPanel.Children.Add(_showOfflineContactsCheck);
            ContentPanel.Children.Add(_showOfflineSeparateCheck);
            ContentPanel.Children.Add(_showOfflineSmsSeparateCheck);

            ContentPanel.Children.Add(Separator());

            // ---- Messenger style ----
            ContentPanel.Children.Add(BoldLabel("Messenger style"));
            ContentPanel.Children.Add(Label("Choose the overall look of the contact list:"));
            _styleCombo = new ComboBox
            {
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _styleCombo.Items.Add("WLM2009");
            _styleCombo.Items.Add("WLMLive");
            _styleCombo.SelectedIndex = AppSettings.UiStyle == "WLMLive" ? 1 : 0;
            ContentPanel.Children.Add(_styleCombo);
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "The new style is applied when you click OK.",
                FontSize = 11,
                Foreground = LabelBrush,
                Margin = new Thickness(0, 0, 0, 12)
            });
        }

        private StackPanel ViewStyleRow(string current, Action<string> onChanged, bool isFavorites)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var sizes = new[] { "Large", "Medium", "Small" };
            foreach (var size in sizes)
            {
                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
                var radio = new RadioButton
                {
                    FontSize = 12,
                    Foreground = TextBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = current == size
                };
                radio.Checked += (s, e) => onChanged(size);
                itemPanel.Children.Add(radio);
                itemPanel.Children.Add(ViewStylePreview(size, isFavorites));
                panel.Children.Add(itemPanel);
            }
            return panel;
        }

        private Grid ViewStylePreview(string size, bool isFavorites)
        {
            double frameSize = size switch
            {
                "Large" => isFavorites ? 28 : 24,
                "Medium" => isFavorites ? 22 : 18,
                _ => isFavorites ? 18 : 14
            };

            var grid = new Grid { Width = frameSize + 4, Height = frameSize + 4, Margin = new Thickness(4, 0, 0, 0) };

            if (size == "Small" && !isFavorites)
            {
                // Small other contacts = status dot
                grid.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Assets/wlm/status_online_dot.png")),
                    Width = 10, Height = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            else
            {
                // Avatar preview with frame
                var frame = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Assets/wlm/status_frame_online_small.png")),
                    Stretch = Stretch.Fill,
                    Width = frameSize, Height = frameSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Panel.SetZIndex(frame, 0);

                double tileSize = Math.Round(frameSize * 0.62);
                var tile = new Border
                {
                    Width = tileSize, Height = tileSize,
                    CornerRadius = new CornerRadius(1),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Clip = new RectangleGeometry(new Rect(0, 0, tileSize, tileSize), 1, 1)
                };
                tile.Child = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Assets/wlm/usertile_default.png")),
                    Stretch = Stretch.UniformToFill
                };
                Panel.SetZIndex(tile, 1);

                grid.Children.Add(frame);
                grid.Children.Add(tile);
            }

            return grid;
        }

        // ===== Sign in =====
        private void AddSignInContent()
        {
            ContentPanel.Children.Add(GroupLabel("General"));
            _startWithWindowsCheck = Check("Start Messenger when I log on to my computer", AppSettings.StartWithWindows);
            _openMainWindowOnSignInCheck = Check("Open the main window when Messenger signs in", AppSettings.OpenMainWindowOnSignIn);
            ContentPanel.Children.Add(_startWithWindowsCheck);
            ContentPanel.Children.Add(_openMainWindowOnSignInCheck);

            ContentPanel.Children.Add(GroupLabel("Sign in at more than one place"));
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "You can sign in to Messenger from more than one place and receive messages everywhere you're signed in.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });
            _showOtherSignInLocationsCheck = Check(
                "Show me all the other places I'm signed in at when I sign in",
                AppSettings.ShowOtherSignInLocations);
            ContentPanel.Children.Add(_showOtherSignInLocationsCheck);

            ContentPanel.Children.Add(Label("Name:"));
            _signInLocationNameBox = Field(AppSettings.SignInLocationName);
            ContentPanel.Children.Add(_signInLocationNameBox);
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Note: This name is only used to identify this location in Messenger.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap
            });
        }

        // ===== Messages =====
        private void AddMessagesContent()
        {
            ContentPanel.Children.Add(Placeholder());
        }

        // ===== Alerts =====
        private void AddAlertsContent()
        {
            _showNotificationsCheck = Check(
                "Show me an alert when a contact comes online", AppSettings.ShowNotifications);
            ContentPanel.Children.Add(_showNotificationsCheck);
        }

        // ===== Sounds =====
        private void AddSoundsContent()
        {
            _muteAllSoundsCheck = Check("Mute all sounds", AppSettings.MuteAllSounds);
            ContentPanel.Children.Add(_muteAllSoundsCheck);

            ContentPanel.Children.Add(BoldLabel("Messenger Sounds"));
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Turn each event on or off, then pick which sound plays. Choose a built-in sound, ▶ to preview, or … to use your own file.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _soundSelectionCombos = new Dictionary<string, ComboBox>(StringComparer.OrdinalIgnoreCase);

            var available = SoundManager.AvailableSounds();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            for (int i = 0; i < SoundManager.Events.Length; i++)
            {
                var ev = SoundManager.Events[i];
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var cb = new CheckBox
                {
                    Content = ev.Label,
                    FontSize = 12,
                    Foreground = TextBrush,
                    IsChecked = EnableInitial(ev.Key),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5),
                    ToolTip = ev.Hint
                };
                StoreEnableRef(i, cb);
                Grid.SetRow(cb, i); Grid.SetColumn(cb, 0);
                grid.Children.Add(cb);

                var combo = BuildSoundCombo(ev, available);
                combo.VerticalAlignment = VerticalAlignment.Center;
                combo.Margin = new Thickness(0, 4, 6, 4);
                combo.MinWidth = 150;
                Grid.SetRow(combo, i); Grid.SetColumn(combo, 1);
                grid.Children.Add(combo);
                _soundSelectionCombos[ev.Key] = combo;

                var play = new Button
                {
                    Content = "▶",
                    Style = (Style)FindResource("WlmDialogButton"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 4, 4),
                    Padding = new Thickness(8, 0, 8, 0),
                    ToolTip = "Preview"
                };
                string key = ev.Key;
                play.Click += (s, e) => PreviewCombo(key);
                Grid.SetRow(play, i); Grid.SetColumn(play, 2);
                grid.Children.Add(play);

                var browse = new Button
                {
                    Content = "…",
                    Style = (Style)FindResource("WlmDialogButton"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 4),
                    Padding = new Thickness(8, 0, 8, 0),
                    ToolTip = "Choose a sound file..."
                };
                var targetCombo = combo;
                browse.Click += (s, e) => BrowseForSound(targetCombo);
                Grid.SetRow(browse, i); Grid.SetColumn(browse, 3);
                grid.Children.Add(browse);
            }

            ContentPanel.Children.Add(grid);

            ContentPanel.Children.Add(Separator());

            ContentPanel.Children.Add(BoldLabel("Sounds for your contacts"));
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "You can choose the sounds you hear when a contact signs in or sends you an instant message. In the main window, right-click on a contact and select \"Choose sounds for this contact\".",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap
            });
        }

        private bool EnableInitial(string key)
        {
            switch (key)
            {
                case "ContactSignIn":     return AppSettings.SoundContactSignIn;
                case "NewInstantMessage": return AppSettings.SoundNewInstantMessage;
                case "NewEmail":          return AppSettings.SoundNewEmail;
                case "IncomingCall":      return AppSettings.SoundIncomingCall;
                case "OutgoingCall":      return AppSettings.SoundOutgoingCall;
                case "NewAlert":          return AppSettings.SoundNewAlert;
                case "Nudge":             return AppSettings.SoundNudge;
                case "MultipleChoiceQuestion": return AppSettings.SoundMultipleChoiceQuestion;
                default:                  return true;
            }
        }

        private void StoreEnableRef(int i, CheckBox cb)
        {
            switch (i)
            {
                case 0: _soundContactSignInCheck = cb; break;
                case 1: _soundNewInstantMessageCheck = cb; break;
                case 2: _soundNewEmailCheck = cb; break;
                case 3: _soundIncomingCallCheck = cb; break;
                case 4: _soundOutgoingCallCheck = cb; break;
                case 5: _soundNewAlertCheck = cb; break;
                case 6: _soundNudgeCheck = cb; break;
                case 7: _soundMultipleChoiceQuestionCheck = cb; break;
            }
        }

        private ComboBox BuildSoundCombo(SoundEvent ev, List<SoundChoice> available)
        {
            var combo = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };

            combo.Items.Add(new SoundChoice { Id = string.Empty, Label = "Default (" + SoundManager.Prettify(ev.DefaultFile) + ")" });
            combo.Items.Add(new SoundChoice { Id = SoundManager.NoneId, Label = "(None) – silent" });
            foreach (var c in available)
                combo.Items.Add(c);

            string saved = AppSettings.GetSoundSelection(ev.Key, string.Empty) ?? string.Empty;

            int selectedIndex = 0;
            if (string.Equals(saved, SoundManager.NoneId, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = 1;
            }
            else if (!string.IsNullOrEmpty(saved))
            {
                int found = -1;
                for (int k = 0; k < combo.Items.Count; k++)
                    if (combo.Items[k] is SoundChoice sc && !string.IsNullOrEmpty(sc.Id) &&
                        string.Equals(sc.Id, saved, StringComparison.OrdinalIgnoreCase))
                    { found = k; break; }

                if (found < 0)
                {
                    var custom = new SoundChoice { Id = saved, Label = "Custom: " + System.IO.Path.GetFileName(saved) };
                    combo.Items.Add(custom);
                    found = combo.Items.Count - 1;
                }
                selectedIndex = found;
            }

            combo.SelectedIndex = selectedIndex;
            return combo;
        }

        private void PreviewCombo(string eventKey)
        {
            if (_soundSelectionCombos == null || !_soundSelectionCombos.TryGetValue(eventKey, out var combo))
                return;
            var sc = combo.SelectedItem as SoundChoice;
            if (sc == null) { SoundManager.Preview(eventKey); return; }
            if (string.IsNullOrEmpty(sc.Id)) SoundManager.Preview(eventKey);
            else SoundManager.PreviewSelection(sc.Id);
        }

        private void BrowseForSound(ComboBox combo)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Choose a sound file",
                Filter = "Audio files (*.mp3;*.wma;*.wav)|*.mp3;*.wma;*.wav|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != true) return;

            string path = dlg.FileName;
            for (int k = 0; k < combo.Items.Count; k++)
                if (combo.Items[k] is SoundChoice sc && string.Equals(sc.Id, path, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = k;
                    return;
                }

            var custom = new SoundChoice { Id = path, Label = "Custom: " + System.IO.Path.GetFileName(path) };
            combo.Items.Add(custom);
            combo.SelectedItem = custom;
        }
        // ===== File Transfer =====
        private void AddFileTransferContent()
        {
            ContentPanel.Children.Add(Placeholder());
        }

        // ===== Privacy =====
        private void AddPrivacyContent()
        {
            ContentPanel.Children.Add(GroupLabel("Security"));
            _dontSaveContactListCheck = Check(
                "Don't save my contact list or social updates on this computer",
                AppSettings.DontSaveContactListLocally);
            _askPasswordForHotmailCheck = Check(
                "Ask for my password when I go to Hotmail and other websites that require a Windows Live ID",
                AppSettings.AskPasswordForHotmail);
            _allowVoiceClipsCheck = Check(
                "Allow voice clips to be sent, received, and temporarily stored on my computer",
                AppSettings.AllowVoiceClips);
            _openMessengerForWebsiteLinksCheck = Check(
                "Open Messenger when I open a link to a website that tries to interact with Messenger",
                AppSettings.OpenMessengerForWebsiteLinks);
            ContentPanel.Children.Add(_dontSaveContactListCheck);
            ContentPanel.Children.Add(_askPasswordForHotmailCheck);
            ContentPanel.Children.Add(_allowVoiceClipsCheck);
            ContentPanel.Children.Add(_openMessengerForWebsiteLinksCheck);

            ContentPanel.Children.Add(GroupLabel("Settings"));
            var settingsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            settingsPanel.Children.Add(new TextBlock
            {
                Text = "Edit your privacy settings, including who can send you messages and what you share with your friends.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Width = 280, VerticalAlignment = VerticalAlignment.Center
            });
            var editSettingsBtn = ActionButton("Edit settings");
            editSettingsBtn.Margin = new Thickness(10, 0, 0, 0);
            editSettingsBtn.Click += (s, e) => DoryNative.XpSim.SimMessageBox.Show(
                "Privacy settings editing is not available on this revival server.",
                "Windows Live Messenger", MessageBoxButton.OK, MessageBoxImage.Information);
            settingsPanel.Children.Add(editSettingsBtn);
            ContentPanel.Children.Add(settingsPanel);

            ContentPanel.Children.Add(GroupLabel("Help improve our products and services"));
            _allowMicrosoftDataCollectionCheck = Check(
                "Allowing Microsoft to collect data about your computer and how you use Windows Live helps us improve our products and services. It may also be used to personalize content for you, but it won't be used to contact you.",
                AppSettings.AllowMicrosoftDataCollection);
            ContentPanel.Children.Add(_allowMicrosoftDataCollectionCheck);
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Changes to this setting don't apply to Bing Bar.",
                FontSize = 11, Foreground = LabelBrush, Margin = new Thickness(0, 2, 0, 8)
            });
            var moreInfoBtn = ActionButton("More information");
            moreInfoBtn.Click += (s, e) => DoryNative.XpSim.SimMessageBox.Show(
                "Data collection is not implemented in this revival client.",
                "Windows Live Messenger", MessageBoxButton.OK, MessageBoxImage.Information);
            ContentPanel.Children.Add(moreInfoBtn);
        }

        // ===== Security =====
        private void AddSecurityContent()
        {
            ContentPanel.Children.Add(Placeholder());
        }

        // ===== Connection =====
        private void AddConnectionContent()
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Connect to the revival server through a Tailscale or Headscale mesh VPN. " +
                       "This lets the server run behind NAT without port forwarding.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _tailscaleEnabledCheck = Check("Use Tailscale / Headscale", AppSettings.TailscaleEnabled);
            ContentPanel.Children.Add(_tailscaleEnabledCheck);

            ContentPanel.Children.Add(Label("Server hostname (MagicDNS name, e.g. wlm-server):"));
            _tailscaleServerHostnameBox = Field(AppSettings.TailscaleServerHostname);
            ContentPanel.Children.Add(_tailscaleServerHostnameBox);

            ContentPanel.Children.Add(Label("MSN nameserver port:"));
            _tailscaleServerPortBox = SmallField(AppSettings.TailscaleServerPort.ToString(), 80);
            ContentPanel.Children.Add(_tailscaleServerPortBox);

            ContentPanel.Children.Add(Label("Auth key (pre-authentication key from Tailscale/Headscale console):"));
            _tailscaleAuthKeyBox = Field(AppSettings.TailscaleAuthKey);
            _tailscaleAuthKeyBox.FontFamily = new FontFamily("Consolas");
            ContentPanel.Children.Add(_tailscaleAuthKeyBox);

            ContentPanel.Children.Add(Label("Login server (leave blank for Tailscale SaaS, or set Headscale URL):"));
            _tailscaleLoginServerBox = Field(AppSettings.TailscaleLoginServer);
            ContentPanel.Children.Add(_tailscaleLoginServerBox);

            _tailscaleAcceptRoutesCheck = Check("Accept routes advertised by the tailnet", AppSettings.TailscaleAcceptRoutes);
            ContentPanel.Children.Add(_tailscaleAcceptRoutesCheck);

            ContentPanel.Children.Add(Label("Tailscale CLI path (optional; auto-detected if blank):"));
            _tailscaleCliPathBox = Field(AppSettings.TailscaleCliPath);
            ContentPanel.Children.Add(_tailscaleCliPathBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
            _tailscaleTestButton = ActionButton("Test / connect now");
            _tailscaleTestButton.Click += async (s, e) => await TestTailscaleAsync();
            buttons.Children.Add(_tailscaleTestButton);
            ContentPanel.Children.Add(buttons);

            _tailscaleStatusText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 11,
                Foreground = LabelBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            ContentPanel.Children.Add(_tailscaleStatusText);

            _tailscaleEnabledCheck.Checked += (s, e) => _tailscaleStatusText.Text = "Tailscale enabled. Click Test to connect.";
            _tailscaleEnabledCheck.Unchecked += (s, e) => _tailscaleStatusText.Text = "Tailscale disabled.";
        }

        private async Task TestTailscaleAsync()
        {
            CommitCurrentTab();
            _tailscaleStatusText.Text = "Connecting to Tailscale...";
            _tailscaleTestButton.IsEnabled = false;
            try
            {
                var ts = new TailscaleService();
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35)))
                {
                    var ok = await ts.EnsureConnectedAsync(cts.Token);
                    if (!ok)
                    {
                        _tailscaleStatusText.Text = "Connection failed: " + ts.LastError;
                        return;
                    }
                    var endpoint = ts.GetMsnServerEndpoint();
                    var ip = ts.ResolveServerIp();
                    _tailscaleStatusText.Text = $"Connected. Tailnet: {ts.TailnetName}\n" +
                                                $"Server endpoint: {endpoint ?? "(unresolved)"}\n" +
                                                $"Resolved IP: {ip ?? "(none)"}";
                }
            }
            catch (Exception ex)
            {
                _tailscaleStatusText.Text = "Error: " + ex.Message;
            }
            finally
            {
                _tailscaleTestButton.IsEnabled = true;
            }
        }

        // ===== IBA =====
        private void AddIbaContent()
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Configure the local IBA backend through its GUI settings API.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            ContentPanel.Children.Add(Label("IBA GUI URL:"));
            _ibaUrlBox = Field(AppSettings.IbaGuiUrl);
            ContentPanel.Children.Add(_ibaUrlBox);

            var loadBtn = ActionButton("Load settings");
            loadBtn.Click += async (s, e) => await LoadIbaSettingsAsync();
            ContentPanel.Children.Add(loadBtn);

            ContentPanel.Children.Add(Separator());
            ContentPanel.Children.Add(BoldLabel("IBA Settings"));

            ContentPanel.Children.Add(Label("Curiosity level:"));
            _ibaCuriosityCombo = DropDown(new[] { "Low", "Normal", "High", "Extreme" }, "Normal");
            ContentPanel.Children.Add(_ibaCuriosityCombo);

            _ibaShowReasoningCheck = Check("Show reasoning summaries", AppSettings.ShowReasoningSummaries);
            ContentPanel.Children.Add(_ibaShowReasoningCheck);

            _ibaAutoUncollapseToolsCheck = Check("Auto-uncollapse tool-usage cards", AppSettings.AutoUncollapseToolUsage);
            ContentPanel.Children.Add(_ibaAutoUncollapseToolsCheck);

            ContentPanel.Children.Add(Label("Question delay (seconds):"));
            _ibaQuestionDelayBox = SmallField("10", 80);
            ContentPanel.Children.Add(_ibaQuestionDelayBox);

            ContentPanel.Children.Add(Label("Self-init interval (seconds):"));
            _ibaInitIntervalBox = SmallField("300", 80);
            ContentPanel.Children.Add(_ibaInitIntervalBox);

            ContentPanel.Children.Add(Label("Log limit:"));
            _ibaLogLimitBox = SmallField("5000", 80);
            ContentPanel.Children.Add(_ibaLogLimitBox);

            ContentPanel.Children.Add(Label("API base URL:"));
            _ibaApiBaseUrlBox = Field(string.Empty);
            ContentPanel.Children.Add(_ibaApiBaseUrlBox);

            ContentPanel.Children.Add(Label("Model:"));
            _ibaModelBox = Field(string.Empty);
            ContentPanel.Children.Add(_ibaModelBox);

            ContentPanel.Children.Add(Label("System prompt:"));
            _ibaSystemPromptBox = new TextBox
            {
                Text = string.Empty,
                Height = 80,
                FontSize = 12,
                BorderBrush = BorderBrushColor,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            ContentPanel.Children.Add(_ibaSystemPromptBox);

            var saveBtn = ActionButton("Save settings");
            saveBtn.Click += async (s, e) => await SaveIbaSettingsAsync();
            ContentPanel.Children.Add(saveBtn);

            _ibaStatusText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 11,
                Foreground = LabelBrush,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            ContentPanel.Children.Add(_ibaStatusText);
        }

        private async Task LoadIbaSettingsAsync()
        {
            var url = _ibaUrlBox.Text?.Trim();
            if (string.IsNullOrEmpty(url)) return;
            _ibaStatusText.Text = "Loading...";
            try
            {
                using (var wc = new WebClient())
                {
                    var json = await Task.Run(() => wc.DownloadString(url.TrimEnd('/') + "/api/settings"));
                    var jo = JObject.Parse(json);
                    var s = jo["settings"] as JObject ?? jo;
                    var curiosity = s["curiosity_level"]?.ToString() ?? "Normal";
                    if (!_ibaCuriosityCombo.Items.Contains(curiosity))
                        _ibaCuriosityCombo.Items.Add(curiosity);
                    _ibaCuriosityCombo.SelectedItem = curiosity;
                    _ibaQuestionDelayBox.Text = s["question_delay"]?.ToString() ?? "10";
                    _ibaInitIntervalBox.Text = s["init_interval"]?.ToString() ?? "300";
                    _ibaLogLimitBox.Text = s["log_limit"]?.ToString() ?? "5000";
                    _ibaApiBaseUrlBox.Text = s["api_base_url"]?.ToString() ?? string.Empty;
                    _ibaModelBox.Text = s["model"]?.ToString() ?? string.Empty;
                    _ibaSystemPromptBox.Text = s["system_prompt"]?.ToString() ?? string.Empty;
                    _ibaStatusText.Text = "Settings loaded.";
                }
            }
            catch (Exception ex)
            {
                _ibaStatusText.Text = "Error loading settings: " + ex.Message;
            }
        }

        private async Task SaveIbaSettingsAsync()
        {
            var url = _ibaUrlBox.Text?.Trim();
            if (string.IsNullOrEmpty(url)) return;
            _ibaStatusText.Text = "Saving...";
            try
            {
                var settings = new JObject
                {
                    ["curiosity_level"] = _ibaCuriosityCombo.SelectedItem?.ToString() ?? "Normal",
                    ["question_delay"] = int.TryParse(_ibaQuestionDelayBox.Text?.Trim(), out var qd) ? qd : 10,
                    ["init_interval"] = int.TryParse(_ibaInitIntervalBox.Text?.Trim(), out var iv) ? iv : 300,
                    ["log_limit"] = int.TryParse(_ibaLogLimitBox.Text?.Trim(), out var ll) ? ll : 5000,
                    ["api_base_url"] = _ibaApiBaseUrlBox.Text?.Trim() ?? string.Empty,
                    ["model"] = _ibaModelBox.Text?.Trim() ?? string.Empty,
                    ["system_prompt"] = _ibaSystemPromptBox.Text ?? string.Empty
                };
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                    var body = settings.ToString(Newtonsoft.Json.Formatting.None);
                    await Task.Run(() => wc.UploadString(url.TrimEnd('/') + "/api/settings", "POST", body));
                    _ibaStatusText.Text = "Settings saved.";
                }
            }
            catch (Exception ex)
            {
                _ibaStatusText.Text = "Error saving settings: " + ex.Message;
            }
        }

        // ===== PIP (subagent picture-in-picture) =====
        private void AddPipContent()
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Choose how a subagent's live picture-in-picture (PiP) view appears inside the conversation when the assistant delegates work to a helper agent.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            ContentPanel.Children.Add(BoldLabel("PiP style"));

            bool advanced = string.Equals(AppSettings.PipStyle, "Advanced", StringComparison.OrdinalIgnoreCase);

            _pipBasicRadio = new RadioButton
            {
                Content = "Basic",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                GroupName = "PipStyle",
                IsChecked = !advanced,
                Margin = new Thickness(0, 6, 0, 0)
            };
            ContentPanel.Children.Add(_pipBasicRadio);
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "A clean, compact card anchored in the conversation. Shows the subagent's name, its task, and its live activity.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 1, 0, 8)
            });

            _pipAdvancedRadio = new RadioButton
            {
                Content = "Advanced",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                GroupName = "PipStyle",
                IsChecked = advanced,
                Margin = new Thickness(0, 2, 0, 0)
            };
            ContentPanel.Children.Add(_pipAdvancedRadio);
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "A mini Windows Live Messenger chat window, with the colour-scheme banner, the subagent's display picture and status, and the full conversation styling.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 1, 0, 8)
            });

            ContentPanel.Children.Add(new TextBlock
            {
                Text = "New subagent windows use this style. Applied when you click OK or Apply.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });
        }

        // ===== Debug =====
        private void AddDebugContent()
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Developer options and runtime diagnostics.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _debugModeCheck = Check("Enable debug mode (verbose logging and extra UI)", AppSettings.DebugMode);
            ContentPanel.Children.Add(_debugModeCheck);

            _softErrorHandlingCheck = Check("Soft error handling (suppress server error popups)", AppSettings.SoftErrorHandling);
            ContentPanel.Children.Add(_softErrorHandlingCheck);

            ContentPanel.Children.Add(Separator());

            var openCliBtn = ActionButton("Open Debug CLI");
            openCliBtn.Click += (s, e) =>
            {
                _debugStatusText.Text = "Debug CLI is not available in this build.";
            };
            ContentPanel.Children.Add(openCliBtn);

            ContentPanel.Children.Add(new TextBlock
            {
                Text = "The Debug CLI streams live ClientLog events with filtering, collapsible sections, and a simple command line.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 10)
            });

            ContentPanel.Children.Add(Separator());

            var clearLogBtn = ActionButton("Clear client log file");
            clearLogBtn.Click += (s, e) =>
            {
            try
            {
                if (!string.IsNullOrEmpty(ClientLog.LogPath) && System.IO.File.Exists(ClientLog.LogPath))
                    System.IO.File.WriteAllText(ClientLog.LogPath, string.Empty);
                ClientLog.Info("OptionsDialog", "Client log cleared.");
                _debugStatusText.Text = "Client log cleared.";
            }
                catch (Exception ex)
                {
                    _debugStatusText.Text = "Could not clear log: " + ex.Message;
                }
            };
            ContentPanel.Children.Add(clearLogBtn);

            _debugStatusText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 11,
                Foreground = LabelBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            ContentPanel.Children.Add(_debugStatusText);
        }

        // ===== Server (port 5000) =====
        private void AddServerContent()
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "MSN-ified view of the port 5000 web management GUI.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            ContentPanel.Children.Add(Label("Web GUI URL:"));
            _gui5000UrlBox = Field(AppSettings.Gui5000Url);
            ContentPanel.Children.Add(_gui5000UrlBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var refreshBtn = ActionButton("Refresh");
            refreshBtn.Click += async (s, e) => await RefreshServerGuiAsync();
            buttons.Children.Add(refreshBtn);

            var openBtn = ActionButton("Open Web GUI");
            openBtn.Margin = new Thickness(10, 4, 0, 10);
            openBtn.Click += (s, e) =>
            {
                var url = _gui5000UrlBox.Text?.Trim();
                if (!string.IsNullOrEmpty(url))
                    Process.Start(url);
            };
            buttons.Children.Add(openBtn);
            ContentPanel.Children.Add(buttons);

            _gui5000StatusText = new TextBlock
            {
                Text = "Click Refresh to load server status.",
                FontSize = 11,
                Foreground = LabelBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            ContentPanel.Children.Add(_gui5000StatusText);

            ContentPanel.Children.Add(Separator());
            ContentPanel.Children.Add(BoldLabel("Server Status"));
            _serverStatusPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
            ContentPanel.Children.Add(_serverStatusPanel);

            ContentPanel.Children.Add(BoldLabel("Profiles"));
            _profilesList = new ItemsControl();
            ContentPanel.Children.Add(_profilesList);

            ContentPanel.Children.Add(BoldLabel("Bots"));
            _botsList = new ItemsControl();
            ContentPanel.Children.Add(_botsList);
        }

        // ===== Updates =====
        private void AddUpdatesContent()
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "The server can announce when a new version is available. " +
                       "Automatic updates are turned off by default, and the server can never force an update.",
                FontSize = 11, Foreground = LabelBrush, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _updateCurrentVersionText = new TextBlock
            {
                Text = "Current version: " + UpdateService.CurrentVersion,
                FontSize = 12, Foreground = TextBrush, Margin = new Thickness(0, 0, 0, 10)
            };
            ContentPanel.Children.Add(_updateCurrentVersionText);

            _updateAutoCheckCheck = Check("Automatically check for updates", AppSettings.UpdateAutomaticCheck);
            ContentPanel.Children.Add(_updateAutoCheckCheck);

            _updateAutoDownloadCheck = Check("Automatically download updates", AppSettings.UpdateAutomaticDownload);
            ContentPanel.Children.Add(_updateAutoDownloadCheck);

            _updateAutoInstallCheck = Check("Automatically install updates", AppSettings.UpdateAutomaticInstall);
            ContentPanel.Children.Add(_updateAutoInstallCheck);

            ContentPanel.Children.Add(Label("Update channel:"));
            _updateChannelCombo = DropDown(new[] { "stable", "beta" }, AppSettings.UpdateChannel);
            ContentPanel.Children.Add(_updateChannelCombo);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
            _updateCheckNowButton = ActionButton("Check now");
            _updateCheckNowButton.Click += async (s, e) => await CheckForUpdateAsync();
            buttons.Children.Add(_updateCheckNowButton);
            ContentPanel.Children.Add(buttons);

            _updateStatusText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 11,
                Foreground = LabelBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            ContentPanel.Children.Add(_updateStatusText);

            ContentPanel.Children.Add(Separator());
            ContentPanel.Children.Add(BoldLabel("Rollback"));
            var rollbackBtn = ActionButton("Revert to previous version");
            rollbackBtn.Click += (s, e) =>
            {
                var svc = new UpdateService();
                if (svc.Rollback())
                    DoryNative.XpSim.SimMessageBox.Show(
                        "The previous version has been staged. Restart Windows Live Messenger to complete the rollback.",
                        "Windows Live Messenger");
                else
                    DoryNative.XpSim.SimMessageBox.Show(
                        "No rollback version is available.",
                        "Windows Live Messenger");
            };
            ContentPanel.Children.Add(rollbackBtn);
        }

        private async Task CheckForUpdateAsync()
        {
            _updateStatusText.Text = "Checking for updates...";
            _updateCheckNowButton.IsEnabled = false;
            try
            {
                var svc = new UpdateService();
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    var result = await svc.CheckAsync(cts.Token);
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        _updateStatusText.Text = "Could not check for updates: " + result.Error;
                        return;
                    }
                    if (!result.Available)
                    {
                        _updateStatusText.Text = "You are up to date.";
                        return;
                    }
                    if (string.Equals(result.NewVersion, AppSettings.UpdateSkippedVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        _updateStatusText.Text = $"Version {result.NewVersion} is available, but you chose to skip it.";
                        return;
                    }

                    _updateStatusText.Text = $"Version {result.NewVersion} is available.\n{result.ReleaseNotes}";
                }
            }
            catch (Exception ex)
            {
                _updateStatusText.Text = "Error: " + ex.Message;
            }
            finally
            {
                _updateCheckNowButton.IsEnabled = true;
            }
        }

        private async Task RefreshServerGuiAsync()
        {
            var baseUrl = _gui5000UrlBox.Text?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl)) return;
            _gui5000StatusText.Text = "Refreshing...";
            try
            {
                var statusTask = Task.Run(() =>
                {
                    using (var wc = new WebClient())
                        return wc.DownloadString(baseUrl + "/api/server/status");
                });
                var profilesTask = Task.Run(() =>
                {
                    using (var wc = new WebClient())
                        return wc.DownloadString(baseUrl + "/api/profiles");
                });
                var botsTask = Task.Run(() =>
                {
                    using (var wc = new WebClient())
                        return wc.DownloadString(baseUrl + "/api/bots");
                });
                await Task.WhenAll(statusTask, profilesTask, botsTask);

                var status = JObject.Parse(statusTask.Result);
                UpdateServerStatusPanel(status);

                var profiles = JObject.Parse(profilesTask.Result);
                UpdateProfilesList(profiles["profiles"] as JArray);

                var bots = JArray.Parse(botsTask.Result);
                UpdateBotsList(bots);

                _gui5000StatusText.Text = "Server status loaded.";
            }
            catch (Exception ex)
            {
                _gui5000StatusText.Text = "Error refreshing server status: " + ex.Message;
            }
        }

        private void UpdateServerStatusPanel(JObject status)
        {
            _serverStatusPanel.Children.Clear();
            AddStatusIndicator(_serverStatusPanel, "MSN NS (1863)", status["msn_ns"]?.Value<bool>() ?? false);
            AddStatusIndicator(_serverStatusPanel, "HTTP (80)", status["http"]?.Value<bool>() ?? false);
            AddStatusIndicator(_serverStatusPanel, "Admin (8082)", status["admin"]?.Value<bool>() ?? false);
            AddStatusIndicator(_serverStatusPanel, "GUI (5000)", status["gui"]?.Value<bool>() ?? false);
        }

        private void AddStatusIndicator(StackPanel panel, string label, bool online)
        {
            var container = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
            var dot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = online ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            container.Children.Add(dot);
            container.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(container);
        }

        private void UpdateProfilesList(JArray profiles)
        {
            _profilesList.Items.Clear();
            if (profiles == null || profiles.Count == 0)
            {
                _profilesList.Items.Add("No profiles found.");
                return;
            }
            foreach (var p in profiles)
            {
                var email = p["email"]?.ToString() ?? "?";
                var name = p["name"]?.ToString() ?? email;
                _profilesList.Items.Add($"{name}  <{email}>");
            }
        }

        private void UpdateBotsList(JArray bots)
        {
            _botsList.Items.Clear();
            if (bots == null || bots.Count == 0)
            {
                _botsList.Items.Add("No bots found.");
                return;
            }
            foreach (var b in bots)
            {
                var email = b["email"]?.ToString() ?? "?";
                var name = b["name"]?.ToString() ?? email;
                var mode = b["mode"]?.ToString() ?? "?";
                var active = b["active"]?.Value<int>() == 1;
                _botsList.Items.Add($"{name}  <{email}>  [{mode}] {(active ? "Active" : "Inactive")}");
            }
        }

        // ===== Persist current tab =====
        private void CommitCurrentTab()
        {
            // Personal
            if (_displayNameBox != null && _messenger != null)
            {
                var newName = _displayNameBox.Text?.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != _messenger.CurrentUser?.Name)
                    _messenger.SetDisplayName(newName);
            }
            if (_personalMessageBox != null && _messenger != null)
                _messenger.SetPersonalMessage(_personalMessageBox.Text ?? string.Empty);
            if (_nowPlayingCheck != null)
                AppSettings.NowPlayingEnabled = _nowPlayingCheck.IsChecked == true;
            if (_showDisplayPictureCheck != null)
                AppSettings.ShowDisplayPicture = _showDisplayPictureCheck.IsChecked == true;
            if (_awayWhenInactiveCheck != null)
                AppSettings.ShowAsAwayWhenInactive = _awayWhenInactiveCheck.IsChecked == true;
            if (_awayMinutesBox != null && int.TryParse(_awayMinutesBox.Text?.Trim(), out var mins))
                AppSettings.AwayAfterMinutes = mins;
            if (_busyWhenFullscreenCheck != null)
                AppSettings.ShowAsBusyWhenFullscreen = _busyWhenFullscreenCheck.IsChecked == true;
            if (_showWebcamCheck != null)
                AppSettings.ShowWebcamStatus = _showWebcamCheck.IsChecked == true;

            // Layout
            if (_showFavoritesCheck != null)
                AppSettings.ShowFavorites = _showFavoritesCheck.IsChecked == true;
            if (_showGroupsCheck != null)
                AppSettings.ShowGroups = _showGroupsCheck.IsChecked == true;
            if (_showTabBarCheck != null)
                AppSettings.ShowTabBar = _showTabBarCheck.IsChecked == true;
            if (_showWhatsNewListCheck != null)
                AppSettings.ShowWhatsNewList = _showWhatsNewListCheck.IsChecked == true;
            if (_labelContactsByCombo != null)
                AppSettings.LabelContactsBy = _labelContactsByCombo.SelectedItem?.ToString() ?? "Display name";
            if (_showStatusInLabelsCheck != null)
                AppSettings.ShowStatusInLabels = _showStatusInLabelsCheck.IsChecked == true;
            if (_organizeContactsByCombo != null)
                AppSettings.OrganizeContactsBy = _organizeContactsByCombo.SelectedItem?.ToString() ?? "Categories";
            if (_sortByStatusRadio != null && _sortAlphabeticallyRadio != null)
                AppSettings.SortContactsBy = _sortByStatusRadio.IsChecked == true ? "Status" : "Alphabetically";
            if (_showOfflineContactsCheck != null)
                AppSettings.ShowOfflineContacts = _showOfflineContactsCheck.IsChecked == true;
            if (_showOfflineSeparateCheck != null)
                AppSettings.ShowOfflineInSeparateCategory = _showOfflineSeparateCheck.IsChecked == true;
            if (_showOfflineSmsSeparateCheck != null)
                AppSettings.ShowOfflineSmsInSeparateCategory = _showOfflineSmsSeparateCheck.IsChecked == true;
            if (_styleCombo != null)
                AppSettings.UiStyle = _styleCombo.SelectedIndex == 1 ? "WLMLive" : "WLM2009";

            // Sign in
            if (_startWithWindowsCheck != null)
                AppSettings.StartWithWindows = _startWithWindowsCheck.IsChecked == true;
            if (_openMainWindowOnSignInCheck != null)
                AppSettings.OpenMainWindowOnSignIn = _openMainWindowOnSignInCheck.IsChecked == true;
            if (_showOtherSignInLocationsCheck != null)
                AppSettings.ShowOtherSignInLocations = _showOtherSignInLocationsCheck.IsChecked == true;
            if (_signInLocationNameBox != null)
                AppSettings.SignInLocationName = _signInLocationNameBox.Text ?? string.Empty;

            // Sounds
            if (_muteAllSoundsCheck != null)
                AppSettings.MuteAllSounds = _muteAllSoundsCheck.IsChecked == true;
            if (_soundContactSignInCheck != null)
                AppSettings.SoundContactSignIn = _soundContactSignInCheck.IsChecked == true;
            if (_soundNewInstantMessageCheck != null)
                AppSettings.SoundNewInstantMessage = _soundNewInstantMessageCheck.IsChecked == true;
            if (_soundNewEmailCheck != null)
                AppSettings.SoundNewEmail = _soundNewEmailCheck.IsChecked == true;
            if (_soundIncomingCallCheck != null)
                AppSettings.SoundIncomingCall = _soundIncomingCallCheck.IsChecked == true;
            if (_soundOutgoingCallCheck != null)
                AppSettings.SoundOutgoingCall = _soundOutgoingCallCheck.IsChecked == true;
            if (_soundNewAlertCheck != null)
                AppSettings.SoundNewAlert = _soundNewAlertCheck.IsChecked == true;
            if (_soundNudgeCheck != null)
                AppSettings.SoundNudge = _soundNudgeCheck.IsChecked == true;
            if (_soundMultipleChoiceQuestionCheck != null)
                AppSettings.SoundMultipleChoiceQuestion = _soundMultipleChoiceQuestionCheck.IsChecked == true;
            if (_soundSelectionCombos != null)
                foreach (var kv in _soundSelectionCombos)
                {
                    var sc = kv.Value.SelectedItem as SoundChoice;
                    AppSettings.SetSoundSelection(kv.Key, sc != null ? (sc.Id ?? string.Empty) : string.Empty);
                }

            // Alerts
            if (_showNotificationsCheck != null)
                AppSettings.ShowNotifications = _showNotificationsCheck.IsChecked == true;

            // Privacy
            if (_dontSaveContactListCheck != null)
                AppSettings.DontSaveContactListLocally = _dontSaveContactListCheck.IsChecked == true;
            if (_askPasswordForHotmailCheck != null)
                AppSettings.AskPasswordForHotmail = _askPasswordForHotmailCheck.IsChecked == true;
            if (_allowVoiceClipsCheck != null)
                AppSettings.AllowVoiceClips = _allowVoiceClipsCheck.IsChecked == true;
            if (_openMessengerForWebsiteLinksCheck != null)
                AppSettings.OpenMessengerForWebsiteLinks = _openMessengerForWebsiteLinksCheck.IsChecked == true;
            if (_allowMicrosoftDataCollectionCheck != null)
                AppSettings.AllowMicrosoftDataCollection = _allowMicrosoftDataCollectionCheck.IsChecked == true;

            // IBA / Server URLs
            if (_ibaUrlBox != null)
                AppSettings.IbaGuiUrl = _ibaUrlBox.Text?.Trim() ?? AppSettings.IbaGuiUrl;
            if (_ibaShowReasoningCheck != null)
                AppSettings.ShowReasoningSummaries = _ibaShowReasoningCheck.IsChecked == true;
            if (_ibaAutoUncollapseToolsCheck != null)
                AppSettings.AutoUncollapseToolUsage = _ibaAutoUncollapseToolsCheck.IsChecked == true;
            if (_gui5000UrlBox != null)
                AppSettings.Gui5000Url = _gui5000UrlBox.Text?.Trim() ?? AppSettings.Gui5000Url;

            // Updates
            if (_updateAutoCheckCheck != null)
                AppSettings.UpdateAutomaticCheck = _updateAutoCheckCheck.IsChecked == true;
            if (_updateAutoDownloadCheck != null)
                AppSettings.UpdateAutomaticDownload = _updateAutoDownloadCheck.IsChecked == true;
            if (_updateAutoInstallCheck != null)
                AppSettings.UpdateAutomaticInstall = _updateAutoInstallCheck.IsChecked == true;
            if (_updateChannelCombo != null)
                AppSettings.UpdateChannel = _updateChannelCombo.SelectedItem?.ToString() ?? "stable";

            // Debug
            if (_debugModeCheck != null)
                AppSettings.DebugMode = _debugModeCheck.IsChecked == true;
            if (_softErrorHandlingCheck != null)
                AppSettings.SoftErrorHandling = _softErrorHandlingCheck.IsChecked == true;

            // Tailscale
            if (_tailscaleEnabledCheck != null)
                AppSettings.TailscaleEnabled = _tailscaleEnabledCheck.IsChecked == true;
            if (_tailscaleServerHostnameBox != null)
                AppSettings.TailscaleServerHostname = _tailscaleServerHostnameBox.Text?.Trim() ?? AppSettings.TailscaleServerHostname;
            if (_tailscaleServerPortBox != null && int.TryParse(_tailscaleServerPortBox.Text?.Trim(), out var tsPort))
                AppSettings.TailscaleServerPort = tsPort;
            if (_tailscaleAuthKeyBox != null)
                AppSettings.TailscaleAuthKey = _tailscaleAuthKeyBox.Text?.Trim() ?? string.Empty;
            if (_tailscaleLoginServerBox != null)
                AppSettings.TailscaleLoginServer = _tailscaleLoginServerBox.Text?.Trim() ?? string.Empty;
            if (_tailscaleAcceptRoutesCheck != null)
                AppSettings.TailscaleAcceptRoutes = _tailscaleAcceptRoutesCheck.IsChecked == true;
            if (_tailscaleCliPathBox != null)
                AppSettings.TailscaleCliPath = _tailscaleCliPathBox.Text?.Trim() ?? string.Empty;

            // PIP
            if (_pipAdvancedRadio != null)
                AppSettings.PipStyle = _pipAdvancedRadio.IsChecked == true ? "Advanced" : "Basic";
        }

        private void ClearTabRefs()
        {
            _displayNameBox = null;
            _personalMessageBox = null;
            _nowPlayingCheck = null;
            _showDisplayPictureCheck = null;
            _awayWhenInactiveCheck = null;
            _awayMinutesBox = null;
            _busyWhenFullscreenCheck = null;
            _showWebcamCheck = null;

            _showFavoritesCheck = null;
            _showGroupsCheck = null;
            _showTabBarCheck = null;
            _showWhatsNewListCheck = null;
            _labelContactsByCombo = null;
            _showStatusInLabelsCheck = null;
            _organizeContactsByCombo = null;
            _sortByStatusRadio = null;
            _sortAlphabeticallyRadio = null;
            _showOfflineContactsCheck = null;
            _showOfflineSeparateCheck = null;
            _showOfflineSmsSeparateCheck = null;
            _styleCombo = null;

            _startWithWindowsCheck = null;
            _openMainWindowOnSignInCheck = null;
            _showOtherSignInLocationsCheck = null;
            _signInLocationNameBox = null;

            _muteAllSoundsCheck = null;
            _soundContactSignInCheck = null;
            _soundNewInstantMessageCheck = null;
            _soundNewEmailCheck = null;
            _soundIncomingCallCheck = null;
            _soundOutgoingCallCheck = null;
            _soundNewAlertCheck = null;
            _soundNudgeCheck = null;
            _soundMultipleChoiceQuestionCheck = null;
            _soundSelectionCombos = null;

            _showNotificationsCheck = null;

            _dontSaveContactListCheck = null;
            _askPasswordForHotmailCheck = null;
            _allowVoiceClipsCheck = null;
            _openMessengerForWebsiteLinksCheck = null;
            _allowMicrosoftDataCollectionCheck = null;

            _ibaUrlBox = null;
            _ibaCuriosityCombo = null;
            _ibaQuestionDelayBox = null;
            _ibaInitIntervalBox = null;
            _ibaLogLimitBox = null;
            _ibaApiBaseUrlBox = null;
            _ibaModelBox = null;
            _ibaSystemPromptBox = null;
            _ibaStatusText = null;
            _ibaShowReasoningCheck = null;
            _ibaAutoUncollapseToolsCheck = null;

            _gui5000UrlBox = null;
            _gui5000StatusText = null;
            _serverStatusPanel = null;
            _profilesList = null;
            _botsList = null;

            _updateAutoCheckCheck = null;
            _updateAutoDownloadCheck = null;
            _updateAutoInstallCheck = null;
            _updateChannelCombo = null;
            _updateStatusText = null;
            _updateCurrentVersionText = null;
            _updateCheckNowButton = null;

            _debugModeCheck = null;
            _softErrorHandlingCheck = null;
            _debugStatusText = null;

            _tailscaleEnabledCheck = null;
            _tailscaleAuthKeyBox = null;
            _tailscaleServerHostnameBox = null;
            _tailscaleServerPortBox = null;
            _tailscaleLoginServerBox = null;
            _tailscaleAcceptRoutesCheck = null;
            _tailscaleCliPathBox = null;
            _tailscaleStatusText = null;
            _tailscaleTestButton = null;

            _pipBasicRadio = null;
            _pipAdvancedRadio = null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CommitCurrentTab();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            CommitCurrentTab();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            DoryNative.XpSim.SimMessageBox.Show(
                "These options are stored locally and applied to your Messenger session.",
                "Windows Live Messenger");
        }
    }
}


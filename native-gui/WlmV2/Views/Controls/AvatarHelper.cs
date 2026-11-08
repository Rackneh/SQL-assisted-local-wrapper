using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MSNPSharp;
using WLMRevivalV2Desktop.Protocol;

namespace WLMRevivalV2Desktop.Views.Controls
{
    /// <summary>
    /// Shared helper for building contact avatar visuals (status frame + display picture)
    /// across all main window skins and chat windows. Eliminates copy-paste duplication.
    /// </summary>
    public static class AvatarHelper
    {
        public static BitmapImage WlmImage(string fileName)
        {
            return new BitmapImage(new Uri(
                "pack://application:,,,/Assets/wlm/" + fileName, UriKind.Absolute));
        }

        public static string StatusFrameSmall(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "status_frame_online_small.png";
                case PresenceStatus.Busy: return "status_frame_busy_small.png";
                case PresenceStatus.Away:
                case PresenceStatus.Idle: return "status_frame_away_small.png";
                default: return "status_frame_offline_small.png";
            }
        }

        public static string StatusFrameLarge(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "status_frame_online_large.png";
                case PresenceStatus.Busy: return "status_frame_busy_large.png";
                case PresenceStatus.Away:
                case PresenceStatus.Idle: return "status_frame_away_large.png";
                default: return "status_frame_offline_large.png";
            }
        }

        public static string StatusDot(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "status_online_dot.png";
                case PresenceStatus.Busy: return "status_busy_dot.png";
                case PresenceStatus.Away:
                case PresenceStatus.Idle: return "status_away_dot.png";
                default: return "status_offline_dot.png";
            }
        }

        /// <summary>
        /// Returns the application window icon that reflects the user's own status
        /// (green / yellow / red / grey dot in the top-right corner of the WLM icon).
        /// Hidden status is shown as offline, matching WLM 2009 behavior.
        /// </summary>
        public static BitmapImage StatusApplicationIcon(PresenceStatus status)
        {
            string iconName;
            switch (status)
            {
                case PresenceStatus.Online: iconName = "app_online.ico"; break;
                case PresenceStatus.Busy: iconName = "app_busy.ico"; break;
                case PresenceStatus.Away:
                case PresenceStatus.Idle: iconName = "app_away.ico"; break;
                case PresenceStatus.Hidden: iconName = "app_offline.ico"; break;
                default: iconName = "app_offline.ico"; break;
            }
            return new BitmapImage(new Uri(
                "pack://application:,,,/Assets/Icons/" + iconName, UriKind.Absolute));
        }

        public static string StatusLabel(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "Available";
                case PresenceStatus.Busy: return "Busy";
                case PresenceStatus.Away:
                case PresenceStatus.Idle: return "Away";
                case PresenceStatus.Hidden: return "Appear offline";
                default: return "Offline";
            }
        }

        public static string StatusText(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "(Available)";
                case PresenceStatus.Busy: return "(Busy)";
                case PresenceStatus.Away: return "(Away)";
                case PresenceStatus.Idle: return "(Idle)";
                default: return "(Offline)";
            }
        }

        public static bool IsOffline(PresenceStatus status)
        {
            return status == PresenceStatus.Offline
                || status == PresenceStatus.Hidden
                || status == PresenceStatus.Unknown;
        }

        /// <summary>
        /// Builds an avatar grid: status frame behind a clipped display picture tile.
        /// </summary>
        public static FrameworkElement BuildAvatar(Contact contact, double frameSize)
        {
            var grid = new Grid { Width = frameSize, Height = frameSize };

            var frame = new Image
            {
                Source = WlmImage(StatusFrameSmall(contact.Status)),
                Stretch = Stretch.Fill
            };
            Panel.SetZIndex(frame, 0);

            double tileSize = Math.Round(frameSize * 0.62);
            var tile = new Border
            {
                Width = tileSize,
                Height = tileSize,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Clip = new RectangleGeometry(new Rect(0, 0, tileSize, tileSize), 2, 2)
            };
            // Prefer the P2P picture; fall back to the HTTP display picture from
            // the style API (covers contacts/bots that don't serve P2P).
            var dp = MessengerService.TryGetDisplayImage(contact)
                     ?? ContactStyleManager.GetContactDisplayImage(contact.Mail);
            tile.Child = new Image
            {
                Source = dp ?? WlmImage("usertile_default.png"),
                Stretch = Stretch.UniformToFill
            };
            Panel.SetZIndex(tile, 1);

            grid.Children.Add(frame);
            grid.Children.Add(tile);
            return grid;
        }

        /// <summary>
        /// Builds a large avatar for chat windows.
        /// </summary>
        public static FrameworkElement BuildLargeAvatar(Contact contact, double frameSize)
        {
            var grid = new Grid { Width = frameSize, Height = frameSize };

            var frame = new Image
            {
                Source = WlmImage(StatusFrameLarge(contact.Status)),
                Stretch = Stretch.Fill
            };
            Panel.SetZIndex(frame, 0);

            double tileSize = Math.Round(frameSize * 0.75);
            var tile = new Border
            {
                Width = tileSize,
                Height = tileSize,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Clip = new RectangleGeometry(new Rect(0, 0, tileSize, tileSize), 2, 2)
            };
            // Prefer the P2P picture; fall back to the HTTP display picture from
            // the style API (covers contacts/bots that don't serve P2P).
            var dp = MessengerService.TryGetDisplayImage(contact)
                     ?? ContactStyleManager.GetContactDisplayImage(contact.Mail);
            tile.Child = new Image
            {
                Source = dp ?? WlmImage("usertile_default.png"),
                Stretch = Stretch.UniformToFill
            };
            Panel.SetZIndex(tile, 1);

            grid.Children.Add(frame);
            grid.Children.Add(tile);
            return grid;
        }

        /// <summary>
        /// Returns the display image for a contact, or the default tile if none.
        /// </summary>
        public static ImageSource GetDisplayImageOrDefault(Contact contact)
        {
            return MessengerService.TryGetDisplayImage(contact)
                   ?? ContactStyleManager.GetContactDisplayImage(contact?.Mail)
                   ?? WlmImage("usertile_default.png");
        }
    }
}

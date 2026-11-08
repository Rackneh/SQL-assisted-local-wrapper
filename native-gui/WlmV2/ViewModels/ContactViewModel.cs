using System;
using System.ComponentModel;
using System.Windows.Media;
using MSNPSharp;
using WLMRevivalV2Desktop.Protocol;

namespace WLMRevivalV2Desktop.ViewModels
{
    /// <summary>
    /// Lightweight viewmodel for a contact in the roster or chat window.
    /// Wraps the underlying MSNPSharp Contact and exposes UI-friendly
    /// properties including cached banner/color style info.
    /// </summary>
    public class ContactViewModel : INotifyPropertyChanged
    {
        private readonly Contact _contact;
        private ContactStyle _style;

        public ContactViewModel(Contact contact)
        {
            _contact = contact ?? throw new ArgumentNullException(nameof(contact));
            RefreshStyle();
        }

        public Contact UnderlyingContact => _contact;

        public string Mail => _contact.Mail;

        public string Name => _contact.Name;

        public string NickName => _contact.NickName;

        public string DisplayName => AppSettings.LabelContactsBy switch
        {
            "Nickname" => _contact.NickName ?? _contact.Name,
            "E-mail address" => _contact.Mail ?? _contact.Name,
            _ => _contact.Name
        };

        public PresenceStatus Status => _contact.Status;

        public bool IsOffline => Status == PresenceStatus.Offline
                              || Status == PresenceStatus.Hidden
                              || Status == PresenceStatus.Unknown;

        public ImageSource DisplayImage => MessengerService.TryGetDisplayImage(_contact);

        public string PersonalMessage => _contact.PersonalMessage?.Message ?? string.Empty;

        public bool HasBanner => _style?.HasBanner == true;

        public string BannerUrl => _style?.BannerUrl;

        public string ColorScheme => _style?.ColorScheme;

        public bool HasStyle => _style != null;

        public void RefreshStyle()
        {
            _style = ContactStyleManager.GetStyle(_contact.Mail);
            OnPropertyChanged(nameof(HasBanner));
            OnPropertyChanged(nameof(BannerUrl));
            OnPropertyChanged(nameof(ColorScheme));
            OnPropertyChanged(nameof(HasStyle));
        }

        public void OnDisplayImageChanged()
        {
            OnPropertyChanged(nameof(DisplayImage));
        }

        public void OnStatusChanged()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsOffline));
            OnPropertyChanged(nameof(DisplayName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

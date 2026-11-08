using System;
using System.ComponentModel;
using System.Windows.Media;
using MSNPSharp;
using WLMRevivalV2Desktop.Protocol;

namespace WLMRevivalV2Desktop.ViewModels
{
    /// <summary>
    /// Lightweight viewmodel for the currently logged-in user.
    /// Wraps the underlying MSNPSharp Owner contact and exposes
    /// UI-friendly properties for bindings or code-behind consumption.
    /// </summary>
    public class CurrentUserViewModel : INotifyPropertyChanged
    {
        private readonly MessengerService _messenger;
        private Contact _currentUser;

        public CurrentUserViewModel(MessengerService messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            Refresh();

            _messenger.ContactListSynchronized += (s, e) => Refresh();
            _messenger.ContactDisplayImageChanged += (s, e) =>
            {
                if (e.Contact != null && e.Contact.Mail == _currentUser?.Mail)
                    OnPropertyChanged(nameof(DisplayImage));
            };
        }

        public Contact UnderlyingContact => _currentUser;

        public string Name => _currentUser?.Name ?? "Not Logged In";

        public PresenceStatus Status => _currentUser?.Status ?? PresenceStatus.Offline;

        public ImageSource DisplayImage => _currentUser != null
            ? MessengerService.TryGetDisplayImage(_currentUser)
            : null;

        public string PersonalMessage => _messenger?.GetPersonalMessage() ?? string.Empty;

        public void Refresh()
        {
            _currentUser = _messenger.CurrentUser;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(DisplayImage));
            OnPropertyChanged(nameof(PersonalMessage));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

// Minimal MSNPSharp compatibility shim.
// The V2 views were written against MSNPSharp's object model; this file provides
// just the types/members they touch so the UI layer compiles unchanged. The real
// transport is DoryNative's BackendApi (HTTP to the Python IBA service).
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MSNPSharp
{
    public enum PresenceStatus
    {
        Unknown = 0,
        Offline = 1,
        Hidden = 2,
        AppearOffline = 3,
        Online = 4,
        Busy = 5,
        Away = 6,
        Idle = 7,
        BeRightBack = 8,
        OnThePhone = 9,
        OutToLunch = 10
    }

    public class PersonalMessage
    {
        public string Message { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class ContactGroup
    {
        public string Name { get; set; }
        public Guid Guid { get; set; } = Guid.NewGuid();
        public List<Contact> Contacts { get; set; } = new List<Contact>();
    }

    public class DisplayImage
    {
        public byte[] ImageData { get; set; }
        public string ContentType { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class Wink
    {
        public string Location { get; set; }
        public string Name { get; set; }
    }

    public class Contact
    {
        public string Mail { get; set; }
        public string Name { get; set; }
        public string NickName { get; set; }
        public PresenceStatus Status { get; set; } = PresenceStatus.Online;
        public PersonalMessage PersonalMessage { get; set; }
        public DisplayImage DisplayImage { get; set; }
        public List<ContactGroup> ContactGroups { get; set; } = new List<ContactGroup>();
        public bool Blocked { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
        public object Tag { get; set; }

        public override string ToString() => Name ?? Mail ?? "Contact";
    }

    public class SignedOffEventArgs : EventArgs
    {
        public string Reason { get; set; }
        public Exception Exception { get; set; }
    }

    public static class Settings
    {
        private static readonly TraceSwitch _traceSwitch = new TraceSwitch("MSNPSharp", "MSNPSharp trace");
        public static TraceSwitch TraceSwitch => _traceSwitch;
    }
}

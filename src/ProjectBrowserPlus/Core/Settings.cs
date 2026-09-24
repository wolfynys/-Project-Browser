using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace ProjectBrowserPlus.Core
{
    [DataContract]
    public class Settings
    {
        [DataMember] public string Language { get; set; } = "ru";
        [DataMember] public bool FollowActiveView { get; set; } = true;
        [DataMember] public bool ShowBadges { get; set; } = true;
        [DataMember] public bool NaturalSort { get; set; } = true;
        [DataMember] public bool AutoFamilyPreviews { get; set; } = true;
        [DataMember] public bool CountInstances { get; set; } = true;
        [DataMember] public bool ConfirmDelete { get; set; } = true;
        [DataMember] public bool OpenOnSingleClick { get; set; } = false;
        [DataMember] public bool CloseOthersOnOpen { get; set; } = false;
        [DataMember] public int PreviewSize { get; set; } = 96;
        [DataMember] public string LastTab { get; set; } = "all";
        [DataMember] public bool RememberLastTab { get; set; } = true;
        [DataMember] public List<string> HiddenTabs { get; set; } = new List<string>();
        [DataMember] public Dictionary<string, string> GroupByPerTab { get; set; } = new Dictionary<string, string>();
        [DataMember] public bool ShowSheetViewsAsChildren { get; set; } = true;
        [DataMember] public bool CompactRows { get; set; } = false;
        [DataMember] public int MaxRecent { get; set; } = 15;

        public static string FilePath => Path.Combine(Log.Dir, "settings.json");

        private static Settings _current;
        public static Settings Current => _current ?? (_current = Json.Load<Settings>(FilePath));

        public void Save() => Json.Save(FilePath, this);

        public static void Reload() { _current = null; }
    }
}

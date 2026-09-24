using System;
using Autodesk.Revit.DB;

namespace ProjectBrowserPlus.Model
{
    public enum ChangeKind { Rename, Renumber }

    /// <summary>A staged edit shown in the "draft" bar and applied later in one transaction.</summary>
    public class PendingChange
    {
        public ChangeKind Kind { get; set; }
        public ElementId Id { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public BrowserItem Item { get; set; }
        public string Description => $"{OldValue} → {NewValue}";
    }
}

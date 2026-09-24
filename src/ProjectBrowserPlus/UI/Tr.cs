using System;
using System.Windows.Markup;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.UI
{
    /// <summary>{ui:Tr key} – localized string in XAML.</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class Tr : MarkupExtension
    {
        public string Key { get; set; }
        public Tr() { }
        public Tr(string key) { Key = key; }
        public override object ProvideValue(IServiceProvider sp) => L.T(Key);
    }
}

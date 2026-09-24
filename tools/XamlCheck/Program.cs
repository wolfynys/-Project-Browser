// Static check for WPF XAML that compiles but fails at runtime:
// every <Setter Property="X"> / <Trigger Property="X"> must name a DependencyProperty
// (a public static field "XProperty") on the Style/ControlTemplate TargetType or one of its bases.
// Usage: XamlCheck <net48 reference assemblies dir> <built add-in dll> <xaml files...>
using System.Reflection;
using System.Xml.Linq;

var refDir = args[0];
var addin = args[1];
var files = args.Skip(2).ToArray();
var paths = Directory.GetFiles(refDir, "*.dll").ToList();
paths.Add(addin);
foreach (var extra in Directory.GetFiles(Path.GetDirectoryName(addin)!, "*.dll")) if (!paths.Contains(extra)) paths.Add(extra);
foreach (var nuget in new[] { "nice3point.revit.api.revitapi", "nice3point.revit.api.revitapiui" })
{
    var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", nuget);
    if (Directory.Exists(d)) paths.AddRange(Directory.GetFiles(d, "*.dll", SearchOption.AllDirectories));
}
using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths.Distinct()), "mscorlib");
var asms = new[] { "PresentationFramework", "PresentationCore", "WindowsBase" }.Select(n => mlc.LoadFromAssemblyPath(Path.Combine(refDir, n + ".dll"))).ToList();
var own = mlc.LoadFromAssemblyPath(addin);
XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

Type Resolve(string name, XElement ctx)
{
    if (name.StartsWith("{x:Type ")) name = name.Substring(8).TrimEnd('}').Trim();
    var colon = name.IndexOf(':');
    if (colon > 0)
    {
        var prefix = name[..colon]; var local = name[(colon + 1)..];
        var ns = ctx.GetNamespaceOfPrefix(prefix)?.NamespaceName ?? "";
        var clr = ns.StartsWith("clr-namespace:") ? ns.Substring(14).Split(';')[0] : "";
        return own.GetType(clr + "." + local);
    }
    foreach (var a in asms)
        foreach (var t in a.GetExportedTypes())
            if (t.Name == name && t.Namespace!.StartsWith("System.Windows")) return t;
    return null;
}

bool HasDp(Type t, string prop, XElement ctx)
{
    if (prop.Contains('.'))
    {
        var owner = Resolve(prop[..prop.LastIndexOf('.')], ctx);
        prop = prop[(prop.LastIndexOf('.') + 1)..];
        t = owner;
    }
    for (var c = t; c != null; c = c.BaseType)
        if (c.GetField(prop + "Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly) != null) return true;
    return false;
}

int errors = 0, checkedCount = 0;
foreach (var f in files)
{
    var doc = XDocument.Load(f, LoadOptions.SetLineInfo);
    foreach (var el in doc.Descendants().Where(e => (e.Name == wpf + "Setter" || e.Name == wpf + "Trigger") && e.Attribute("Property") != null))
    {
        if (el.Attribute("TargetName") != null) continue;
        var owner = el.Ancestors().FirstOrDefault(a => (a.Name == wpf + "Style" || a.Name == wpf + "ControlTemplate") && a.Attribute("TargetType") != null);
        if (owner == null) continue;
        var type = Resolve(owner.Attribute("TargetType")!.Value, owner);
        var prop = el.Attribute("Property")!.Value;
        checkedCount++;
        if (type == null) { Console.WriteLine($"{f}({((System.Xml.IXmlLineInfo)el).LineNumber}): cannot resolve type {owner.Attribute("TargetType")!.Value}"); errors++; continue; }
        if (!HasDp(type, prop, el))
        {
            Console.WriteLine($"{f}({((System.Xml.IXmlLineInfo)el).LineNumber}): '{prop}' is not a DependencyProperty of {type.Name} -> XamlParseException at runtime");
            errors++;
        }
    }
}
Console.WriteLine($"checked {checkedCount} setters/triggers, {errors} problem(s)");
return errors == 0 ? 0 : 1;

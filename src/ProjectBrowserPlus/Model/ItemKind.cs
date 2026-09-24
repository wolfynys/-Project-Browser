namespace ProjectBrowserPlus.Model
{
    public enum ItemKind
    {
        Folder,        // virtual grouping node
        View,          // any graphical view (plan, section, 3D, drafting ...)
        Sheet,
        Schedule,
        Legend,
        ViewTemplate,
        Category,      // family category folder
        Family,        // loadable family / system family
        FamilyType,    // family symbol or element type
        Group,         // model / detail group type
        Link,          // Revit link / CAD link
        Level,
        Workset,
        Info           // non-clickable informational row
    }

    public enum SortMode { NameAsc, NameDesc, NumberAsc, NumberDesc, IdAsc }
}

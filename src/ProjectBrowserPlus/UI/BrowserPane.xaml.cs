using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Settings = ProjectBrowserPlus.Core.Settings;
using Autodesk.Revit.UI;
using TextBox = System.Windows.Controls.TextBox;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;
using ProjectBrowserPlus.Services;
using ProjectBrowserPlus.UI.Dialogs;

namespace ProjectBrowserPlus.UI
{
    public partial class BrowserPane : UserControl
    {
        public BrowserViewModel ViewModel { get; }
        private bool _firstLoad = true;
        private BrowserItem _editing;

        public BrowserPane()
        {
            InitializeComponent();
            ViewModel = new BrowserViewModel();
            DataContext = ViewModel;
            ViewModel.ScrollIntoViewRequested += it => Dispatcher.BeginInvoke(new Action(() => { try { List.ScrollIntoView(it); } catch { } }), System.Windows.Threading.DispatcherPriority.Background);
            Loaded += (s, e) => { if (_firstLoad) { _firstLoad = false; ViewModel.RequestRefresh(true); } };
            IsVisibleChanged += (s, e) => { if ((bool)e.NewValue && !_firstLoad) ViewModel.RequestRefresh(false); };
        }

        // ------------------------------------------------------------------ tabs / toolbar
        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TabVm t) ViewModel.SelectedTab = t;
        }

        private void Sort_Click(object sender, RoutedEventArgs e)
        {
            var cm = new ContextMenu();
            foreach (var s in ViewModel.SortModes)
            {
                var m = new MenuItem { Header = s.Title, Icon = new Icon { Kind = s.Icon, Size = 14 }, IsChecked = ViewModel.SelectedSort == s };
                var sm = s;
                m.Click += (o, a) => ViewModel.SelectedSort = sm;
                cm.Items.Add(m);
            }
            OpenMenu(cm, SortButton);
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var cm = new ContextMenu();
            Add(cm, L.T("menu.createSheets"), "file-plus", CreateSheets);
            Add(cm, L.T("menu.createPlans"), "layers", CreatePlans);
            var views = ViewModel.TargetItems(i => i.IsViewLike && i.Kind != ItemKind.Sheet);
            Add(cm, L.T("menu.sheetForViews"), "sheet", () => CreateSheetForViews(views), views.Count > 0);
            Add(cm, L.T("menu.duplicate"), "files", () => Duplicate(ViewModel.TargetItems(i => i.Kind == ItemKind.View || i.Kind == ItemKind.Legend || i.Kind == ItemKind.Schedule)), views.Count > 0);
            OpenMenu(cm, CreateButton);
        }

        private void Tools_Click(object sender, RoutedEventArgs e)
        {
            var cm = new ContextMenu();
            var targets = ViewModel.TargetItems();
            var any = targets.Count > 0;
            Add(cm, L.T("menu.batchRename"), "replace", () => BatchRename(targets.Count > 0 ? targets : AllLeaves()), true);
            Add(cm, L.T("menu.renumber"), "hash", () => Renumber(SheetsFor(targets)), true);
            Add(cm, L.T("menu.applyTemplate"), "paintbrush", () => ApplyTemplate(targets.Where(i => i.Kind == ItemKind.View).ToList()), targets.Any(i => i.Kind == ItemKind.View));
            Add(cm, L.T("menu.placeOnSheets"), "file-check", () => PlaceOnSheets(targets.Where(i => i.Kind == ItemKind.Legend || i.Kind == ItemKind.Schedule || i.Kind == ItemKind.View).ToList()), targets.Any(i => i.Kind == ItemKind.Legend || i.Kind == ItemKind.Schedule || i.Kind == ItemKind.View));
            Add(cm, L.T("menu.setParameter"), "sliders-horizontal", () => SetParameter(targets), any);
            cm.Items.Add(new Separator());
            Add(cm, L.T("menu.selectUnused"), "circle-x", SelectUnused, ViewModel.IsFamiliesTab);
            Add(cm, L.T("menu.selectNotOnSheet"), "file-x", SelectNotOnSheet, ViewModel.IsViewsTab || ViewModel.SelectedTab?.Key == "legends" || ViewModel.SelectedTab?.Key == "schedules");
            Add(cm, L.T("menu.closeInactive"), "panels-top-left", () => ViewModel.Run(app => RevitActions.CloseOtherViews(app, app.ActiveUIDocument.ActiveView.Id)));
            cm.Items.Add(new Separator());
            Add(cm, L.T("menu.copyList"), "clipboard-copy", () => SafeClipboard(ViewModel.ExportText(false)));
            Add(cm, L.T("menu.exportCsv"), "download", ExportCsv);
            Add(cm, L.T("menu.clearPreviewCache"), "image-off", () => { try { System.IO.Directory.Delete(PreviewService.CacheDir, true); } catch { } PreviewService.ClearMemory(); });
            OpenMenu(cm, ToolsButton);
        }

        private static void OpenMenu(ContextMenu cm, UIElement target)
        {
            cm.PlacementTarget = target; cm.Placement = PlacementMode.Bottom; cm.IsOpen = true;
        }

        private static MenuItem Add(ItemsControl cm, string header, string icon, Action action, bool enabled = true, string gesture = null)
        {
            var m = new MenuItem { Header = header, IsEnabled = enabled, InputGestureText = gesture };
            if (icon != null) m.Icon = new Icon { Kind = icon, Size = 14, Foreground = (Brush)new BrushConverter().ConvertFromString("#6B7280") };
            m.Click += (s, e) => { try { action(); } catch (Exception ex) { Log.Error("menu " + header, ex); } };
            cm.Items.Add(m);
            return m;
        }

        private List<BrowserItem> AllLeaves() => ViewModel.Rows.Where(r => !r.IsFolder && r.IsElement).ToList();

        private List<BrowserItem> SheetsFor(List<BrowserItem> targets)
        {
            var sheets = targets.Where(i => i.Kind == ItemKind.Sheet).ToList();
            if (sheets.Count == 0) sheets = ViewModel.Rows.Where(r => r.Kind == ItemKind.Sheet).ToList();
            return sheets;
        }

        // ------------------------------------------------------------------ list events
        private void Row_Loaded(object sender, RoutedEventArgs e)
        {
            var it = (sender as FrameworkElement)?.DataContext as BrowserItem;
            if (it != null && it.NeedsPreview && it.Preview == null && Settings.Current.AutoFamilyPreviews)
                PreviewService.Request(it);
        }

        private void Row_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => Row_Loaded(sender, null);

        private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.OriginalSource is DependencyObject src && FindAncestor<ButtonBase>(src) != null) return;
            var it = ItemAt(e.OriginalSource as DependencyObject);
            if (it == null) return;
            if (_editing != null) return;
            ViewModel.Activate(it);
            e.Handled = true;
        }

        private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!Settings.Current.OpenOnSingleClick) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            var it = ItemAt(e.OriginalSource as DependencyObject);
            if (it == null || it.IsFolder || !it.IsViewLike) return;
            if (e.OriginalSource is DependencyObject d && FindAncestor<ButtonBase>(d) != null) return;
            ViewModel.Activate(it);
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) => ViewModel.UpdateSelectionStatus();

        private void List_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_editing != null) return;
            var sel = ViewModel.SelectedItems;
            var focus = List.SelectedItem as BrowserItem ?? sel.LastOrDefault();
            switch (e.Key)
            {
                case Key.Enter:
                    if (focus != null) { ViewModel.Activate(focus); e.Handled = true; }
                    break;
                case Key.F2:
                    if (focus != null && focus.IsElement) { BeginEdit(focus); e.Handled = true; }
                    break;
                case Key.Delete:
                    DeleteItems(ViewModel.TargetItems()); e.Handled = true;
                    break;
                case Key.Right:
                    if (focus != null && focus.HasChildren && !focus.IsExpanded) { focus.IsExpanded = true; ViewModel.RebuildRows(); e.Handled = true; }
                    break;
                case Key.Left:
                    if (focus != null)
                    {
                        if (focus.HasChildren && focus.IsExpanded) { focus.IsExpanded = false; ViewModel.RebuildRows(); }
                        else if (focus.Parent != null && focus.Parent.Level >= 0) { ViewModel.Reveal(focus.Parent, true); }
                        e.Handled = true;
                    }
                    break;
                case Key.Space:
                    if (focus != null && !focus.IsFolder) { ViewModel.ToggleFavorite(focus); e.Handled = true; }
                    break;
                case Key.F:
                    if (Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
                    break;
                case Key.D:
                    if (Keyboard.Modifiers == ModifierKeys.Control) { Duplicate(ViewModel.TargetItems(i => i.Kind == ItemKind.View || i.Kind == ItemKind.Legend || i.Kind == ItemKind.Schedule)); e.Handled = true; }
                    break;
                case Key.C:
                    if (Keyboard.Modifiers == ModifierKeys.Control) { SafeClipboard(string.Join("\n", sel.Select(i => i.DisplayText))); e.Handled = true; }
                    break;
                case Key.A:
                    if (Keyboard.Modifiers == ModifierKeys.Control) { foreach (var r in ViewModel.Rows) r.IsSelected = !r.IsFolder; e.Handled = true; }
                    break;
                case Key.Escape:
                    if (ViewModel.HasSearch) { ViewModel.Search = ""; e.Handled = true; }
                    break;
            }
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { ViewModel.Search = ""; e.Handled = true; }
            else if (e.Key == Key.Down || e.Key == Key.Enter)
            {
                List.Focus();
                var first = ViewModel.Rows.FirstOrDefault(r => !r.IsFolder) ?? ViewModel.Rows.FirstOrDefault();
                if (first != null)
                {
                    List.SelectedItem = first;
                    if (e.Key == Key.Enter) ViewModel.Activate(first);
                    else { var c = List.ItemContainerGenerator.ContainerFromItem(first) as ListBoxItem; c?.Focus(); }
                }
                e.Handled = true;
            }
        }

        private static BrowserItem ItemAt(DependencyObject d)
        {
            var c = FindAncestor<ListBoxItem>(d);
            return c?.DataContext as BrowserItem;
        }

        private static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return null;
        }

        // ------------------------------------------------------------------ inline editing
        public void BeginEdit(BrowserItem it)
        {
            if (it == null || !it.IsElement) return;
            if (it.Kind == ItemKind.Link && it.Number != "revit") return;
            _editing = it;
            it.IsEditing = true;
        }

        private void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            tb.IsVisibleChanged -= Editor_VisibleChanged;
            tb.IsVisibleChanged += Editor_VisibleChanged;
            if (!tb.IsVisible) return;
            if (tb.DataContext is BrowserItem it) tb.Text = it.Kind == ItemKind.Sheet ? it.EffectiveNumber + " - " + it.EffectiveName : it.EffectiveName;
            tb.Dispatcher.BeginInvoke(new Action(() => { tb.Focus(); Keyboard.Focus(tb); tb.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void Editor_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var tb = (TextBox)sender;
            if ((bool)e.NewValue)
            {
                if (tb.DataContext is BrowserItem it) tb.Text = it.Kind == ItemKind.Sheet ? it.EffectiveNumber + " - " + it.EffectiveName : it.EffectiveName;
                tb.Dispatcher.BeginInvoke(new Action(() => { tb.Focus(); Keyboard.Focus(tb); tb.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            var tb = (TextBox)sender;
            if (e.Key == Key.Enter) { CommitEdit(tb, true); e.Handled = true; }
            else if (e.Key == Key.Escape) { CommitEdit(tb, false); e.Handled = true; }
        }

        private void Editor_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitEdit((TextBox)sender, true);

        private void CommitEdit(TextBox tb, bool commit)
        {
            var it = tb.DataContext as BrowserItem;
            if (it == null || !it.IsEditing) return;
            it.IsEditing = false;
            _editing = null;
            if (commit) ViewModel.StageRename(it, tb.Text);
            List.Focus();
            var c = List.ItemContainerGenerator.ContainerFromItem(it) as ListBoxItem; c?.Focus();
        }

        // ------------------------------------------------------------------ context menu
        private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var clicked = ItemAt(e.OriginalSource as DependencyObject);
            if (clicked != null && !clicked.IsSelected) { foreach (var r in ViewModel.Rows) r.IsSelected = r == clicked; }
            e.Handled = true;
            var cm = new ContextMenu();
            BuildContextMenu(cm, clicked);
            if (cm.Items.Count == 0) return;
            cm.PlacementTarget = List;
            cm.Placement = PlacementMode.MousePoint;
            cm.IsOpen = true;
        }

        private void BuildContextMenu(ContextMenu cm, BrowserItem clicked)
        {
            var sel = ViewModel.SelectedItems;
            var targets = ViewModel.TargetItems();
            var focus = clicked ?? sel.LastOrDefault();
            if (focus == null) { Add(cm, L.T("menu.refresh"), "refresh-cw", () => ViewModel.RequestRefresh(true)); return; }
            var kinds = new HashSet<ItemKind>(targets.Select(t => t.Kind));
            var single = sel.Count == 1;
            var viewLike = targets.Where(t => t.IsViewLike).ToList();
            var graphViews = targets.Where(t => t.Kind == ItemKind.View).ToList();
            var sheets = targets.Where(t => t.Kind == ItemKind.Sheet).ToList();
            var placeable = targets.Where(t => t.Kind == ItemKind.Legend || t.Kind == ItemKind.Schedule || (t.Kind == ItemKind.View && !t.IsOnSheet)).ToList();
            var types = targets.Where(t => t.Kind == ItemKind.FamilyType).ToList();
            var fams = targets.Where(t => t.Kind == ItemKind.Family).ToList();

            if (focus.IsFolder)
            {
                Add(cm, focus.IsExpanded ? L.T("menu.collapse") : L.T("menu.expand"), focus.IsExpanded ? "chevrons-down-up" : "chevrons-up-down", () => { focus.IsExpanded = !focus.IsExpanded; ViewModel.RebuildRows(); });
                Add(cm, L.T("menu.expandBranch"), "folder-open", () => { foreach (var d in focus.Descendants()) if (d.HasChildren) d.IsExpanded = true; focus.IsExpanded = true; ViewModel.RebuildRows(); });
                Add(cm, L.T("menu.selectChildren"), "list-checks", () => { focus.IsExpanded = true; ViewModel.RebuildRows(); foreach (var r in ViewModel.Rows) r.IsSelected = r != focus && IsUnder(r, focus) && !r.IsFolder; });
                cm.Items.Add(new Separator());
            }

            if (focus.IsViewLike && single)
            {
                Add(cm, L.T("menu.open"), "external-link", () => ViewModel.Activate(focus), true, "Enter");
                if (focus.IsOnSheet && focus.Kind != ItemKind.Sheet) Add(cm, L.T("menu.openSheet"), "sheet", () => ViewModel.Run(app => RevitActions.OpenSheetOf(app, focus.Id)));
                if (focus.IsOpen) Add(cm, L.T("menu.closeTab"), "x", () => ViewModel.Run(app => RevitActions.CloseViews(app, new[] { focus.Id })));
            }
            else if (viewLike.Count > 1)
            {
                var openIds = viewLike.Where(v => v.IsOpen).Select(v => v.Id).ToList();
                if (openIds.Count > 0) Add(cm, L.T("menu.closeTabs"), "x", () => ViewModel.Run(app => RevitActions.CloseViews(app, openIds)));
            }
            if (focus.Kind == ItemKind.FamilyType && single && focus.IsPlaceable)
                Add(cm, L.T("menu.place"), "move", () => ViewModel.Activate(focus), true, "Enter");
            if (types.Count > 0 || fams.Count > 0 || kinds.Contains(ItemKind.Group) || kinds.Contains(ItemKind.Link))
            {
                var inst = targets.Where(t => t.Kind == ItemKind.FamilyType || t.Kind == ItemKind.Family || t.Kind == ItemKind.Group || t.Kind == ItemKind.Link).ToList();
                Add(cm, L.T("menu.selectInView"), "locate", () => SelectInstances(inst, true, false));
                Add(cm, L.T("menu.selectInProject"), "crosshair", () => SelectInstances(inst, false, false));
                Add(cm, L.T("menu.showInstances"), "eye", () => SelectInstances(inst, false, true));
            }
            if (fams.Count == 1 && single && !focus.IsSystemFamily && focus.IsElement)
            {
                Add(cm, L.T("menu.editFamily"), "square-pen", () => ViewModel.Run(app => ViewModel.Report(RevitActions.EditFamily(app, focus.Id))));
                Add(cm, L.T("menu.saveFamily"), "save", () => SaveFamily(focus));
            }
            if (kinds.Contains(ItemKind.Link))
            {
                var links = targets.Where(t => t.Kind == ItemKind.Link).ToList();
                Add(cm, L.T("menu.reload"), "refresh-cw", () => ViewModel.Run(app => { foreach (var l in links) ViewModel.Report(RevitActions.ReloadLink(app, l.Id)); }));
                if (links.Any(l => l.Number == "revit")) Add(cm, L.T("menu.unload"), "eye-off", () => ViewModel.Run(app => { foreach (var l in links.Where(x => x.Number == "revit")) ViewModel.Report(RevitActions.UnloadLink(app, l.Id)); }));
                if (single) Add(cm, L.T("menu.openFolder"), "folder-open", () => ViewModel.Run(app => ViewModel.Report(RevitActions.OpenLinkFolder(app, focus.Id))));
            }
            if (kinds.Contains(ItemKind.Level))
                Add(cm, L.T("menu.createPlans"), "layers", CreatePlans);

            cm.Items.Add(new Separator());
            if (focus.IsElement && single) Add(cm, L.T("menu.rename"), "text-cursor-input", () => BeginEdit(focus), true, "F2");
            if (targets.Count > 0) Add(cm, L.T("menu.batchRename"), "replace", () => BatchRename(targets));
            if (sheets.Count > 0) Add(cm, L.T("menu.renumber"), "hash", () => Renumber(SheetsFor(targets)));
            if (graphViews.Count > 0 || kinds.Contains(ItemKind.Legend) || kinds.Contains(ItemKind.Schedule))
                Add(cm, L.T("menu.duplicate"), "files", () => Duplicate(targets.Where(i => i.Kind == ItemKind.View || i.Kind == ItemKind.Legend || i.Kind == ItemKind.Schedule).ToList()), true, "Ctrl+D");
            if (graphViews.Count > 0)
            {
                Add(cm, L.T("menu.applyTemplate"), "paintbrush", () => ApplyTemplate(graphViews));
                if (graphViews.Any(v => v.IsTemplate)) Add(cm, L.T("menu.removeTemplate"), "eye-off", () => ViewModel.Run(app => ViewModel.Report(RevitActions.ApplyTemplate(app, graphViews.Select(v => v.Id).ToList(), ElementId.InvalidElementId))));
            }
            if (placeable.Count > 0) Add(cm, L.T("menu.placeOnSheets"), "file-check", () => PlaceOnSheets(placeable));
            if (viewLike.Any(v => v.Kind != ItemKind.Sheet)) Add(cm, L.T("menu.sheetForViews"), "sheet", () => CreateSheetForViews(viewLike.Where(v => v.Kind != ItemKind.Sheet).ToList()));
            if (targets.Count > 0) Add(cm, L.T("menu.setParameter"), "sliders-horizontal", () => SetParameter(targets));

            cm.Items.Add(new Separator());
            if (!focus.IsFolder)
            {
                Add(cm, focus.IsFavorite ? L.T("menu.unfavorite") : L.T("menu.favorite"), "star", () => ViewModel.ToggleFavorite(focus), focus.UniqueId != null, "Space");
                if (single) Add(cm, L.T("menu.note"), "text", () => AddNote(focus), focus.UniqueId != null);
                var colors = new MenuItem { Header = L.T("menu.color"), Icon = new Icon { Kind = "tag", Size = 14 } };
                foreach (var c in new[] { "#EF4444", "#F59E0B", "#22C55E", "#3B82F6", "#8B5CF6", "#EC4899", "#64748B" })
                {
                    var cc = c;
                    var mi = new MenuItem { Header = new Border { Width = 60, Height = 12, Background = (Brush)new BrushConverter().ConvertFromString(cc), CornerRadius = new CornerRadius(3) } };
                    mi.Click += (s, e) => ViewModel.SetColor(focus, cc);
                    colors.Items.Add(mi);
                }
                var none = new MenuItem { Header = L.T("menu.colorNone") }; none.Click += (s, e) => ViewModel.SetColor(focus, null); colors.Items.Add(none);
                cm.Items.Add(colors);
                if (focus.IsViewLike && single) Add(cm, L.T("menu.preview"), "image", () => { ViewModel.ShowPreview = true; ViewModel.UpdatePreview(); ViewModel.GenerateViewPreview(true); });
            }
            Add(cm, L.T("menu.copyName"), "clipboard-copy", () => SafeClipboard(string.Join("\n", sel.Select(i => i.DisplayText))), true, "Ctrl+C");
            if (focus.IsElement && single) Add(cm, L.T("menu.copyId"), "hash", () => SafeClipboard(focus.IdValue.ToString()));
            cm.Items.Add(new Separator());
            var deletable = targets.Where(t => t.IsElement && t.Kind != ItemKind.Link || t.Kind == ItemKind.Link).ToList();
            if (deletable.Count > 0) Add(cm, L.T("menu.delete"), "trash-2", () => DeleteItems(deletable), true, "Del");
        }

        private static bool IsUnder(BrowserItem r, BrowserItem folder)
        {
            for (var p = r.Parent; p != null; p = p.Parent) if (p == folder) return true;
            return false;
        }

        // ------------------------------------------------------------------ operations
        private void SelectInstances(List<BrowserItem> items, bool inView, bool show)
        {
            ViewModel.Run(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var view = inView ? app.ActiveUIDocument.ActiveGraphicalView : null;
                var ids = new List<ElementId>();
                foreach (var it in items) ids.AddRange(RevitActions.InstancesOf(doc, it, view));
                RevitActions.Select(app, ids);
                if (show) RevitActions.ShowElements(app, ids);
                ViewModel.Report(string.Format(L.T("msg.selected"), ids.Count));
            });
        }

        private void DeleteItems(List<BrowserItem> items)
        {
            items = items.Where(i => i.IsElement).ToList();
            if (items.Count == 0) return;
            var ids = items.Select(i => i.Id).ToList();
            var names = string.Join("\n", items.Take(12).Select(i => "• " + i.DisplayText)) + (items.Count > 12 ? "\n…" : "");
            var count = items.Count;
            ViewModel.Run(app =>
            {
                if (Settings.Current.ConfirmDelete)
                {
                    var td = new TaskDialog("Project Browser+") { MainInstruction = string.Format(L.T("confirm.delete"), count), MainContent = names, CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, DefaultButton = TaskDialogResult.No };
                    if (td.Show() != TaskDialogResult.Yes) return;
                }
                ViewModel.Report(RevitActions.Delete(app, ids));
            });
        }

        private void BatchRename(List<BrowserItem> items)
        {
            items = items.Where(i => i.IsElement).ToList();
            if (items.Count == 0) return;
            var dlg = new RenameDialog(items);
            DialogHelper.Own(dlg, RevitTask.UIApp);
            if (dlg.ShowDialog() != true) return;
            if (dlg.ApplyNow) ViewModel.ApplyNow(dlg.Result); else ViewModel.Stage(dlg.Result);
        }

        private void Renumber(List<BrowserItem> sheets)
        {
            if (sheets.Count == 0) { ViewModel.Report(L.T("msg.noSheets")); return; }
            var dlg = new RenumberDialog(sheets);
            DialogHelper.Own(dlg, RevitTask.UIApp);
            if (dlg.ShowDialog() != true) return;
            if (dlg.ApplyNow) ViewModel.ApplyNow(dlg.Result); else ViewModel.Stage(dlg.Result);
        }

        private void Duplicate(List<BrowserItem> views)
        {
            if (views.Count == 0) return;
            var dlg = new DuplicateDialog(views.Count);
            DialogHelper.Own(dlg, RevitTask.UIApp);
            if (dlg.ShowDialog() != true) return;
            var ids = views.Select(v => v.Id).ToList();
            ViewModel.Run(app => ViewModel.Report(RevitActions.DuplicateViews(app, ids, dlg.Option, dlg.Copies, dlg.Suffix)));
        }

        private void ApplyTemplate(List<BrowserItem> views)
        {
            if (views.Count == 0) return;
            var ids = views.Select(v => v.Id).ToList();
            ViewModel.Run(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var first = doc.GetElement(ids[0]) as View;
                var list = RevitActions.ViewTemplates(doc, ids.Count == 1 ? first : null);
                var dlg = new PickDialog(L.T("dlg.pickTemplate"), list, false);
                DialogHelper.Own(dlg, app);
                if (dlg.ShowDialog() != true || dlg.SelectedIds.Count == 0) return;
                ViewModel.Report(RevitActions.ApplyTemplate(app, ids, dlg.SelectedIds[0]));
            });
        }

        private void PlaceOnSheets(List<BrowserItem> views)
        {
            if (views.Count == 0) return;
            var ids = views.Select(v => v.Id).ToList();
            ViewModel.Run(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                RevitActions.SheetsList(doc, out var sheets);
                var dlg = new PickDialog(L.T("dlg.pickSheets"), sheets, true) { OptionText = L.T("dlg.samePosition"), OptionChecked = true };
                DialogHelper.Own(dlg, app);
                if (dlg.ShowDialog() != true || dlg.SelectedIds.Count == 0) return;
                ViewModel.Report(RevitActions.PlaceOnSheets(app, ids, dlg.SelectedIds, null, dlg.OptionChecked));
            });
        }

        private void CreateSheetForViews(List<BrowserItem> views)
        {
            if (views.Count == 0) return;
            var ids = views.Select(v => v.Id).ToList();
            ViewModel.Run(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var tbs = RevitActions.TitleBlockTypes(doc);
                tbs.Insert(0, new KeyValuePair<ElementId, string>(ElementId.InvalidElementId, L.T("dlg.noTitleblock")));
                var dlg = new PickDialog(L.T("dlg.pickTitleblock"), tbs, false);
                DialogHelper.Own(dlg, app);
                if (dlg.ShowDialog() != true || dlg.SelectedIds.Count == 0) return;
                ViewModel.Report(RevitActions.CreateSheetForViews(app, ids, dlg.SelectedIds[0]));
            });
        }

        private void CreateSheets()
        {
            ViewModel.Run(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var tbs = RevitActions.TitleBlockTypes(doc);
                var last = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber).OrderBy(n => n, NaturalComparer.Instance).LastOrDefault();
                var dlg = new CreateSheetsDialog(tbs, last == null ? "A-001" : RevitActions.Increment(last));
                DialogHelper.Own(dlg, app);
                if (dlg.ShowDialog() != true) return;
                var msg = RevitActions.CreateSheets(app, dlg.TitleBlockId, dlg.StartNumber, dlg.SheetName, dlg.Count, dlg.Placeholder, out var created);
                ViewModel.Report(msg);
                if (dlg.OpenFirst && created.Count > 0) RevitActions.OpenView(app, created[0]);
            });
        }

        private void CreatePlans()
        {
            var selLevels = ViewModel.TargetItems(i => i.Kind == ItemKind.Level).Select(i => i.Id).ToList();
            ViewModel.Run(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderByDescending(l => l.Elevation).Select(l => new KeyValuePair<ElementId, string>(l.Id, l.Name)).ToList();
                var types = RevitActions.PlanViewTypes(doc);
                var templates = RevitActions.ViewTemplates(doc, null);
                var dlg = new CreateViewsDialog(levels, types, templates, selLevels);
                DialogHelper.Own(dlg, app);
                if (dlg.ShowDialog() != true) return;
                ViewModel.Report(RevitActions.CreatePlans(app, dlg.LevelIds, dlg.ViewTypeId, dlg.NameTemplate, dlg.TemplateId));
            });
        }

        private void SetParameter(List<BrowserItem> items)
        {
            items = items.Where(i => i.IsElement).ToList();
            if (items.Count == 0) return;
            var ids = items.Select(i => i.Id).ToList();
            ViewModel.Run(app =>
            {
                var names = RevitActions.CommonParameters(app.ActiveUIDocument.Document, ids);
                if (names.Count == 0) { ViewModel.Report(L.T("msg.noCommonParams")); return; }
                var dlg = new SetParameterDialog(names, ids.Count);
                DialogHelper.Own(dlg, app);
                if (dlg.ShowDialog() != true) return;
                ViewModel.Report(RevitActions.SetParameter(app, ids, dlg.ParameterName, dlg.Value));
            });
        }

        private void SaveFamily(BrowserItem fam)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = L.T("dlg.saveFamilyFolder") };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var folder = dlg.SelectedPath;
            ViewModel.Run(app => ViewModel.Report(RevitActions.SaveFamily(app, fam.Id, folder) ?? string.Format(L.T("msg.familySaved"), folder)));
        }

        private void AddNote(BrowserItem it)
        {
            var dlg = new InputDialog(L.T("dlg.note"), it.DisplayText, it.Note ?? "");
            DialogHelper.Own(dlg, RevitTask.UIApp);
            if (dlg.ShowDialog() != true) return;
            ViewModel.SetNote(it, dlg.Value);
        }

        private void SelectUnused()
        {
            foreach (var r in ViewModel.Rows) r.IsSelected = false;
            ViewModel.SetExpanded(false);
            var unused = ViewModel.Rows.Where(r => r.Kind == ItemKind.Family && r.IsUnused).ToList();
            foreach (var u in unused) u.IsSelected = true;
            ViewModel.Report(string.Format(L.T("msg.selectedUnused"), unused.Count));
        }

        private void SelectNotOnSheet()
        {
            foreach (var r in ViewModel.Rows) r.IsSelected = false;
            ViewModel.SetExpanded(true);
            var list = ViewModel.Rows.Where(r => r.IsViewLike && r.Kind != ItemKind.Sheet && !r.IsOnSheet && !r.IsFolder && r.Parent?.Kind != ItemKind.Sheet).ToList();
            foreach (var u in list) u.IsSelected = true;
            ViewModel.Report(string.Format(L.T("msg.selected"), list.Count));
        }

        private void ExportCsv()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "project-browser.csv" };
            if (dlg.ShowDialog() != true) return;
            try { System.IO.File.WriteAllText(dlg.FileName, ViewModel.ExportText(true), new System.Text.UTF8Encoding(true)); ViewModel.Report(L.T("msg.exported")); }
            catch (Exception ex) { ViewModel.Report(ex.Message); }
        }

        private static void SafeClipboard(string text)
        {
            try { Clipboard.SetText(text ?? ""); } catch { }
        }
    }
}

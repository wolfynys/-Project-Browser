using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.UI
{
    public class BoolToVis : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            var b = v is bool bb && bb;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class InverseBool : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => !(v is bool b && b);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => !(v is bool b && b);
    }

    public class NullToVis : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            var has = v != null && !(v is string s && s.Length == 0);
            if (Invert) has = !has;
            return has ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class IndentConverter : IValueConverter
    {
        public double Step { get; set; } = 16;
        public object Convert(object v, Type t, object p, CultureInfo c) => new Thickness(Math.Max(0, (v is int i ? i : 0)) * Step + 4, 0, 0, 0);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class ExpanderIcon : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b && b ? "chevron-down" : "chevron-right";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class FavIcon : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b && b ? "star" : "star";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class ColorBrush : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            try { if (v is string s && s.Length > 0) return new SolidColorBrush((Color)ColorConverter.ConvertFromString(s)); } catch { }
            return Brushes.Transparent;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class KindToWeight : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            var k = v is ItemKind kk ? kk : ItemKind.View;
            return k == ItemKind.Folder || k == ItemKind.Category ? FontWeights.SemiBold : FontWeights.Normal;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class CountToText : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => v is int i && i > 0 ? i.ToString() : "";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    public class MultiBoolAnd : IMultiValueConverter
    {
        public object Convert(object[] vs, Type t, object p, CultureInfo c)
        {
            foreach (var v in vs) if (!(v is bool b && b)) return Visibility.Collapsed;
            return Visibility.Visible;
        }
        public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();
    }
}

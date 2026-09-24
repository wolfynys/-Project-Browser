using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProjectBrowserPlus.UI
{
    /// <summary>
    /// Vector Lucide icon: &lt;ui:Icon Kind="folder" Size="16"/&gt;. Stroke follows Foreground.
    /// Geometries live in Icons.xaml (generated from the Lucide SVG set).
    /// </summary>
    public class Icon : Viewbox
    {
        private static ResourceDictionary _dict;
        private readonly Path _path;

        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(Icon), new PropertyMetadata(null, OnKindChanged));
        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(Icon), new PropertyMetadata(16.0, OnSizeChanged));
        public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(Icon), new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.Inherits, OnForegroundChanged));
        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(Icon), new PropertyMetadata(2.0, (d, e) => ((Icon)d)._path.StrokeThickness = (double)e.NewValue));

        public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
        public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

        public Icon()
        {
            _path = new Path
            {
                Width = 24, Height = 24, Stretch = Stretch.None,
                StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Stroke = Brushes.DimGray, SnapsToDevicePixels = false,
            };
            Child = _path;
            Width = 16; Height = 16;
            Stretch = Stretch.Uniform;
            IsHitTestVisible = false;
        }

        public static Geometry Get(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            if (_dict == null)
            {
                _dict = new ResourceDictionary { Source = new Uri("pack://application:,,,/ProjectBrowserPlus;component/UI/Icons.xaml", UriKind.Absolute) };
            }
            var key = "ic." + kind;
            return _dict.Contains(key) ? _dict[key] as Geometry : (_dict.Contains("ic.square") ? _dict["ic.square"] as Geometry : null);
        }

        private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var i = (Icon)d;
            try { i._path.Data = Get((string)e.NewValue); } catch (Exception ex) { Core.Log.Warn("icon " + e.NewValue + ": " + ex.Message); }
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var i = (Icon)d; var s = (double)e.NewValue; i.Width = s; i.Height = s;
        }

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Icon)d)._path.Stroke = e.NewValue as Brush ?? Brushes.DimGray;
        }
    }
}

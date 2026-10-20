using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Linq;

namespace MineDock.App;

public sealed class Icon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(Icon),
        new FrameworkPropertyMetadata("box", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(Icon),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    private static readonly Dictionary<string, List<Geometry>> Cache = new();
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!Cache.TryGetValue(Kind, out var geometries))
        {
            geometries = [];
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MineDock.App.Assets.Icons." + Kind + ".svg");
            if (stream is not null)
            {
                var xml = XDocument.Load(stream);
                double N(XElement e, string attr) => double.Parse(e.Attribute(attr)?.Value ?? "0", CultureInfo.InvariantCulture);
                foreach (var e in xml.Root!.Elements())
                {
                    Geometry? geometry = e.Name.LocalName switch
                    {
                        "path" => Geometry.Parse(e.Attribute("d")!.Value),
                        "circle" => new EllipseGeometry(new Point(N(e, "cx"), N(e, "cy")), N(e, "r"), N(e, "r")),
                        "line" => new LineGeometry(new Point(N(e, "x1"), N(e, "y1")), new Point(N(e, "x2"), N(e, "y2"))),
                        "rect" => new RectangleGeometry(new Rect(N(e, "x"), N(e, "y"), N(e, "width"), N(e, "height")), N(e, "rx"), N(e, "rx")),
                        "polyline" or "polygon" => Geometry.Parse("M" + e.Attribute("points")!.Value + (e.Name.LocalName == "polygon" ? "Z" : "")),
                        _ => null
                    };
                    if (geometry is not null) { geometry.Freeze(); geometries.Add(geometry); }
                }
            }
            Cache[Kind] = geometries;
        }
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        var pen = new Pen(Foreground, 1.75) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        foreach (var geometry in geometries) dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }
}
public sealed class NotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
public sealed class NotVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class EmptyVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class ListItemWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double width && double.IsFinite(width) && width > 13 ? width - 13 : double.PositiveInfinity;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

// 界面基础：主题画刷、通用控件（开关、滑块、菜单、取色器、字体选择器……）
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IO = System.IO;

namespace WorldClock
{
    public static class T
    {
        public static Theme Cur;
        public static FontFamily UI = new FontFamily("Microsoft YaHei UI, Segoe UI");
        public static FontFamily Num = new FontFamily("Bahnschrift, Segoe UI");
        public static readonly FontFamily Icon = new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily Mono = new FontFamily("Consolas, Microsoft YaHei UI");

        public static void SetTheme(Theme t)
        {
            Cur = t;
            UI = new FontFamily(Safe(t.FontUI) + "Microsoft YaHei UI, Segoe UI");
            Num = new FontFamily(Safe(t.FontNum) + "Segoe UI");
        }
        static string Safe(string f) { return string.IsNullOrWhiteSpace(f) ? "" : f.Replace(",", " ") + ", "; }
        public static FontFamily FontOf(string f, string fallback) { return new FontFamily(Safe(f) + fallback); }

        // ── 颜色 ──
        public static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        public static bool TryC(string s, out Color c)
        {
            c = Colors.Transparent;
            try
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                s = s.Trim();
                if (!s.StartsWith("#")) s = "#" + s;
                if (s.Length != 7 && s.Length != 9 && s.Length != 4) return false;
                c = C(s);
                return true;
            }
            catch { return false; }
        }
        public static string Hex(Color c)
        {
            return c.A == 255 ? string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B) : string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }
        public static Color A(Color c, double op) { return Color.FromArgb((byte)Math.Max(0, Math.Min(255, Math.Round(c.A * op))), c.R, c.G, c.B); }
        public static Color Mix(Color a, Color b, double f)
        {
            f = Math.Max(0, Math.Min(1, f));
            return Color.FromArgb((byte)(a.A + (b.A - a.A) * f), (byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f));
        }
        public static double Lum(Color c) { return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0; }

        public static SolidColorBrush B(string hex) { var b = new SolidColorBrush(C(hex)); b.Freeze(); return b; }
        public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        public static SolidColorBrush B(Color c, double op) { return B(A(c, op)); }
        public static SolidColorBrush Fg(double op) { return B(Cur.Text, op); }
        public static SolidColorBrush W(double op) { return B(Colors.White, op); }
        public static SolidColorBrush AccentBrush { get { return B(Cur.Accent); } }
        public static SolidColorBrush HighlightBrush { get { return B(Cur.Highlight); } }
        public static Brush AccentGrad()
        {
            var g = new LinearGradientBrush(Cur.Accent, Cur.Accent2, new Point(0, 0), new Point(1, 1)); g.Freeze(); return g;
        }
        public static Brush OnAccent { get { return Lum(Cur.Accent) > 0.62 ? B("#15171C") : Brushes.White; } }
        public static Color PopupBg { get { return Color.FromArgb(250, Cur.Panel.R, Cur.Panel.G, Cur.Panel.B); } }

        public static FontWeight Weight(string w)
        {
            switch ((w ?? "").ToLowerInvariant())
            {
                case "light": return FontWeights.Light;
                case "medium": return FontWeights.Medium;
                case "semibold": return FontWeights.SemiBold;
                case "bold": return FontWeights.Bold;
                default: return FontWeights.Normal;
            }
        }

        public static TextBlock Text(string s, double size, double op)
        {
            return new TextBlock
            {
                Text = s, FontSize = size, Foreground = Fg(op),
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
            };
        }
        public static TextBlock Glyph(string g, double size, Brush fg)
        {
            return new TextBlock { Text = g, FontFamily = Icon, FontSize = size, Foreground = fg, VerticalAlignment = VerticalAlignment.Center };
        }

        // ── 背景图缓存 ──
        static readonly Dictionary<string, ImageSource> imgCache = new Dictionary<string, ImageSource>();
        public static ImageSource LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            ImageSource img;
            if (imgCache.TryGetValue(path, out img)) return img;
            try
            {
                if (!IO.File.Exists(path)) return null;
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(path); bi.DecodePixelWidth = 1600; bi.EndInit(); bi.Freeze();
                img = bi;
            }
            catch { img = null; }
            imgCache[path] = img;
            return img;
        }

        // 窗口外壳：阴影 + 面板（渐变 / 背景图）。返回可放内容的层
        public static Grid Shell(Grid root)
        {
            var t = Cur;
            var r = new CornerRadius(t.Radius);
            if (t.Shadow)
                root.Children.Add(new Border
                {
                    CornerRadius = r, Background = B(t.Panel2, Math.Max(0.25, t.PanelOpacity * 0.6)), Margin = new Thickness(1),
                    Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.42, Color = Colors.Black }
                });
            var bg = new LinearGradientBrush(A(t.Panel, t.PanelOpacity), A(t.Panel2, t.PanelOpacity), new Point(0, 0), new Point(0.4, 1));
            var bd = new LinearGradientBrush(A(t.Text, t.BorderOpacity * 1.6), A(t.Text, t.BorderOpacity * 0.4), new Point(0, 0), new Point(0, 1));
            var shell = new Border { CornerRadius = r, BorderThickness = new Thickness(t.BorderOpacity > 0 ? 1 : 0), BorderBrush = bd, Background = bg };
            var layer = new Grid();
            var img = LoadImage(t.BgImage);
            if (img != null)
                layer.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(Math.Max(0, t.Radius - 1)), Opacity = t.BgImageOpacity, IsHitTestVisible = false,
                    Background = new ImageBrush(img) { Stretch = Stretch.UniformToFill }
                });
            shell.Child = layer;
            root.Children.Add(shell);
            return layer;
        }

        public static Border Pill(string text, Action click, string kind)
        {
            Brush bg, hover; Brush fg = Fg(0.95);
            if (kind == "primary") { bg = AccentGrad(); hover = AccentGrad(); fg = OnAccent; }
            else if (kind == "danger") { bg = B(C("#E5484D"), 0.25); hover = B(C("#E5484D"), 0.42); }
            else { bg = Fg(0.09); hover = Fg(0.17); }
            var tb = Text(text, 12.5, 1);
            tb.Foreground = fg;
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            var b = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(13, 6, 13, 6), Background = bg, Child = tb, Cursor = Cursors.Hand };
            b.MouseEnter += delegate { b.Background = hover; b.Opacity = kind == "primary" ? 0.88 : 1; };
            b.MouseLeave += delegate { b.Background = bg; b.Opacity = 1; };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; click(); };
            return b;
        }

        public static TextBox Input(string text, double size)
        {
            return new TextBox
            {
                Text = text, FontSize = size, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = Fg(0.95), CaretBrush = Fg(1), SelectionBrush = AccentBrush, Padding = new Thickness(0),
                FontFamily = UI, VerticalContentAlignment = VerticalAlignment.Center
            };
        }
        public static Border Box(UIElement child)
        {
            return new Border { CornerRadius = new CornerRadius(8), Background = Fg(0.06), BorderBrush = Fg(0.12), BorderThickness = new Thickness(1), Padding = new Thickness(9, 5, 9, 5), Child = child };
        }

        public static ResourceDictionary ScrollStyle()
        {
            string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type ScrollBar}'>
 <Setter Property='Width' Value='6'/><Setter Property='MinWidth' Value='6'/>
 <Setter Property='Template'><Setter.Value>
  <ControlTemplate TargetType='{x:Type ScrollBar}'>
   <Track x:Name='PART_Track' IsDirectionReversed='True'>
    <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border CornerRadius='3' Background='COLOR'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
   </Track>
  </ControlTemplate></Setter.Value></Setter>
</Style></ResourceDictionary>".Replace("COLOR", Hex(A(Cur.Text, 0.25)));
            return (ResourceDictionary)XamlReader.Parse(xaml);
        }

        public static void Fade(UIElement e, double to)
        {
            e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(180)));
        }
    }

    public class IconButton : Border
    {
        readonly TextBlock tb;
        bool active;
        public IconButton(string glyph, string tip, Action click, double size = 28, double font = 12)
        {
            tb = T.Glyph(glyph, font, T.Fg(0.9));
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            Width = size; Height = size; CornerRadius = new CornerRadius(8); Background = Brushes.Transparent; Child = tb; Cursor = Cursors.Hand;
            if (!string.IsNullOrEmpty(tip)) ToolTip = tip;
            MouseEnter += delegate { Background = T.Fg(0.12); };
            MouseLeave += delegate { Background = active ? (Brush)T.B(T.Cur.Accent, 0.18) : Brushes.Transparent; };
            MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            MouseLeftButtonUp += (s, e) => { e.Handled = true; click(); };
        }
        public string Glyph { set { tb.Text = value; } }
        public bool Active
        {
            set
            {
                active = value;
                tb.Foreground = value ? (Brush)T.AccentBrush : T.Fg(0.9);
                Background = value ? (Brush)T.B(T.Cur.Accent, 0.18) : Brushes.Transparent;
            }
        }
    }

    public class ToggleSwitch : Border
    {
        bool on;
        readonly TranslateTransform tt = new TranslateTransform();
        readonly Border knob;
        readonly Action<bool> changed;
        public ToggleSwitch(bool initial, Action<bool> onChanged)
        {
            on = initial; changed = onChanged;
            Width = 40; Height = 22; CornerRadius = new CornerRadius(11); Cursor = Cursors.Hand; VerticalAlignment = VerticalAlignment.Center;
            knob = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0),
                RenderTransform = tt, Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.3 }
            };
            Child = knob;
            Apply(false);
            MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            MouseLeftButtonUp += (s, e) => { e.Handled = true; on = !on; Apply(true); if (changed != null) changed(on); };
        }
        void Apply(bool anim)
        {
            Background = on ? T.AccentGrad() : T.Fg(0.18);
            knob.Background = on ? T.OnAccent : Brushes.White;
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(on ? 18 : 0, TimeSpan.FromMilliseconds(anim ? 160 : 0)) { EasingFunction = new QuadraticEase() });
        }
    }

    public class FlatSlider : Grid
    {
        public double Min, Max, Step;
        double v;
        readonly bool centerFill;
        public Action<double> Changed;
        readonly Canvas cv = new Canvas();
        readonly Border track, fill;
        readonly Ellipse thumb;

        public FlatSlider(double min, double max, double step, double value, bool center)
        {
            Min = min; Max = max; Step = step; centerFill = center;
            v = Clamp(Snap(value));
            Height = 24; Background = Brushes.Transparent; Cursor = Cursors.Hand;
            track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = T.Fg(0.16) };
            fill = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = T.AccentGrad() };
            thumb = new Ellipse { Width = 16, Height = 16, Fill = Brushes.White, Stroke = T.Fg(0.12), StrokeThickness = 1, Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.35 } };
            cv.Children.Add(track); cv.Children.Add(fill); cv.Children.Add(thumb);
            Children.Add(cv);
            SizeChanged += delegate { Layout(); };
            MouseLeftButtonDown += (s, e) => { CaptureMouse(); FromPoint(e.GetPosition(this)); e.Handled = true; };
            MouseMove += (s, e) => { if (IsMouseCaptured) FromPoint(e.GetPosition(this)); };
            MouseLeftButtonUp += (s, e) => { ReleaseMouseCapture(); e.Handled = true; };
            MouseWheel += (s, e) => { Value = v + (e.Delta > 0 ? Step : -Step); e.Handled = true; };
        }

        double Snap(double x) { return Math.Round((x - Min) / Step) * Step + Min; }
        double Clamp(double x) { return Math.Max(Min, Math.Min(Max, x)); }

        public double Value
        {
            get { return v; }
            set
            {
                double nv = Clamp(Snap(value));
                if (Math.Abs(nv - v) < 1e-9) { Layout(); return; }
                v = nv; Layout();
                if (Changed != null) Changed(v);
            }
        }

        void FromPoint(Point p)
        {
            double inner = ActualWidth - 16;
            if (inner <= 0) return;
            Value = Min + (p.X - 8) / inner * (Max - Min);
        }

        void Layout()
        {
            double w = ActualWidth;
            if (w <= 16) return;
            double inner = w - 16;
            double x = 8 + (v - Min) / (Max - Min) * inner;
            double x0 = centerFill ? 8 + (0 - Min) / (Max - Min) * inner : 8;
            track.Width = inner; Canvas.SetLeft(track, 8); Canvas.SetTop(track, 10);
            Canvas.SetLeft(fill, Math.Min(x0, x)); fill.Width = Math.Abs(x - x0); Canvas.SetTop(fill, 10);
            Canvas.SetLeft(thumb, x - 8); Canvas.SetTop(thumb, 4);
        }
    }

    public static class Seg
    {
        // 分段选择器
        public static Border Make(string[] labels, string[] values, string current, Action<string> pick, double fontSize = 12.5)
        {
            var sp = new WrapPanel();
            var outer = new Border { CornerRadius = new CornerRadius(10), Background = T.Fg(0.07), Padding = new Thickness(3), Child = sp, HorizontalAlignment = HorizontalAlignment.Left };
            var items = new List<Border>();
            for (int i = 0; i < labels.Length; i++)
            {
                var tb = T.Text(labels[i], fontSize, 0.95);
                var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5, 12, 5), Child = tb, Cursor = Cursors.Hand, Tag = values[i] };
                items.Add(b);
                sp.Children.Add(b);
            }
            Action<string> paint = cur =>
            {
                foreach (var b in items)
                {
                    bool sel = (string)b.Tag == cur;
                    b.Background = sel ? T.AccentGrad() : Brushes.Transparent;
                    ((TextBlock)b.Child).Foreground = sel ? T.OnAccent : T.Fg(0.85);
                }
            };
            paint(current);
            foreach (var b in items)
            {
                var bb = b;
                bb.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                bb.MouseLeftButtonUp += (s, e) => { e.Handled = true; paint((string)bb.Tag); pick((string)bb.Tag); };
            }
            return outer;
        }
    }

    // 自绘弹出菜单
    public static class PopMenu
    {
        public class Item
        {
            public string Glyph, Text; public bool Checked, Sep, Danger; public Action Act;
            public static Item Of(string glyph, string text, Action a) { return new Item { Glyph = glyph, Text = text, Act = a }; }
            public static Item Check(string glyph, string text, bool on, Action a) { return new Item { Glyph = glyph, Text = text, Act = a, Checked = on }; }
            public static Item Separator() { return new Item { Sep = true }; }
        }

        public static Border Frame(UIElement child)
        {
            var border = new Border
            {
                Background = T.B(T.PopupBg), BorderBrush = T.Fg(0.14), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(5), Margin = new Thickness(12), Child = child,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = 0.4 }
            };
            TextElement.SetFontFamily(border, T.UI);
            TextElement.SetForeground(border, T.Fg(1));
            return border;
        }

        public static void Show(UIElement target, IList<Item> items, PlacementMode mode = PlacementMode.MousePoint)
        {
            var popup = new Popup { AllowsTransparency = true, StaysOpen = false, Placement = mode, PopupAnimation = PopupAnimation.Fade, PlacementTarget = target };
            var sp = new StackPanel();
            foreach (var it in items)
            {
                if (it.Sep) { sp.Children.Add(new Border { Height = 1, Background = T.Fg(0.1), Margin = new Thickness(8, 4, 8, 4) }); continue; }
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                var red = T.B("#F87171");
                var ic = T.Glyph(it.Glyph ?? "", 12, it.Danger ? (Brush)red : T.Fg(0.75));
                var tx = T.Text(it.Text, 13, 0.95);
                if (it.Danger) tx.Foreground = red;
                Grid.SetColumn(tx, 1);
                g.Children.Add(ic); g.Children.Add(tx);
                if (it.Checked)
                {
                    var ck = T.Glyph("", 12, T.AccentBrush);
                    ck.HorizontalAlignment = HorizontalAlignment.Right;
                    Grid.SetColumn(ck, 2); g.Children.Add(ck);
                }
                var row = new Border { Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Child = g, Cursor = Cursors.Hand };
                row.MouseEnter += delegate { row.Background = T.Fg(0.09); };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                var act = it.Act;
                row.MouseLeftButtonUp += delegate { popup.IsOpen = false; if (act != null) act(); };
                sp.Children.Add(row);
            }
            var frame = Frame(sp);
            frame.MinWidth = 200;
            popup.Child = frame;
            popup.IsOpen = true;
        }
    }

    // ───────────────────────────── 取色器 ─────────────────────────────
    public static class ColorPicker
    {
        public static bool IsOpen;
        static readonly string[] Palette = {
            "#FFFFFF", "#E5E7EB", "#9CA3AF", "#4B5563", "#1F2937", "#000000",
            "#F87171", "#EF4444", "#F97316", "#F59E0B", "#FACC15", "#84CC16",
            "#22C55E", "#10B981", "#14B8A6", "#06B6D4", "#0EA5E9", "#3B82F6",
            "#6366F1", "#7C8CFF", "#8B5CF6", "#A855F7", "#D946EF", "#EC4899",
            "#FFF1F2", "#FEF3C7", "#ECFCCB", "#E0F2FE", "#EDE9FE", "#1B2032" };

        public static void Show(FrameworkElement target, Color start, Action<Color> changed)
        {
            double h, s, v;
            ToHsv(start, out h, out s, out v);
            byte alpha = start.A;
            const double W = 228, H = 140;
            var popup = new Popup { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Bottom, PlacementTarget = target, PopupAnimation = PopupAnimation.Fade };
            var sp = new StackPanel { Width = W };

            var sv = new Grid { Width = W, Height = H, Cursor = Cursors.Cross, Background = Brushes.Transparent };
            var hueRect = new Border { CornerRadius = new CornerRadius(8) };
            var white = new Border { CornerRadius = new CornerRadius(8), Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) };
            var black = new Border { CornerRadius = new CornerRadius(8), Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) };
            var marker = new Ellipse { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.8 } };
            sv.Children.Add(hueRect); sv.Children.Add(white); sv.Children.Add(black); sv.Children.Add(marker);

            var hueBar = new Grid { Height = 14, Margin = new Thickness(0, 10, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            string[] hs = { "#FF0000", "#FFFF00", "#00FF00", "#00FFFF", "#0000FF", "#FF00FF", "#FF0000" };
            for (int i = 0; i < hs.Length; i++) rainbow.GradientStops.Add(new GradientStop(T.C(hs[i]), i / 6.0));
            var hm = new Border { Width = 8, Height = 18, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.White, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, -2, 0, -2), IsHitTestVisible = false, Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.8 } };
            hueBar.Children.Add(new Border { CornerRadius = new CornerRadius(7), Background = rainbow });
            hueBar.Children.Add(hm);

            var pal = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            var hexTb = T.Text("", 12, 0.75);
            hexTb.FontFamily = T.Mono;
            hexTb.Margin = new Thickness(2, 8, 0, 0);

            Action paint = () =>
            {
                hueRect.Background = new SolidColorBrush(FromHsv(h, 1, 1, 255));
                marker.Margin = new Thickness(s * W - 7, (1 - v) * H - 7, 0, 0);
                hm.Margin = new Thickness(h / 360 * (W - 8), -2, 0, -2);
                hexTb.Text = T.Hex(FromHsv(h, s, v, alpha));
            };
            Action commit = () => { paint(); changed(FromHsv(h, s, v, alpha)); };

            MouseButtonEventHandler svDown = (o, e) => { sv.CaptureMouse(); var p = e.GetPosition(sv); s = Cl(p.X / W); v = 1 - Cl(p.Y / H); commit(); e.Handled = true; };
            sv.MouseLeftButtonDown += svDown;
            sv.MouseMove += (o, e) => { if (!sv.IsMouseCaptured) return; var p = e.GetPosition(sv); s = Cl(p.X / W); v = 1 - Cl(p.Y / H); commit(); };
            sv.MouseLeftButtonUp += (o, e) => { sv.ReleaseMouseCapture(); };
            hueBar.MouseLeftButtonDown += (o, e) => { hueBar.CaptureMouse(); h = Cl(e.GetPosition(hueBar).X / W) * 359.9; commit(); e.Handled = true; };
            hueBar.MouseMove += (o, e) => { if (!hueBar.IsMouseCaptured) return; h = Cl(e.GetPosition(hueBar).X / W) * 359.9; commit(); };
            hueBar.MouseLeftButtonUp += (o, e) => { hueBar.ReleaseMouseCapture(); };

            foreach (var hex in Palette)
            {
                var col = T.C(hex);
                var sw = new Border { Width = 30, Height = 20, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(col), Margin = new Thickness(0, 0, 8, 8), BorderBrush = T.Fg(0.2), BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = hex };
                sw.MouseLeftButtonUp += delegate { ToHsv(col, out h, out s, out v); commit(); };
                pal.Children.Add(sw);
            }

            sp.Children.Add(sv); sp.Children.Add(hueBar); sp.Children.Add(pal); sp.Children.Add(hexTb);
            var frame = PopMenu.Frame(sp);
            frame.Padding = new Thickness(12);
            popup.Child = frame;
            popup.Closed += delegate { IsOpen = false; };
            paint();
            IsOpen = true;
            popup.IsOpen = true;
        }

        static double Cl(double x) { return Math.Max(0, Math.Min(1, x)); }

        public static void ToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            v = max; s = max == 0 ? 0 : d / max;
            if (d == 0) h = 0;
            else if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }

        public static Color FromHsv(double h, double s, double v, byte a)
        {
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromArgb(a, (byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
    }

    public class ColorField : StackPanel
    {
        Color c;
        readonly Border sw;
        readonly TextBox hex;
        readonly Action<Color> changed;

        public ColorField(Color init, Action<Color> onChange)
        {
            Orientation = Orientation.Horizontal; c = init; changed = onChange;
            sw = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(8), BorderBrush = T.Fg(0.25), BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Background = new SolidColorBrush(c), ToolTip = "点击打开取色器" };
            hex = T.Input(T.Hex(c), 12.5);
            hex.FontFamily = T.Mono;
            hex.Width = 82;
            var box = T.Box(hex);
            box.Margin = new Thickness(8, 0, 0, 0);
            Children.Add(sw); Children.Add(box);
            sw.MouseLeftButtonDown += (o, e) => { e.Handled = true; };
            sw.MouseLeftButtonUp += (o, e) =>
            {
                e.Handled = true;
                ColorPicker.Show(sw, c, col => { c = col; sw.Background = new SolidColorBrush(col); hex.Text = T.Hex(col); changed(col); });
            };
            hex.KeyDown += (o, e) => { if (e.Key == Key.Enter) Commit(); };
            hex.LostFocus += delegate { Commit(); };
        }

        void Commit()
        {
            Color x;
            if (T.TryC(hex.Text, out x)) { if (x != c) { c = x; sw.Background = new SolidColorBrush(x); changed(x); } }
            hex.Text = T.Hex(c);
        }
    }

    // ───────────────────────────── 字体选择 ─────────────────────────────
    public class FontPicker : Border
    {
        public static bool IsOpen;
        static List<KeyValuePair<string, string>> fonts;   // Source, 显示名
        readonly TextBlock label;
        readonly Action<string> pick;

        static List<KeyValuePair<string, string>> AllFonts()
        {
            if (fonts != null) return fonts;
            var zh = XmlLanguage.GetLanguage("zh-cn");
            fonts = new List<KeyValuePair<string, string>>();
            try
            {
                foreach (var f in Fonts.SystemFontFamilies)
                {
                    string local;
                    string disp = f.FamilyNames.TryGetValue(zh, out local) && local != f.Source ? local + "  (" + f.Source + ")" : f.Source;
                    fonts.Add(new KeyValuePair<string, string>(f.Source, disp));
                }
            }
            catch { }
            fonts = fonts.GroupBy(x => x.Key).Select(g => g.First()).OrderBy(x => x.Value, StringComparer.CurrentCulture).ToList();
            return fonts;
        }

        public FontPicker(string current, Action<string> onPick)
        {
            pick = onPick;
            CornerRadius = new CornerRadius(8); Background = T.Fg(0.07); BorderBrush = T.Fg(0.12); BorderThickness = new Thickness(1);
            Padding = new Thickness(11, 5, 9, 5); Cursor = Cursors.Hand; MinWidth = 170;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            label = T.Text(current, 13, 0.95);
            label.FontFamily = T.FontOf(current, "Segoe UI");
            label.MaxWidth = 170;
            var chev = T.Glyph("", 10, T.Fg(0.6));
            chev.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(chev, 1);
            g.Children.Add(label); g.Children.Add(chev);
            Child = g;
            MouseLeftButtonDown += (o, e) => { e.Handled = true; };
            MouseLeftButtonUp += (o, e) => { e.Handled = true; Open(); };
        }

        void Open()
        {
            var popup = new Popup { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Bottom, PlacementTarget = this, PopupAnimation = PopupAnimation.Fade };
            var sp = new StackPanel { Width = 300 };
            var search = T.Input("", 13);
            var ph = T.Text("搜索字体…", 13, 0.4); ph.IsHitTestVisible = false;
            var sg = new Grid(); sg.Children.Add(search); sg.Children.Add(ph);
            sp.Children.Add(T.Box(sg));
            var list = new StackPanel();
            var scroll = new ScrollViewer { Height = 320, Margin = new Thickness(0, 8, 0, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list };
            scroll.Resources.MergedDictionaries.Add(T.ScrollStyle());
            sp.Children.Add(scroll);
            Action fill = () =>
            {
                list.Children.Clear();
                string q = search.Text.Trim();
                ph.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                foreach (var f in AllFonts().Where(x => q.Length == 0 || x.Value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(160))
                {
                    var src = f.Key;
                    var tb = T.Text(f.Value, 14, 0.95);
                    tb.FontFamily = new FontFamily(src);
                    var row = new Border { Padding = new Thickness(8, 5, 8, 5), CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Child = tb, Cursor = Cursors.Hand };
                    row.MouseEnter += delegate { row.Background = T.Fg(0.09); };
                    row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                    row.MouseLeftButtonUp += delegate
                    {
                        popup.IsOpen = false;
                        label.Text = src; label.FontFamily = T.FontOf(src, "Segoe UI");
                        pick(src);
                    };
                    list.Children.Add(row);
                }
            };
            search.TextChanged += delegate { fill(); };
            fill();
            var frame = PopMenu.Frame(sp);
            frame.Padding = new Thickness(10);
            popup.Child = frame;
            popup.Closed += delegate { IsOpen = false; };
            IsOpen = true;
            popup.IsOpen = true;
            popup.Dispatcher.BeginInvoke(new Action(() => search.Focus()), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}

// 城市卡片
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WorldClock
{
    public class CardOpts { public bool Horiz, Timeline, Weather, Holidays; }

    public class CityCard : Border
    {
        public readonly CityInfo City;
        readonly TimeZoneInfo tz;
        readonly bool valid, dyn, soft;
        readonly CardOpts o;
        readonly Theme th;
        readonly Color fgC;
        readonly TextBlock nameTb, wxTb, dateTb, offTb, chipTb, badgeTb, timeTb, holTb;
        readonly Run ampmRun, hmRun, secRun;
        readonly Border badge;
        readonly Ellipse chipDot;
        readonly Canvas icon, tl;
        readonly Border tlTrack, tlWork;
        readonly Ellipse tlMarker;
        readonly StackPanel holRow;
        readonly GradientStop g0, g1;
        int lastKey = -1, isDay = -1;
        DateTime lastHolDate;
        double frac;

        SolidColorBrush F(double op) { return T.B(fgC, op); }

        public CityCard(CityInfo c, CardOpts opts)
        {
            City = c; o = opts; th = T.Cur;
            tz = TimeUtil.Find(c.TzId);
            valid = tz != null;
            if (!valid) tz = TimeZoneInfo.Utc;

            string st = th.CardStyle;
            bool onColor = st == "sky" || st == "accent";
            fgC = onColor ? (st == "accent" && T.Lum(th.Accent) > 0.62 ? T.C("#15171C") : Colors.White) : th.Text;

            CornerRadius = new CornerRadius(th.CardRadius);
            BorderThickness = new Thickness(st == "outline" ? 1.5 : 1);
            g0 = new GradientStop(Colors.Black, 0); g1 = new GradientStop(Colors.Black, 1);
            switch (st)
            {
                case "sky":
                case "soft":
                    dyn = true; soft = st == "soft";
                    var lg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                    lg.GradientStops.Add(g0); lg.GradientStops.Add(g1);
                    Background = lg;
                    BorderBrush = soft ? T.Fg(0.08) : T.W(0.13);
                    break;
                case "accent":
                    Background = new LinearGradientBrush(th.Accent, th.Accent2, 45);
                    BorderBrush = T.W(0.15);
                    break;
                case "outline":
                    Background = T.B(th.Card, th.CardOpacity);
                    BorderBrush = T.AccentGrad();
                    break;
                default:
                    Background = T.B(th.Card, th.CardOpacity);
                    BorderBrush = T.Fg(0.08);
                    break;
            }

            double extra = Math.Max(0, th.TimeSize - 44);
            Width = o.Horiz ? 216 + extra * 2.4 : 356 + extra * 2.8;
            Margin = o.Horiz ? new Thickness(0, 0, 10, 0) : new Thickness(0, 0, 0, 10);

            var host = new Grid();
            double ir = Math.Max(0, th.CardRadius - 1);
            host.SizeChanged += delegate { host.Clip = new RectangleGeometry(new Rect(0, 0, host.ActualWidth, host.ActualHeight), ir, ir); };
            if (onColor) host.Children.Add(new Border { Background = new LinearGradientBrush(T.C("#00000000"), T.C("#38000000"), new Point(0, 0), new Point(0, 1)) });
            if (th.Glow)
            {
                var gc = onColor ? fgC : th.Accent;
                host.Children.Add(new Ellipse
                {
                    Width = 170, Height = 170, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -80, -60, 0), IsHitTestVisible = false,
                    Fill = new RadialGradientBrush(T.A(gc, onColor ? 0.2 : 0.22), T.A(gc, 0))
                });
            }
            Child = host;

            // —— 元素 ——
            icon = new Canvas { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
            nameTb = new TextBlock { Text = c.Name + (valid ? "" : "（时区无效）"), FontSize = th.NameSize, FontWeight = FontWeights.SemiBold, Foreground = F(1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            wxTb = new TextBlock { FontSize = 12, Foreground = F(0.78), Margin = new Thickness(9, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(icon); nameRow.Children.Add(nameTb); nameRow.Children.Add(wxTb);

            dateTb = new TextBlock { FontSize = 12, Foreground = F(0.78), Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };

            chipDot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(1, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            chipTb = new TextBlock { FontSize = 11.5, Foreground = F(0.85), VerticalAlignment = VerticalAlignment.Center };
            badgeTb = new TextBlock { FontSize = 10.5, Foreground = F(1) };
            badge = new Border { CornerRadius = new CornerRadius(6), Background = F(0.18), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), Child = badgeTb, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            var chipRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0) };
            chipRow.Children.Add(chipDot); chipRow.Children.Add(chipTb); chipRow.Children.Add(badge);

            holTb = new TextBlock { FontSize = 11.5, Foreground = F(0.85), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            holRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0), Visibility = Visibility.Collapsed };
            var hg = T.Glyph("", 11, T.B("#F9A8D4"));
            hg.Margin = new Thickness(0, 0, 6, 0);
            holRow.Children.Add(hg); holRow.Children.Add(holTb);

            double ts = o.Horiz ? th.TimeSize * 0.9 : th.TimeSize;
            ampmRun = new Run { FontSize = Math.Max(11, ts * 0.32), FontFamily = T.UI };
            hmRun = new Run { FontSize = ts };
            secRun = new Run { FontSize = ts * 0.48, Foreground = F(0.62) };
            timeTb = new TextBlock { FontFamily = T.Num, FontWeight = T.Weight(th.TimeWeight), Foreground = F(1) };
            timeTb.Inlines.Add(ampmRun); timeTb.Inlines.Add(hmRun); timeTb.Inlines.Add(secRun);
            TextOptions.SetTextRenderingMode(timeTb, TextRenderingMode.Grayscale);

            offTb = new TextBlock { FontSize = 11.5, Foreground = F(0.75), TextTrimming = TextTrimming.CharacterEllipsis };

            tl = new Canvas { Height = 10, Margin = new Thickness(0, 12, 0, 0), ToolTip = "24 小时时间轴（亮段为工作时间 09:00–18:00）" };
            tlTrack = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = F(0.18) };
            tlWork = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = onColor ? F(0.55) : (Brush)T.B(th.Accent, 0.7) };
            var mc = onColor ? fgC : th.Accent;
            tlMarker = new Ellipse { Width = 10, Height = 10, Fill = T.B(mc), Effect = new DropShadowEffect { Color = mc, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 } };
            Canvas.SetTop(tlTrack, 3); Canvas.SetTop(tlWork, 3); Canvas.SetTop(tlMarker, 0);
            tl.Children.Add(tlTrack); tl.Children.Add(tlWork); tl.Children.Add(tlMarker);
            tl.SizeChanged += delegate { LayoutTl(); };

            if (!o.Timeline) { tl.Visibility = Visibility.Collapsed; chipRow.Visibility = Visibility.Collapsed; }

            // —— 布局 ——
            if (o.Horiz)
            {
                var sp = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
                timeTb.Margin = new Thickness(0, 8, 0, 2);
                offTb.Margin = new Thickness(0, 2, 0, 0);
                sp.Children.Add(nameRow); sp.Children.Add(timeTb); sp.Children.Add(dateTb); sp.Children.Add(offTb); sp.Children.Add(chipRow); sp.Children.Add(holRow); sp.Children.Add(tl);
                host.Children.Add(sp);
            }
            else
            {
                var g = new Grid { Margin = new Thickness(18, 14, 18, 14) };
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                left.Children.Add(nameRow); left.Children.Add(dateTb); left.Children.Add(chipRow); left.Children.Add(holRow);
                var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 0, 0, 0) };
                timeTb.HorizontalAlignment = HorizontalAlignment.Right;
                timeTb.Margin = new Thickness(0, -ts * 0.14, 0, 0);
                offTb.HorizontalAlignment = HorizontalAlignment.Right;
                right.Children.Add(timeTb); right.Children.Add(offTb);
                Grid.SetColumn(right, 1);
                Grid.SetRow(tl, 1); Grid.SetColumnSpan(tl, 2);
                g.Children.Add(left); g.Children.Add(right); g.Children.Add(tl);
                host.Children.Add(g);
            }
        }

        public DateTime LocalOf(DateTime utc) { return TimeZoneInfo.ConvertTimeFromUtc(utc, tz); }
        public TimeSpan OffsetOf(DateTime utc) { return tz.GetUtcOffset(utc); }

        public void SetWeather(Weather.Info w)
        {
            wxTb.Text = w == null ? "" : (Weather.Describe(w.Code) + " " + Math.Round(w.Temp) + "°").Trim();
        }

        public void Update(DateTime utc, bool converting, AppSettings s, TimeZoneInfo baseTz)
        {
            DateTime t = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            TimeSpan off = tz.GetUtcOffset(utc);
            DateTime bt = TimeZoneInfo.ConvertTimeFromUtc(utc, baseTz);
            TimeSpan boff = baseTz.GetUtcOffset(utc);

            if (s.Use24h) { ampmRun.Text = ""; hmRun.Text = t.ToString("HH:mm"); }
            else
            {
                ampmRun.Text = t.Hour < 12 ? "上午 " : "下午 ";
                int h12 = t.Hour % 12; if (h12 == 0) h12 = 12;
                hmRun.Text = h12 + ":" + t.ToString("mm");
            }
            secRun.Text = (s.ShowSeconds && !converting) ? ":" + t.ToString("ss") : "";
            hmRun.Foreground = converting ? (Brush)T.HighlightBrush : F(1);

            dateTb.Text = (City.Sub.Length > 0 ? City.Sub + " · " : "") + TimeUtil.DateText(t);
            offTb.Text = TimeUtil.Offset(off) + " · " + (City.IsBase ? "基准" : TimeUtil.Diff(off - boff));

            int dd = (t.Date - bt.Date).Days;
            if (dd == 0 || City.IsBase) badge.Visibility = Visibility.Collapsed;
            else { badge.Visibility = Visibility.Visible; badgeTb.Text = dd > 0 ? "明天" : "昨天"; }

            string country = s.ShowHolidays ? City.Country : "";
            string st;
            int score = TimeUtil.Score(t, country, out st);
            string dc = score == 2 ? "#34D399" : st == "节假日" ? "#F472B6" : st == "周末" ? "#FBBF24" : score == 1 ? "#FB923C" : "#9AA8C7";
            chipTb.Text = st;
            chipDot.Fill = T.B(dc);
            bool offDay = st == "周末" || st == "节假日" || t.DayOfWeek == DayOfWeek.Saturday || t.DayOfWeek == DayOfWeek.Sunday;
            tlWork.Opacity = offDay ? 0.35 : 1;

            if (t.Date != lastHolDate)
            {
                lastHolDate = t.Date;
                string hol = Holidays.Get(country, t.Date);
                DateTime when; string next;
                if (!o.Holidays || !s.ShowHolidays) holRow.Visibility = Visibility.Collapsed;
                else if (hol != null) { holRow.Visibility = Visibility.Visible; holTb.Text = "今天放假：" + hol; }
                else if (Holidays.Next(country, t.Date, 14, out when, out next))
                {
                    int n = (when - t.Date).Days;
                    holRow.Visibility = Visibility.Visible;
                    holTb.Text = when.Month + "月" + when.Day + "日 " + next + " · " + (n == 1 ? "明天" : n + " 天后");
                }
                else holRow.Visibility = Visibility.Collapsed;
            }

            double hf = t.Hour + t.Minute / 60.0;
            int key = t.Hour * 60 + t.Minute;
            if (key != lastKey)
            {
                lastKey = key;
                if (dyn)
                {
                    Color a, b;
                    Sky.Get(hf, out a, out b);
                    if (soft) { a = T.Mix(th.Panel, a, th.SkyStrength); b = T.Mix(th.Panel2, b, th.SkyStrength); }
                    g0.Color = a; g1.Color = b;
                }
                int d = Sky.IsDay(hf) ? 1 : 0;
                if (d != isDay) { isDay = d; DrawIcon(d == 1); }
            }
            frac = (hf + t.Second / 3600.0) / 24.0;
            LayoutTl();
        }

        void LayoutTl()
        {
            double w = tl.ActualWidth;
            if (w <= 0) return;
            tlTrack.Width = w;
            Canvas.SetLeft(tlWork, w * 9 / 24); tlWork.Width = w * 9 / 24;
            Canvas.SetLeft(tlMarker, Math.Max(0, Math.Min(w - 10, frac * w - 5)));
        }

        void DrawIcon(bool day)
        {
            icon.Children.Clear();
            if (day)
            {
                var e = new Ellipse
                {
                    Width = 14, Height = 14,
                    Fill = new RadialGradientBrush(T.C("#FFF6C2"), T.C("#FFB938")),
                    Effect = new DropShadowEffect { Color = T.C("#FFD27A"), BlurRadius = 12, ShadowDepth = 0, Opacity = 1 }
                };
                Canvas.SetLeft(e, 2); Canvas.SetTop(e, 2);
                icon.Children.Add(e);
            }
            else
            {
                bool light = T.Lum(fgC) < 0.5;   // 浅色卡片上用深色月亮
                var mc = light ? T.C("#5B5F97") : T.C("#EEF1FF");
                var geo = new CombinedGeometry(GeometryCombineMode.Exclude,
                    new EllipseGeometry(new Point(9, 9), 7, 7), new EllipseGeometry(new Point(13.5, 5.5), 6, 6));
                icon.Children.Add(new Path { Data = geo, Fill = new SolidColorBrush(mc), Effect = new DropShadowEffect { Color = T.C("#A5B4FC"), BlurRadius = 10, ShadowDepth = 0, Opacity = light ? 0.4 : 0.9 } });
                var star = new Ellipse { Width = 2.4, Height = 2.4, Fill = new SolidColorBrush(T.A(mc, 0.85)) };
                Canvas.SetLeft(star, 14.5); Canvas.SetTop(star, 11);
                icon.Children.Add(star);
            }
        }

        public string CopyText(DateTime utc)
        {
            var t = LocalOf(utc);
            return City.Name + (City.Sub.Length > 0 ? "（" + City.Sub + "）" : "") + " " + t.ToString("yyyy-MM-dd") + " " + TimeUtil.WD[(int)t.DayOfWeek] + " " + t.ToString("HH:mm") + " " + TimeUtil.Offset(OffsetOf(utc));
        }
    }
}

// 会议时间规划窗口 + 提醒弹窗
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WorldClock
{
    public class PlannerWindow : Window
    {
        readonly MainWindow M;
        readonly AppSettings S;
        readonly Grid outer = new Grid();
        public Grid Root;
        int dayOffset;
        TextBlock info;
        const double CellW = 31, CellH = 34, LabelW = 128;

        public PlannerWindow(MainWindow m, AppSettings s)
        {
            M = m; S = s;
            Title = "会议时间规划";
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            Icon = AppIcon.Wpf(64);
            Content = outer;
            KeyDown += (o, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key == Key.Left) { dayOffset--; Build(); } else if (e.Key == Key.Right) { dayOffset++; Build(); } };
            Build();
        }

        public void Build()
        {
            FontFamily = T.UI; Foreground = T.Fg(1);
            outer.Children.Clear();
            Root = new Grid { Margin = new Thickness(16) };
            outer.Children.Add(Root);
            var layer = T.Shell(Root);
            var main = new StackPanel { Margin = new Thickness(22, 16, 22, 18) };
            layer.Children.Add(main);

            var btz = M.BaseTz();
            var baseNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, btz);
            var day = baseNow.Date.AddDays(dayOffset);

            // 标题栏
            var header = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ht = new StackPanel();
            var title = T.Text("会议时间规划", 19, 1); title.FontWeight = FontWeights.SemiBold;
            ht.Children.Add(title);
            ht.Children.Add(T.Text("按「" + M.BaseName() + "」的时间排列　·　点击任意一列，复制该时段各城市的时间", 12, 0.55));
            header.Children.Add(ht);
            var nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            nav.Children.Add(new IconButton("", "前一天（←）", () => { dayOffset--; Build(); }, 30, 11));
            var dt = T.Text((dayOffset == 0 ? "今天 " : dayOffset == 1 ? "明天 " : dayOffset == -1 ? "昨天 " : "") + TimeUtil.DateText(day), 14, 0.95);
            dt.MinWidth = 130; dt.TextAlignment = TextAlignment.Center;
            nav.Children.Add(dt);
            nav.Children.Add(new IconButton("", "后一天（→）", () => { dayOffset++; Build(); }, 30, 11));
            if (dayOffset != 0) { var today = T.Pill("今天", () => { dayOffset = 0; Build(); }, "normal"); today.Padding = new Thickness(10, 3, 10, 3); today.Margin = new Thickness(6, 0, 0, 0); nav.Children.Add(today); }
            Grid.SetColumn(nav, 1);
            header.Children.Add(nav);
            var close = new IconButton("", "关闭", Close, 32, 11);
            Grid.SetColumn(close, 2);
            header.Children.Add(close);
            header.MouseLeftButtonDown += (o, e) => { try { DragMove(); } catch { } };
            main.Children.Add(header);

            if (S.Cities.Count == 0) { main.Children.Add(T.Text("请先在设置里添加城市", 13, 0.6)); return; }

            // 计算每个格子
            var cities = S.Cities.Where(c => TimeUtil.Find(c.TzId) != null).ToList();
            int n = cities.Count;
            var utcCols = new DateTime[24];
            var scores = new int[n, 24];
            var locals = new DateTime[n, 24];
            var labels = new string[n, 24];
            for (int h = 0; h < 24; h++)
            {
                var local = day.AddHours(h);
                DateTime u;
                try { u = btz.IsInvalidTime(local) ? TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), btz) : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), btz); }
                catch { u = local.ToUniversalTime(); }
                utcCols[h] = u;
                for (int i = 0; i < n; i++)
                {
                    var lt = TimeZoneInfo.ConvertTimeFromUtc(u, TimeUtil.Find(cities[i].TzId));
                    locals[i, h] = lt;
                    string lb;
                    scores[i, h] = TimeUtil.Score(lt, S.ShowHolidays ? cities[i].Country : "", out lb);
                    labels[i, h] = lb;
                }
            }
            var colMin = new int[24];
            for (int h = 0; h < 24; h++) { int mn = 9; for (int i = 0; i < n; i++) mn = Math.Min(mn, scores[i, h]); colMin[h] = mn; }
            int best = colMin.Max();

            // 网格
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelW) });
            for (int h = 0; h < 24; h++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellW) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            for (int i = 0; i < n; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellH + 4) });

            // 悬停列高亮
            var hover = new Border { CornerRadius = new CornerRadius(8), Background = T.Fg(0.08), Visibility = Visibility.Collapsed, IsHitTestVisible = false, Margin = new Thickness(-1, 0, -1, 0) };
            Grid.SetRowSpan(hover, n + 1);
            grid.Children.Add(hover);

            // 推荐时段外框
            if (best > 0)
            {
                for (int h = 0; h < 24; h++)
                {
                    if (colMin[h] != best || (h > 0 && colMin[h - 1] == best)) continue;
                    int e = h; while (e + 1 < 24 && colMin[e + 1] == best) e++;
                    var hl = new Border { CornerRadius = new CornerRadius(9), BorderBrush = T.AccentGrad(), BorderThickness = new Thickness(2), Background = T.B(T.Cur.Accent, 0.1), IsHitTestVisible = false, Margin = new Thickness(-1, 0, -1, 0) };
                    Grid.SetColumn(hl, h + 1); Grid.SetColumnSpan(hl, e - h + 1); Grid.SetRowSpan(hl, n + 1);
                    grid.Children.Add(hl);
                }
            }

            // 表头：基准城市的小时
            var corner = T.Text(M.BaseName(), 11.5, 0.5); corner.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(corner);
            for (int h = 0; h < 24; h++)
            {
                var tb = new TextBlock { Text = h.ToString(), FontFamily = T.Num, FontSize = 12, Foreground = colMin[h] == best && best > 0 ? T.AccentBrush : T.Fg(0.55), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                if (colMin[h] == best && best > 0) tb.FontWeight = FontWeights.Bold;
                Grid.SetColumn(tb, h + 1);
                grid.Children.Add(tb);
            }

            var cWork = T.C("#34D399"); var cOk = T.C("#FBBF24");
            for (int i = 0; i < n; i++)
            {
                var c = cities[i];
                var lab = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                var nm = T.Text(c.Name, 13.5, 0.95); nm.FontWeight = FontWeights.SemiBold;
                lab.Children.Add(nm);
                lab.Children.Add(T.Text(TimeUtil.Offset(TimeUtil.Find(c.TzId).GetUtcOffset(utcCols[12])) + (c.IsBase ? " · 基准" : ""), 11, 0.5));
                Grid.SetRow(lab, i + 1);
                grid.Children.Add(lab);
                for (int h = 0; h < 24; h++)
                {
                    int sc = scores[i, h];
                    var lt = locals[i, h];
                    bool newDay = lt.Hour == 0;
                    var bg = sc == 2 ? T.B(cWork, 0.55) : sc == 1 ? T.B(cOk, 0.42) : T.Fg(0.06);
                    var tx = new TextBlock
                    {
                        Text = newDay ? lt.Day + "日" : lt.Hour.ToString(), FontFamily = newDay ? T.UI : T.Num, FontSize = newDay ? 10 : 12.5,
                        Foreground = sc == 0 ? T.Fg(0.5) : (T.Lum(T.Cur.Text) > 0.5 ? Brushes.White : T.Fg(1)),
                        FontWeight = newDay ? FontWeights.Bold : FontWeights.Normal,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    };
                    var cell = new Border
                    {
                        Height = CellH, Margin = new Thickness(1.5, 2, 1.5, 2), CornerRadius = new CornerRadius(6), Background = bg, Child = tx, Cursor = Cursors.Hand,
                        ToolTip = c.Name + "  " + TimeUtil.DateText(lt) + " " + lt.ToString("HH:mm") + "  ·  " + labels[i, h]
                    };
                    int hh = h;
                    cell.MouseEnter += delegate { Grid.SetColumn(hover, hh + 1); hover.Visibility = Visibility.Visible; };
                    cell.MouseLeave += delegate { hover.Visibility = Visibility.Collapsed; };
                    cell.MouseLeftButtonUp += delegate { CopyColumn(cities, utcCols[hh]); };
                    Grid.SetRow(cell, i + 1); Grid.SetColumn(cell, h + 1);
                    grid.Children.Add(cell);
                }
            }
            main.Children.Add(grid);

            // 图例
            var legend = new WrapPanel { Margin = new Thickness(LabelW, 12, 0, 0) };
            Action<Brush, Brush, string> lg = (fill, stroke, text) =>
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
                sp.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(4), Background = fill, BorderBrush = stroke, BorderThickness = new Thickness(stroke == null ? 0 : 2), Margin = new Thickness(0, 0, 6, 0) });
                sp.Children.Add(T.Text(text, 12, 0.65));
                legend.Children.Add(sp);
            };
            lg(T.B(cWork, 0.55), null, "工作时间");
            lg(T.B(cOk, 0.42), null, "早晚 / 周末，还醒着");
            lg(T.Fg(0.06), null, "休息");
            lg(T.B(T.Cur.Accent, 0.1), T.AccentGrad(), "推荐时段");
            main.Children.Add(legend);

            // 结论
            var sum = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            string head = best == 2 ? "所有城市都在工作时间的时段：" : best == 1 ? "没有共同的工作时间，以下时段大家都还醒着（一方在早晚）：" : "这一天找不到大家都方便的时段，试试前后几天。";
            var ht2 = T.Text(head, 13, 0.9); ht2.FontWeight = FontWeights.SemiBold;
            sum.Children.Add(ht2);
            if (best > 0)
            {
                for (int h = 0; h < 24; h++)
                {
                    if (colMin[h] != best || (h > 0 && colMin[h - 1] == best)) continue;
                    int e = h; while (e + 1 < 24 && colMin[e + 1] == best) e++;
                    var parts = new List<string>();
                    if (S.BaseCity == null) parts.Add("本地 " + h.ToString("00") + ":00–" + ((e + 1) % 24).ToString("00") + ":00");
                    for (int i = 0; i < n; i++)
                    {
                        var a = locals[i, h]; var b = locals[i, e].AddHours(1);
                        parts.Add(cities[i].Name + " " + a.ToString("HH:mm") + "–" + b.ToString("HH:mm") + (a.Date != day ? "（" + a.Day + "日）" : ""));
                    }
                    var line = T.Text("•  " + string.Join("　＝　", parts), 12.5, 0.75);
                    line.TextWrapping = TextWrapping.Wrap; line.Margin = new Thickness(0, 5, 0, 0); line.MaxWidth = LabelW + 24 * CellW;
                    sum.Children.Add(line);
                }
            }
            main.Children.Add(sum);
            info = T.Text("", 12, 0.6);
            info.Foreground = T.AccentBrush;
            info.Margin = new Thickness(0, 10, 0, 0);
            main.Children.Add(info);
        }

        void CopyColumn(List<CityInfo> cities, DateTime utc)
        {
            var sb = new StringBuilder("会议时间：");
            var parts = new List<string>();
            foreach (var c in cities)
            {
                var lt = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeUtil.Find(c.TzId));
                parts.Add(c.Name + " " + lt.Month + "月" + lt.Day + "日 " + TimeUtil.WD[(int)lt.DayOfWeek] + " " + lt.ToString("HH:mm"));
            }
            sb.Append(string.Join(" / ", parts));
            try { Clipboard.SetText(sb.ToString()); info.Text = "已复制：" + sb; } catch { info.Text = "复制失败，请重试"; }
        }
    }

    // 右下角提醒弹窗
    public class NotifyWindow : Window
    {
        static readonly List<NotifyWindow> open = new List<NotifyWindow>();
        readonly DispatcherTimer life;

        public static void Popup(string title, string body, Action snooze)
        {
            var w = new NotifyWindow(title, body, snooze);
            w.Show();
            try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
        }

        NotifyWindow(string title, string body, Action snooze)
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false; Topmost = true; ShowActivated = false; SizeToContent = SizeToContent.Height; Width = 380;
            FontFamily = T.UI; Foreground = T.Fg(1); UseLayoutRounding = true;
            var root = new Grid { Margin = new Thickness(16) };
            var layer = T.Shell(root);
            var g = new Grid { Margin = new Thickness(16, 14, 14, 14) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ic = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Background = T.AccentGrad(), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0) };
            var gl = T.Glyph("", 16, T.OnAccent); gl.HorizontalAlignment = HorizontalAlignment.Center;
            ic.Child = gl;
            g.Children.Add(ic);
            var sp = new StackPanel();
            var tt = T.Text(title, 14.5, 1); tt.FontWeight = FontWeights.SemiBold; tt.TextWrapping = TextWrapping.Wrap;
            var bb = T.Text(body, 12.5, 0.75); bb.TextWrapping = TextWrapping.Wrap; bb.Margin = new Thickness(0, 4, 0, 12);
            sp.Children.Add(tt); sp.Children.Add(bb);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            if (snooze != null)
            {
                var sz = T.Pill("10 分钟后再提醒", () => { snooze(); Dismiss(); }, "normal");
                sz.Margin = new Thickness(0, 0, 8, 0);
                btns.Children.Add(sz);
            }
            btns.Children.Add(T.Pill("知道了", Dismiss, "primary"));
            sp.Children.Add(btns);
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);
            layer.Children.Add(g);
            Content = root;

            Opacity = 0;
            Loaded += delegate
            {
                var wa = SystemParameters.WorkArea;
                double y = wa.Bottom - 4;
                foreach (var o in open) y -= o.ActualHeight;
                Left = wa.Right - ActualWidth - 4;
                Top = y - ActualHeight;
                open.Add(this);
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
            };
            Closed += delegate { open.Remove(this); };
            life = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            life.Tick += delegate { Dismiss(); };
            life.Start();
        }

        void Dismiss()
        {
            life.Stop();
            var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
            a.Completed += delegate { Close(); };
            BeginAnimation(OpacityProperty, a);
        }
    }
}

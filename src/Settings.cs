// 设置窗口：城市 / 外观（主题编辑器）/ 提醒 / 常规 / 关于
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using IO = System.IO;

namespace WorldClock
{
    public class SettingsWindow : Window
    {
        readonly MainWindow M;
        readonly AppSettings S;
        readonly Grid outer = new Grid();
        public Grid Root;
        public ScrollViewer Sv;
        StackPanel body;
        int tab;
        readonly DispatcherTimer restyleTimer;
        static readonly string[] Tabs = { "城市", "外观", "提醒", "常规", "关于" };
        static readonly string[] TabGlyphs = { "", "", "", "", "" };

        // 城市页
        StackPanel cityList, results;
        WrapPanel chips;
        TextBox search;
        // 提醒页草稿
        Reminder draft;

        public SettingsWindow(MainWindow m, AppSettings s, int startTab)
        {
            M = m; S = s; tab = startTab;
            Title = "世界时钟 · 设置";
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            Width = 560; Height = Math.Min(800, SystemParameters.WorkArea.Height - 20);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            Icon = AppIcon.Wpf(64);
            Content = outer;
            KeyDown += (o, e) => { if (e.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox)) Close(); };
            restyleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            restyleTimer.Tick += delegate
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed || ColorPicker.IsOpen || FontPicker.IsOpen) return;
                restyleTimer.Stop();
                BuildAll();
            };
            BuildAll();
        }

        public void ScheduleRestyle() { restyleTimer.Stop(); restyleTimer.Start(); }
        public void ShowTab(int i) { tab = i; BuildAll(); if (Sv != null) Sv.ScrollToTop(); }

        // ───────── 框架 ─────────
        public void BuildAll()
        {
            double offset = Sv != null ? Sv.VerticalOffset : 0;
            FontFamily = T.UI; Foreground = T.Fg(1);
            outer.Children.Clear();
            Root = new Grid { Margin = new Thickness(16) };
            outer.Children.Add(Root);
            var layer = T.Shell(Root);
            var g = new Grid { Margin = new Thickness(22, 16, 12, 14) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layer.Children.Add(g);

            var header = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 0, 4, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ht = new StackPanel { Orientation = Orientation.Horizontal };
            ht.Children.Add(new Image { Source = AppIcon.Wpf(64), Width = 26, Height = 26, Margin = new Thickness(0, 0, 10, 0) });
            var htt = T.Text("设置", 19, 1); htt.FontWeight = FontWeights.SemiBold;
            ht.Children.Add(htt);
            header.Children.Add(ht);
            var close = new IconButton("", "关闭", Close, 32, 11);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += (o, e) => { try { DragMove(); } catch { } };
            g.Children.Add(header);

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            for (int i = 0; i < Tabs.Length; i++)
            {
                int idx = i;
                bool sel = i == tab;
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(T.Glyph(TabGlyphs[i], 12, sel ? T.OnAccent : T.Fg(0.7)));
                var tx = T.Text(Tabs[i], 13, 0.85); tx.Margin = new Thickness(7, 0, 0, 0);
                if (sel) tx.Foreground = T.OnAccent;
                sp.Children.Add(tx);
                var b = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(13, 6, 14, 6), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, Child = sp, Background = sel ? T.AccentGrad() : Brushes.Transparent };
                if (!sel)
                {
                    b.MouseEnter += delegate { b.Background = T.Fg(0.08); };
                    b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
                }
                b.MouseLeftButtonUp += delegate { ShowTab(idx); };
                tabs.Children.Add(b);
            }
            Grid.SetRow(tabs, 1);
            g.Children.Add(tabs);

            Sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 10, 0) };
            Sv.Resources.MergedDictionaries.Add(T.ScrollStyle());
            body = new StackPanel();
            Sv.Content = body;
            Grid.SetRow(Sv, 2);
            g.Children.Add(Sv);

            switch (tab)
            {
                case 0: CityTab(); break;
                case 1: ThemeTab(); break;
                case 2: ReminderTab(); break;
                case 3: GeneralTab(); break;
                default: AboutTab(); break;
            }
            body.Children.Add(new Border { Height = 10 });
            var sv = Sv;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => sv.ScrollToVerticalOffset(offset)));
        }

        // ───────── 通用小部件 ─────────
        static TextBlock Section(string t)
        {
            var tb = T.Text(t, 12.5, 0.5);
            tb.Margin = new Thickness(4, 18, 0, 8);
            return tb;
        }

        static Border Card(UIElement child, Thickness pad)
        {
            return new Border { CornerRadius = new CornerRadius(14), Background = T.Fg(0.045), BorderBrush = T.Fg(0.08), BorderThickness = new Thickness(1), Padding = pad, Child = child };
        }
        static Border Card(UIElement child) { return Card(child, new Thickness(16, 2, 16, 2)); }

        static StackPanel Rows(params UIElement[] rows)
        {
            var sp = new StackPanel();
            int n = 0;
            foreach (var r in rows)
            {
                if (r == null) continue;
                sp.Children.Add(new Border { Child = r, BorderBrush = T.Fg(0.07), BorderThickness = new Thickness(0, n == 0 ? 0 : 1, 0, 0) });
                n++;
            }
            return sp;
        }

        static UIElement Row(string title, string desc, UIElement ctrl)
        {
            var g = new Grid { Margin = new Thickness(0, 11, 0, 11) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            l.Children.Add(T.Text(title, 13.5, 0.95));
            if (!string.IsNullOrEmpty(desc))
            {
                var d = T.Text(desc, 11.5, 0.5); d.TextWrapping = TextWrapping.Wrap; d.Margin = new Thickness(0, 2, 0, 0);
                l.Children.Add(d);
            }
            g.Children.Add(l);
            var fe = ctrl as FrameworkElement;
            if (fe != null) fe.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ctrl, 1);
            g.Children.Add(ctrl);
            return g;
        }

        // 标题在上、控件在下（控件较宽时）
        static UIElement RowWide(string title, string desc, UIElement ctrl)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 11, 0, 11) };
            sp.Children.Add(T.Text(title, 13.5, 0.95));
            if (!string.IsNullOrEmpty(desc)) { var d = T.Text(desc, 11.5, 0.5); d.TextWrapping = TextWrapping.Wrap; d.Margin = new Thickness(0, 2, 0, 0); sp.Children.Add(d); }
            var fe = ctrl as FrameworkElement;
            if (fe != null) fe.Margin = new Thickness(0, 9, 0, 0);
            sp.Children.Add(ctrl);
            return sp;
        }

        static UIElement SliderCtl(double min, double max, double step, double val, Func<double, string> fmt, Action<double> onChange)
        {
            var g = new Grid { Width = 210 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            var sl = new FlatSlider(min, max, step, val, false);
            var lb = T.Text(fmt(val), 12.5, 0.8); lb.HorizontalAlignment = HorizontalAlignment.Right;
            sl.Changed = v => { lb.Text = fmt(v); onChange(v); };
            Grid.SetColumn(lb, 1);
            g.Children.Add(sl); g.Children.Add(lb);
            return g;
        }

        static string Pct(double v) { return Math.Round(v * 100) + "%"; }

        static UIElement Tip(string text)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ic = T.Glyph("", 13, T.AccentBrush); ic.VerticalAlignment = VerticalAlignment.Top; ic.Margin = new Thickness(0, 2, 10, 0);
            var tx = T.Text(text, 12, 0.7); tx.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(tx, 1);
            g.Children.Add(ic); g.Children.Add(tx);
            var b = Card(g, new Thickness(14, 10, 14, 10));
            b.Background = T.B(T.Cur.Accent, 0.08);
            b.BorderBrush = T.B(T.Cur.Accent, 0.2);
            b.Margin = new Thickness(0, 12, 0, 0);
            return b;
        }

        static Border PickerButton(string text, Action click)
        {
            var g = new StackPanel { Orientation = Orientation.Horizontal };
            g.Children.Add(T.Text(text, 13, 0.95));
            var chev = T.Glyph("", 10, T.Fg(0.6)); chev.Margin = new Thickness(10, 0, 0, 0);
            g.Children.Add(chev);
            var b = new Border { CornerRadius = new CornerRadius(8), Background = T.Fg(0.07), BorderBrush = T.Fg(0.12), BorderThickness = new Thickness(1), Padding = new Thickness(11, 5, 9, 5), Cursor = Cursors.Hand, Child = g, HorizontalAlignment = HorizontalAlignment.Left };
            b.MouseLeftButtonDown += (o, e) => { e.Handled = true; };
            b.MouseLeftButtonUp += (o, e) => { e.Handled = true; click(); };
            return b;
        }

        // ═════════════════════ 城市 ═════════════════════
        void CityTab()
        {
            body.Children.Add(Section("时差基准"));
            var b = S.BaseCity;
            string cur = b != null ? b.Name : "系统时区（" + TimeUtil.Offset(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)) + "）";
            Border picker = null;
            picker = PickerButton(cur, () =>
            {
                var items = new List<PopMenu.Item>();
                items.Add(PopMenu.Item.Check("", "系统时区：" + TimeZoneInfo.Local.DisplayName, b == null, () => { foreach (var x in S.Cities) x.IsBase = false; Changed(true); }));
                foreach (var c in S.Cities)
                {
                    var cc = c;
                    items.Add(PopMenu.Item.Check("", c.Name + (c.Sub.Length > 0 ? "（" + c.Sub + "）" : ""), c.IsBase, () => { foreach (var x in S.Cities) x.IsBase = false; cc.IsBase = true; Changed(true); }));
                }
                PopMenu.Show(picker, items, PlacementMode.Bottom);
            });
            body.Children.Add(Card(Rows(Row("基准", "卡片上的「快 / 慢 X 小时」「明天 / 昨天」都以它为准。默认是北京，不受电脑时区设置影响。", picker))));

            body.Children.Add(Section("我的城市（右键时钟卡片也能排序、删除、设为基准）"));
            cityList = new StackPanel();
            body.Children.Add(Card(cityList, new Thickness(10, 6, 10, 6)));
            BuildCityList();

            body.Children.Add(Section("添加城市"));
            var add = new StackPanel();
            add.Children.Add(SearchBox());
            chips = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            results = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            add.Children.Add(chips); add.Children.Add(results);
            body.Children.Add(Card(add, new Thickness(14)));
            RenderResults();
        }

        void Changed(bool rebuildTab)
        {
            M.ApplyAll(true);
            if (rebuildTab) BuildAll();
        }

        void BuildCityList()
        {
            cityList.Children.Clear();
            if (S.Cities.Count == 0)
            {
                var e = T.Text("还没有城市，在下方搜索添加吧", 12.5, 0.55);
                e.Margin = new Thickness(8, 12, 8, 12);
                cityList.Children.Add(e);
                return;
            }
            var utc = DateTime.UtcNow;
            for (int i = 0; i < S.Cities.Count; i++)
            {
                var c = S.Cities[i];
                int idx = i;
                var tz = TimeUtil.Find(c.TzId);
                var row = new Grid { Margin = new Thickness(4, 7, 0, 7) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                Color a, bb;
                double hf = 12;
                if (tz != null) { var t = TimeZoneInfo.ConvertTimeFromUtc(utc, tz); hf = t.Hour + t.Minute / 60.0; }
                Sky.Get(hf, out a, out bb);
                var num = new TextBlock { Text = (i + 1).ToString(), FontSize = 13, Foreground = Brushes.White, FontFamily = T.Num, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var badge = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(10), Background = new LinearGradientBrush(a, bb, 45), Child = num, Margin = new Thickness(0, 0, 12, 0), BorderBrush = T.W(0.15), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(badge);

                var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
                var name = T.Input(c.Name, 14);
                name.ToolTip = "城市名称（点击可编辑）";
                name.MinWidth = 40;
                name.TextChanged += delegate { c.Name = name.Text; M.ApplyAll(true); };
                nameRow.Children.Add(name);
                if (c.IsBase)
                {
                    var bt = new Border { CornerRadius = new CornerRadius(6), Background = T.B(T.Cur.Accent, 0.2), Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = T.Text("基准", 10.5, 0.95) };
                    nameRow.Children.Add(bt);
                }
                var subRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                var sub = T.Input(c.Sub, 11.5);
                sub.Foreground = T.Fg(0.6); sub.MinWidth = 24; sub.ToolTip = "副标题（如国家），点击可编辑";
                sub.TextChanged += delegate { c.Sub = sub.Text; M.ApplyAll(true); };
                var cc = T.Input(c.Country, 11.5);
                cc.Foreground = T.Fg(0.6); cc.MinWidth = 18; cc.CharacterCasing = CharacterCasing.Upper; cc.MaxLength = 3;
                cc.ToolTip = "国家代码，用于节假日（如 CN、CL、US）";
                cc.TextChanged += delegate { c.Country = cc.Text.Trim(); M.ApplyAll(true); };
                string tzText = tz == null ? "时区无效：" + c.TzId : TimeUtil.Offset(tz.GetUtcOffset(utc)) + (tz.SupportsDaylightSavingTime ? " · 有夏令时" : "");
                subRow.Children.Add(sub);
                subRow.Children.Add(T.Text("  ·  节假日 ", 11.5, 0.42));
                subRow.Children.Add(cc);
                subRow.Children.Add(T.Text("  ·  " + tzText, 11.5, 0.42));
                mid.Children.Add(nameRow); mid.Children.Add(subRow);
                Grid.SetColumn(mid, 1);
                row.Children.Add(mid);

                var ops = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var star = new IconButton(c.IsBase ? "" : "", c.IsBase ? "当前基准" : "设为时差基准", () => { foreach (var x in S.Cities) x.IsBase = false; c.IsBase = true; Changed(true); }, 28, 12);
                if (c.IsBase) star.Active = true;
                var up = new IconButton("", "上移", () => { if (idx > 0) { S.Cities[idx] = S.Cities[idx - 1]; S.Cities[idx - 1] = c; Changed(true); } }, 28, 11);
                var down = new IconButton("", "下移", () => { if (idx < S.Cities.Count - 1) { S.Cities[idx] = S.Cities[idx + 1]; S.Cities[idx + 1] = c; Changed(true); } }, 28, 11);
                var del = new IconButton("", "删除", () => { S.Cities.RemoveAt(idx); Changed(true); }, 28, 11);
                if (idx == 0) up.Opacity = 0.3;
                if (idx == S.Cities.Count - 1) down.Opacity = 0.3;
                ops.Children.Add(star); ops.Children.Add(up); ops.Children.Add(down); ops.Children.Add(del);
                Grid.SetColumn(ops, 2);
                row.Children.Add(ops);

                cityList.Children.Add(new Border { Child = row, BorderBrush = T.Fg(0.07), BorderThickness = new Thickness(0, i == 0 ? 0 : 1, 0, 0) });
            }
        }

        UIElement SearchBox()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ic = T.Glyph("", 13, T.Fg(0.55)); ic.Margin = new Thickness(0, 0, 10, 0);
            search = T.Input("", 13.5);
            var ph = T.Text("搜索城市、国家或时区，如：纽约、Tokyo、UTC+9", 13, 0.38);
            ph.IsHitTestVisible = false;
            ph.Margin = new Thickness(2, 0, 0, 0);
            search.TextChanged += delegate { ph.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; RenderResults(); };
            Grid.SetColumn(search, 1); Grid.SetColumn(ph, 1);
            g.Children.Add(ic); g.Children.Add(search); g.Children.Add(ph);
            var box = T.Box(g);
            box.CornerRadius = new CornerRadius(10);
            box.Padding = new Thickness(12, 8, 12, 8);
            return box;
        }

        bool Has(CityInfo c) { return S.Cities.Any(x => x.Name == c.Name && x.TzId == c.TzId); }

        void AddCity(CityInfo c)
        {
            S.Cities.Add(c.Clone());
            Changed(true);
        }

        void RenderResults()
        {
            if (chips == null || results == null) return;
            chips.Children.Clear();
            results.Children.Clear();
            string q = search == null ? "" : search.Text.Trim();
            if (q.Length == 0)
            {
                chips.Visibility = Visibility.Visible;
                foreach (var p in Presets.All)
                {
                    if (Has(p)) continue;
                    var pc = p;
                    var tb = T.Text("+ " + p.Name, 12.5, 0.9);
                    var chip = new Border { CornerRadius = new CornerRadius(14), Background = T.Fg(0.07), BorderBrush = T.Fg(0.08), BorderThickness = new Thickness(1), Padding = new Thickness(11, 4, 11, 5), Margin = new Thickness(0, 0, 6, 6), Child = tb, Cursor = Cursors.Hand, ToolTip = p.Sub + " · " + p.TzId };
                    chip.MouseEnter += delegate { chip.Background = T.B(T.Cur.Accent, 0.25); };
                    chip.MouseLeave += delegate { chip.Background = T.Fg(0.07); };
                    chip.MouseLeftButtonUp += delegate { AddCity(pc); };
                    chips.Children.Add(chip);
                }
                return;
            }
            chips.Visibility = Visibility.Collapsed;

            var list = new List<CityInfo>();
            var utc = DateTime.UtcNow;
            var m = Regex.Match(q, @"^(?:UTC|GMT)?\s*([+\-−])\s*(\d{1,2})(?::?(\d{2}))?$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                int sign = m.Groups[1].Value == "+" ? 1 : -1;
                var want = TimeSpan.FromMinutes(sign * (int.Parse(m.Groups[2].Value) * 60 + (m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0)));
                foreach (var z in TimeUtil.Zones)
                    if (z.GetUtcOffset(utc) == want) list.Add(new CityInfo(TimeUtil.ZoneName(z), "", z.Id));
            }
            else
            {
                foreach (var p in Presets.All)
                    if (Contains(p.Name, q) || Contains(p.Sub, q) || Contains(p.Alias, q) || Contains(p.TzId, q)) list.Add(p);
                foreach (var z in TimeUtil.Zones)
                    if (Contains(z.DisplayName, q) || Contains(z.Id, q) || Contains(z.StandardName, q))
                        list.Add(new CityInfo(TimeUtil.ZoneName(z), "", z.Id));
            }

            if (list.Count == 0)
            {
                var e = T.Text("没有找到匹配的城市或时区", 12.5, 0.5);
                e.Margin = new Thickness(4, 8, 0, 2);
                results.Children.Add(e);
                return;
            }
            foreach (var r in list.Take(30))
            {
                var rc = r;
                var tz = TimeUtil.Find(r.TzId);
                if (tz == null) continue;
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var l = new StackPanel();
                l.Children.Add(T.Text(r.Name, 13.5, 0.95));
                l.Children.Add(T.Text((r.Sub.Length > 0 ? r.Sub + " · " : "") + tz.DisplayName, 11, 0.45));
                var t = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
                var rt = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                rt.Children.Add(new TextBlock { Text = t.ToString("HH:mm"), FontFamily = T.Num, FontSize = 16, Foreground = T.Fg(1), HorizontalAlignment = HorizontalAlignment.Right });
                var ot = T.Text(TimeUtil.Offset(tz.GetUtcOffset(utc)), 10.5, 0.5); ot.HorizontalAlignment = HorizontalAlignment.Right;
                rt.Children.Add(ot);
                Grid.SetColumn(rt, 1);
                var plusTb = T.Glyph(Has(r) ? "" : "", 11, T.OnAccent);
                plusTb.HorizontalAlignment = HorizontalAlignment.Center;
                var plus = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = T.AccentGrad(), VerticalAlignment = VerticalAlignment.Center, Child = plusTb };
                Grid.SetColumn(plus, 2);
                g.Children.Add(l); g.Children.Add(rt); g.Children.Add(plus);
                var row = new Border { Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, Child = g, Cursor = Cursors.Hand };
                row.MouseEnter += delegate { row.Background = T.Fg(0.07); };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                row.MouseLeftButtonUp += delegate { AddCity(rc); };
                results.Children.Add(row);
            }
        }

        static bool Contains(string s, string q) { return !string.IsNullOrEmpty(s) && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0; }

        // ═════════════════════ 外观 / 主题 ═════════════════════
        void TC() { M.ThemeChanged(); }

        void ThemeTab()
        {
            var th = T.Cur;
            body.Children.Add(Section("主题（点击切换，切换后仍可在下方继续自定义每一项）"));
            var gallery = new WrapPanel();
            foreach (var t in Theme.BuiltIns) gallery.Children.Add(ThemeTile(t, false));
            foreach (var t in Theme.UserThemes()) gallery.Children.Add(ThemeTile(t, true));
            body.Children.Add(gallery);

            body.Children.Add(Section("颜色"));
            body.Children.Add(Card(Rows(
                Row("文字颜色", null, new ColorField(th.Text, c => { th.Text = c; TC(); })),
                Row("强调色", "按钮、开关、滑块、图标", new ColorField(th.Accent, c => { th.Accent = c; TC(); })),
                Row("强调色 2", "与强调色组成渐变", new ColorField(th.Accent2, c => { th.Accent2 = c; TC(); })),
                Row("高亮色", "时间换算时的时间颜色", new ColorField(th.Highlight, c => { th.Highlight = c; TC(); })),
                Row("面板颜色（上）", null, new ColorField(th.Panel, c => { th.Panel = c; TC(); })),
                Row("面板颜色（下）", null, new ColorField(th.Panel2, c => { th.Panel2 = c; TC(); })),
                Row("面板不透明度", null, SliderCtl(0.15, 1, 0.01, th.PanelOpacity, Pct, v => { th.PanelOpacity = v; TC(); })),
                Row("边框亮度", null, SliderCtl(0, 0.6, 0.01, th.BorderOpacity, Pct, v => { th.BorderOpacity = v; TC(); }))
            )));

            body.Children.Add(Section("城市卡片"));
            body.Children.Add(Card(Rows(
                RowWide("卡片样式", "天空：随当地时间变成日出 / 白天 / 黄昏 / 夜晚色　柔和：淡化的天空色　玻璃 / 纯色：使用下方卡片颜色　渐变：强调色渐变　描边：强调色边框",
                    Seg.Make(Theme.CardStyleNames, Theme.CardStyles, th.CardStyle, v => { th.CardStyle = v; TC(); })),
                Row("卡片颜色", "玻璃 / 描边 / 纯色样式使用", new ColorField(th.Card, c => { th.Card = c; TC(); })),
                Row("卡片不透明度", null, SliderCtl(0, 1, 0.01, th.CardOpacity, Pct, v => { th.CardOpacity = v; TC(); })),
                Row("天空色强度", "柔和样式下天空色的浓淡", SliderCtl(0.05, 1, 0.01, th.SkyStrength, Pct, v => { th.SkyStrength = v; TC(); })),
                Row("卡片圆角", null, SliderCtl(0, 30, 1, th.CardRadius, v => v + " px", v => { th.CardRadius = v; TC(); })),
                Row("装饰光晕", "卡片右上角的柔光", new ToggleSwitch(th.Glow, v => { th.Glow = v; TC(); }))
            )));

            body.Children.Add(Section("面板"));
            string imgName = string.IsNullOrEmpty(th.BgImage) ? "未设置" : IO.Path.GetFileName(th.BgImage);
            var imgBtns = new StackPanel { Orientation = Orientation.Horizontal };
            imgBtns.Children.Add(T.Pill("选择图片…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*", Title = "选择背景图片" };
                if (dlg.ShowDialog(this) == true) { th.BgImage = dlg.FileName; TC(); BuildAll(); }
            }, "normal"));
            if (!string.IsNullOrEmpty(th.BgImage))
            {
                var clr = T.Pill("清除", () => { th.BgImage = ""; TC(); BuildAll(); }, "normal");
                clr.Margin = new Thickness(6, 0, 0, 0);
                imgBtns.Children.Add(clr);
            }
            body.Children.Add(Card(Rows(
                Row("面板圆角", null, SliderCtl(0, 36, 1, th.Radius, v => v + " px", v => { th.Radius = v; TC(); })),
                Row("阴影", null, new ToggleSwitch(th.Shadow, v => { th.Shadow = v; TC(); })),
                Row("背景图片", imgName, imgBtns),
                Row("图片不透明度", null, SliderCtl(0.05, 1, 0.01, th.BgImageOpacity, Pct, v => { th.BgImageOpacity = v; TC(); }))
            )));

            body.Children.Add(Section("字体"));
            body.Children.Add(Card(Rows(
                Row("界面字体", "城市名、日期等文字", new FontPicker(th.FontUI, f => { th.FontUI = f; TC(); })),
                Row("数字字体", "时间数字", new FontPicker(th.FontNum, f => { th.FontNum = f; TC(); })),
                Row("时间字号", null, SliderCtl(24, 80, 1, th.TimeSize, v => v + " px", v => { th.TimeSize = v; TC(); })),
                RowWide("时间字重", "部分字体没有全部字重，会自动取最接近的", Seg.Make(Theme.WeightNames, Theme.Weights, th.TimeWeight, v => { th.TimeWeight = v; TC(); })),
                Row("城市名字号", null, SliderCtl(11, 28, 1, th.NameSize, v => v + " px", v => { th.NameSize = v; TC(); }))
            )));

            // 主题代码
            body.Children.Add(Section("主题代码（高级）"));
            var code = new TextBox
            {
                Text = th.Serialize(), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = T.Mono, FontSize = 12,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = T.Fg(0.9), CaretBrush = T.Fg(1), SelectionBrush = T.AccentBrush,
                Height = 230, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            code.Resources.MergedDictionaries.Add(T.ScrollStyle());
            var codeBox = T.Box(code);
            codeBox.Padding = new Thickness(10, 8, 6, 8);
            var msg = T.Text("", 12, 0.65);
            msg.TextWrapping = TextWrapping.Wrap;
            msg.Margin = new Thickness(2, 8, 0, 0);
            var nameBox = T.Input(th.Name, 13); nameBox.MinWidth = 120;
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            nameRow.Children.Add(T.Text("主题名称", 12.5, 0.7));
            var nb = T.Box(nameBox); nb.Margin = new Thickness(10, 0, 8, 0);
            nameRow.Children.Add(nb);
            nameRow.Children.Add(T.Pill("保存为我的主题", () =>
            {
                string n = nameBox.Text.Trim();
                if (n.Length == 0) { msg.Text = "请先填写主题名称"; return; }
                if (Theme.IsBuiltIn(n)) { msg.Text = "不能和内置主题同名，换个名字吧"; return; }
                try
                {
                    IO.Directory.CreateDirectory(AppSettings.ThemesDir);
                    th.Name = n;
                    IO.File.WriteAllText(Theme.UserThemePath(n), th.Serialize(), new UTF8Encoding(true));
                    TC(); BuildAll();
                }
                catch (Exception ex) { msg.Text = "保存失败：" + ex.Message; }
            }, "primary"));

            var btns = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Action<string, Action, string> addBtn = (text, act, kind) => { var p = T.Pill(text, act, kind); p.Margin = new Thickness(0, 0, 8, 8); btns.Children.Add(p); };
            addBtn("应用代码", () =>
            {
                int bad;
                var nt = Theme.Parse(code.Text, th, out bad);
                T.Cur = nt; TC();
                msg.Text = bad > 0 ? "已应用，但有 " + bad + " 行无法识别（已忽略）" : "已应用";
            }, "primary");
            addBtn("导入主题文件…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "世界时钟主题|*.theme;*.txt|所有文件|*.*", Title = "导入主题" };
                if (dlg.ShowDialog(this) != true) return;
                try
                {
                    var nt = Theme.Parse(IO.File.ReadAllText(dlg.FileName, Encoding.UTF8));
                    if (nt.Name == "未命名") nt.Name = IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                    if (Theme.IsBuiltIn(nt.Name)) nt.Name += "（导入）";
                    IO.Directory.CreateDirectory(AppSettings.ThemesDir);
                    IO.File.WriteAllText(Theme.UserThemePath(nt.Name), nt.Serialize(), new UTF8Encoding(true));
                    T.Cur = nt; TC(); BuildAll();
                }
                catch (Exception ex) { msg.Text = "导入失败：" + ex.Message; }
            }, "normal");
            addBtn("导出当前主题…", () =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "世界时钟主题|*.theme", FileName = th.Name + ".theme", Title = "导出主题" };
                if (dlg.ShowDialog(this) != true) return;
                try { IO.File.WriteAllText(dlg.FileName, th.Serialize(), new UTF8Encoding(true)); msg.Text = "已导出到 " + dlg.FileName; }
                catch (Exception ex) { msg.Text = "导出失败：" + ex.Message; }
            }, "normal");
            addBtn("复制代码", () => { try { Clipboard.SetText(code.Text); msg.Text = "已复制，可以发给朋友，对方粘贴后点「应用代码」即可"; } catch { } }, "normal");
            addBtn("打开主题文件夹", () => { try { IO.Directory.CreateDirectory(AppSettings.ThemesDir); System.Diagnostics.Process.Start("explorer.exe", "\"" + AppSettings.ThemesDir + "\""); } catch { } }, "normal");

            var cs = new StackPanel();
            var d1 = T.Text("像 Obsidian 的 CSS 片段一样：直接修改下面的代码，点「应用代码」即时生效。所有设置都在这里，也可以导入 / 导出 .theme 文件和别人分享。", 12, 0.6);
            d1.TextWrapping = TextWrapping.Wrap; d1.Margin = new Thickness(0, 0, 0, 10);
            cs.Children.Add(d1); cs.Children.Add(codeBox); cs.Children.Add(btns); cs.Children.Add(nameRow); cs.Children.Add(msg);
            body.Children.Add(Card(cs, new Thickness(16, 14, 16, 14)));
        }

        UIElement ThemeTile(Theme th, bool user)
        {
            bool sel = th.Name == T.Cur.Name;
            var tile = new Border
            {
                Width = 146, Height = 100, CornerRadius = new CornerRadius(Math.Min(th.Radius, 14)), Margin = new Thickness(0, 0, 10, 10), Cursor = Cursors.Hand,
                Background = new LinearGradientBrush(T.A(th.Panel, Math.Max(0.9, th.PanelOpacity)), T.A(th.Panel2, Math.Max(0.9, th.PanelOpacity)), new Point(0, 0), new Point(0.4, 1)),
                BorderBrush = sel ? T.AccentGrad() : T.Fg(0.14), BorderThickness = new Thickness(sel ? 2 : 1), ToolTip = th.Name + (user ? "（我的主题，右键可删除）" : "")
            };
            var g = new Grid { Margin = new Thickness(10) };
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 卡片样本
            string st = th.CardStyle;
            bool onColor = st == "sky" || st == "accent";
            Color fg = onColor ? (st == "accent" && T.Lum(th.Accent) > 0.62 ? T.C("#15171C") : Colors.White) : th.Text;
            Brush cardBg; Brush cardBd = new SolidColorBrush(T.A(th.Text, 0.1));
            Color a, b; Sky.Get(10.5, out a, out b);
            if (st == "sky") cardBg = new LinearGradientBrush(a, b, 45);
            else if (st == "soft") cardBg = new LinearGradientBrush(T.Mix(th.Panel, a, th.SkyStrength), T.Mix(th.Panel2, b, th.SkyStrength), 45);
            else if (st == "accent") cardBg = new LinearGradientBrush(th.Accent, th.Accent2, 45);
            else cardBg = new SolidColorBrush(T.A(th.Card, th.CardOpacity));
            if (st == "outline") cardBd = new LinearGradientBrush(th.Accent, th.Accent2, 45);
            var sample = new Border { CornerRadius = new CornerRadius(Math.Min(th.CardRadius, 10)), Background = cardBg, BorderBrush = cardBd, BorderThickness = new Thickness(st == "outline" ? 1.5 : 1) };
            var sg = new Grid { Margin = new Thickness(10, 0, 10, 0) };
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var bars = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            bars.Children.Add(new Border { Height = 5, Width = 34, CornerRadius = new CornerRadius(2.5), Background = new SolidColorBrush(T.A(fg, 0.85)), HorizontalAlignment = HorizontalAlignment.Left });
            bars.Children.Add(new Border { Height = 4, Width = 22, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(T.A(fg, 0.45)), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0) });
            var time = new TextBlock { Text = "10:24", FontFamily = T.FontOf(th.FontNum, "Segoe UI"), FontWeight = T.Weight(th.TimeWeight), FontSize = 21, Foreground = new SolidColorBrush(fg), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(time, 1);
            sg.Children.Add(bars); sg.Children.Add(time);
            sample.Child = sg;
            g.Children.Add(sample);

            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 8, 0, 0) };
            nameRow.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new LinearGradientBrush(th.Accent, th.Accent2, 45), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
            nameRow.Children.Add(new TextBlock { Text = th.Name, FontSize = 12, FontFamily = T.FontOf(th.FontUI, "Microsoft YaHei UI"), Foreground = new SolidColorBrush(th.Text), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 110 });
            if (sel) { var ck = T.Glyph("", 11, new SolidColorBrush(th.Accent)); ck.Margin = new Thickness(6, 0, 0, 0); nameRow.Children.Add(ck); }
            Grid.SetRow(nameRow, 1);
            g.Children.Add(nameRow);
            tile.Child = g;

            var tt = th;
            tile.MouseLeftButtonUp += delegate { T.Cur = tt.Clone(); TC(); BuildAll(); };
            if (user)
                tile.MouseRightButtonUp += (o, e) =>
                {
                    e.Handled = true;
                    PopMenu.Show(tile, new List<PopMenu.Item> {
                        new PopMenu.Item { Glyph = "", Text = "删除主题「" + tt.Name + "」", Danger = true, Act = () => { try { IO.File.Delete(Theme.UserThemePath(tt.Name)); } catch { } BuildAll(); } }
                    });
                };
            return tile;
        }

        // ═════════════════════ 提醒 ═════════════════════
        void ReminderTab()
        {
            body.Children.Add(Tip("到点后屏幕右下角会弹出提醒并响一声。「工作日」会自动跳过当地的周末和节假日。打开「倒计时」后，主界面会显示还剩多久。"));

            if (S.Cities.Count > 0)
            {
                body.Children.Add(Section("快速添加"));
                var quick = new WrapPanel();
                foreach (var c in S.Cities.OrderBy(x => x.IsBase ? 1 : 0).Take(4))
                {
                    var cc = c;
                    quick.Children.Add(QuickChip(c.Name + "上班 09:00", () => AddReminder(new Reminder { Title = cc.Name + "上班了，可以联系了", TzId = cc.TzId, City = cc.Name, Country = cc.Country, Hour = 9, Minute = 0, Repeat = "workdays", Lead = 0 })));
                    quick.Children.Add(QuickChip(c.Name + "下班前 17:30", () => AddReminder(new Reminder { Title = cc.Name + "快下班了", TzId = cc.TzId, City = cc.Name, Country = cc.Country, Hour = 17, Minute = 30, Repeat = "workdays", Lead = 0 })));
                }
                body.Children.Add(quick);
            }

            body.Children.Add(Section("我的提醒"));
            var list = new StackPanel();
            if (S.Reminders.Count == 0)
            {
                var e = T.Text("还没有提醒。用上面的快速添加，或在下面新建。", 12.5, 0.55);
                e.Margin = new Thickness(8, 12, 8, 12);
                list.Children.Add(e);
            }
            var now = DateTime.UtcNow;
            var btz = M.BaseTz();
            for (int i = 0; i < S.Reminders.Count; i++)
            {
                var r = S.Reminders[i];
                var g = new Grid { Margin = new Thickness(2, 9, 0, 9) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var tg = new ToggleSwitch(r.Enabled, v => { r.Enabled = v; M.ApplyAll(true); });
                tg.Margin = new Thickness(0, 0, 14, 0);
                g.Children.Add(tg);
                var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                mid.Children.Add(T.Text(string.IsNullOrWhiteSpace(r.Title) ? "（无标题）" : r.Title, 13.5, r.Enabled ? 0.95 : 0.5));
                var n = r.NextUtc(now);
                string next = n.HasValue ? "下次：" + TimeUtil.DateText(TimeZoneInfo.ConvertTimeFromUtc(n.Value, btz)) + " " + TimeZoneInfo.ConvertTimeFromUtc(n.Value, btz).ToString("HH:mm") + "（" + M.BaseName() + "）" : "已过期";
                var d = T.Text(r.Describe() + "　" + next, 11.5, 0.5); d.TextWrapping = TextWrapping.Wrap; d.Margin = new Thickness(0, 2, 0, 0);
                mid.Children.Add(d);
                Grid.SetColumn(mid, 1);
                g.Children.Add(mid);
                var ops = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var cd = new IconButton("", r.Countdown ? "主界面显示倒计时：开" : "主界面显示倒计时：关", () => { r.Countdown = !r.Countdown; M.ApplyAll(true); BuildAll(); });
                cd.Active = r.Countdown;
                var del = new IconButton("", "删除", () => { S.Reminders.Remove(r); M.ApplyAll(true); BuildAll(); });
                ops.Children.Add(cd); ops.Children.Add(del);
                Grid.SetColumn(ops, 2);
                g.Children.Add(ops);
                list.Children.Add(new Border { Child = g, BorderBrush = T.Fg(0.07), BorderThickness = new Thickness(0, i == 0 ? 0 : 1, 0, 0) });
            }
            body.Children.Add(Card(list, new Thickness(14, 2, 10, 2)));

            body.Children.Add(Section("新建提醒 / 倒计时"));
            if (draft == null || S.Cities.All(c => c.TzId != draft.TzId))
            {
                var c0 = S.Cities.FirstOrDefault(x => !x.IsBase) ?? S.Cities.FirstOrDefault();
                draft = new Reminder { Title = "", Hour = 9, Minute = 0, Repeat = "workdays" };
                if (c0 != null) { draft.TzId = c0.TzId; draft.City = c0.Name; draft.Country = c0.Country; }
                else { draft.TzId = TimeZoneInfo.Local.Id; draft.City = "本地"; }
                draft.Date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, draft.Tz).Date;
            }
            var dr = draft;
            var titleTb = T.Input(dr.Title, 13.5); titleTb.MinWidth = 240;
            titleTb.TextChanged += delegate { dr.Title = titleTb.Text; };
            Border cityBtn = null;
            cityBtn = PickerButton(dr.City, () =>
            {
                var items = S.Cities.Select(c => PopMenu.Item.Check("", c.Name, c.TzId == dr.TzId && c.Name == dr.City, () => { dr.TzId = c.TzId; dr.City = c.Name; dr.Country = c.Country; BuildAll(); })).ToList();
                PopMenu.Show(cityBtn, items, PlacementMode.Bottom);
            });
            var timeTb = T.Input(dr.TimeText, 14); timeTb.Width = 60; timeTb.FontFamily = T.Num;
            var dateTb = T.Input(dr.Date.ToString("yyyy-MM-dd"), 14); dateTb.Width = 100; dateTb.FontFamily = T.Num;
            var dateBox = T.Box(dateTb);
            var dateRow = Row("日期", "按所选城市的当地日期，格式 2026-11-20", dateBox);
            Action showDate = () =>
            {
                var fe = (FrameworkElement)dateRow;
                var wrap = (fe.Parent as FrameworkElement) ?? fe;
                wrap.Visibility = dr.Repeat == "once" ? Visibility.Visible : Visibility.Collapsed;
            };
            var repeat = Seg.Make(new[] { "工作日", "每天", "单次" }, new[] { "workdays", "daily", "once" }, dr.Repeat, v => { dr.Repeat = v; showDate(); });
            var lead = Seg.Make(new[] { "准时", "5 分钟", "15 分钟", "30 分钟" }, new[] { "0", "5", "15", "30" }, dr.Lead.ToString(), v => { dr.Lead = int.Parse(v); });
            var cdTg = new ToggleSwitch(dr.Countdown, v => { dr.Countdown = v; });
            var err = T.Text("", 12, 1); err.Foreground = T.B("#F87171"); err.Margin = new Thickness(0, 8, 0, 0);
            var addBtn = T.Pill("添加提醒", () =>
            {
                var m = Regex.Match(timeTb.Text.Trim(), @"^(\d{1,2})[:：](\d{2})$");
                if (!m.Success || int.Parse(m.Groups[1].Value) > 23 || int.Parse(m.Groups[2].Value) > 59) { err.Text = "时间格式不对，例如 09:30"; return; }
                dr.Hour = int.Parse(m.Groups[1].Value); dr.Minute = int.Parse(m.Groups[2].Value);
                if (dr.Repeat == "once")
                {
                    DateTime dd;
                    if (!DateTime.TryParseExact(dateTb.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dd)) { err.Text = "日期格式不对，例如 2026-11-20"; return; }
                    dr.Date = dd;
                    if (!dr.NextUtc(DateTime.UtcNow).HasValue) { err.Text = "这个时间已经过去了"; return; }
                }
                if (string.IsNullOrWhiteSpace(dr.Title)) dr.Title = dr.City + " " + dr.TimeText;
                AddReminder(dr);
                draft = null;
            }, "primary");
            addBtn.HorizontalAlignment = HorizontalAlignment.Left;
            addBtn.Margin = new Thickness(0, 12, 0, 4);

            var form = new StackPanel();
            form.Children.Add(Rows(
                Row("标题", null, T.Box(titleTb)),
                Row("城市", "时间按这个城市的当地时间计算", cityBtn),
                Row("时间", "24 小时制，例如 09:00", T.Box(timeTb)),
                RowWide("重复", null, repeat),
                dateRow,
                RowWide("提前提醒", null, lead),
                Row("在主界面显示倒计时", "适合促销开始、发货截止这类一次性的时间点", cdTg)
            ));
            showDate();
            form.Children.Add(addBtn);
            form.Children.Add(err);
            body.Children.Add(Card(form, new Thickness(16, 2, 16, 10)));
        }

        Border QuickChip(string text, Action act)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(T.Glyph("", 10, T.AccentBrush));
            var tb = T.Text(text, 12.5, 0.9); tb.Margin = new Thickness(6, 0, 0, 0);
            sp.Children.Add(tb);
            var chip = new Border { CornerRadius = new CornerRadius(14), Background = T.Fg(0.07), BorderBrush = T.Fg(0.08), BorderThickness = new Thickness(1), Padding = new Thickness(11, 5, 12, 5), Margin = new Thickness(0, 0, 6, 6), Child = sp, Cursor = Cursors.Hand };
            chip.MouseEnter += delegate { chip.Background = T.B(T.Cur.Accent, 0.22); };
            chip.MouseLeave += delegate { chip.Background = T.Fg(0.07); };
            chip.MouseLeftButtonUp += delegate { act(); };
            return chip;
        }

        void AddReminder(Reminder r)
        {
            // 新建时把「上次触发」设为现在之前，避免立刻补发
            var n = r.NextUtc(DateTime.UtcNow.AddMinutes(-2));
            if (n.HasValue && n.Value <= DateTime.UtcNow) r.LastFired = n.Value.Ticks;
            S.Reminders.Add(r);
            M.ApplyAll(true);
            BuildAll();
        }

        // ═════════════════════ 常规 ═════════════════════
        void GeneralTab()
        {
            body.Children.Add(Section("显示"));
            body.Children.Add(Card(Rows(
                Row("24 小时制", "关闭后显示 上午 / 下午", new ToggleSwitch(S.Use24h, v => { S.Use24h = v; M.ApplyAll(false); })),
                Row("显示秒", null, new ToggleSwitch(S.ShowSeconds, v => { S.ShowSeconds = v; M.ApplyAll(false); })),
                Row("横向排列", "城市卡片左右并排", new ToggleSwitch(S.Horizontal, v => { S.Horizontal = v; M.ApplyAll(true); })),
                Row("迷你模式", "只显示一行时间，可贴在屏幕边上；双击迷你条恢复", new ToggleSwitch(S.Mini, v => { S.Mini = v; M.ApplyAll(true); })),
                Row("时间轴与工作状态", "24 小时进度条，以及对方现在是工作时间、休息还是周末", new ToggleSwitch(S.ShowTimeline, v => { S.ShowTimeline = v; M.ApplyAll(true); })),
                Row("天气", "显示当地天气和气温（来自 Open-Meteo，每 30 分钟更新）", new ToggleSwitch(S.ShowWeather, v => { S.ShowWeather = v; M.ApplyAll(true); })),
                Row("节假日", "标出当地今天或 14 天内的公共假日", new ToggleSwitch(S.ShowHolidays, v => { S.ShowHolidays = v; M.ApplyAll(true); })),
                Row("整点报时", "每到整点播放一次提示音", new ToggleSwitch(S.Chime, v => { S.Chime = v; M.ApplyAll(false); }))
            )));

            body.Children.Add(Section("窗口"));
            body.Children.Add(Card(Rows(
                Row("开机自动启动", "登录 Windows 后自动显示时钟", new ToggleSwitch(AutoStart.IsEnabled(), v => AutoStart.Set(v))),
                RowWide("窗口层级", "置顶：总在最前　普通：和其他窗口一样　桌面：沉在所有窗口下面，像桌面挂件",
                    Seg.Make(new[] { "置顶", "普通", "桌面" }, new[] { "top", "normal", "bottom" }, S.Layer, v => { S.Layer = v; M.ApplyAll(false); })),
                Row("拖动边缘调整大小", "开启后把鼠标移到时钟边缘或四角，拖动即可等比放大缩小", new ToggleSwitch(S.Resizable, v => { S.Resizable = v; M.ApplyAll(true); })),
                Row("锁定位置", "防止误拖动（同时禁用边缘缩放）", new ToggleSwitch(S.Locked, v => { S.Locked = v; M.ApplyAll(true); })),
                Row("鼠标穿透", "点击会穿过时钟落到桌面上；需要时在托盘菜单里关闭", new ToggleSwitch(S.ClickThrough, v => { S.ClickThrough = v; M.ApplyAll(false); })),
                Row("不透明度", null, SliderCtl(0.4, 1, 0.05, S.Opacity, Pct, v => { S.Opacity = v; M.ApplyAll(false); })),
                Row("缩放", null, SliderCtl(0.5, 2.5, 0.05, S.Scale, Pct, v => { S.Scale = v; M.ApplyAll(false); }))
            )));

            body.Children.Add(Section("全局快捷键（点击输入框后按下新的组合键，Backspace 清除）"));
            body.Children.Add(Card(Rows(
                Row("显示 / 隐藏时钟", null, HotkeyBox(S.HotShow, 1, v => S.HotShow = v)),
                Row("迷你模式", null, HotkeyBox(S.HotMini, 2, v => S.HotMini = v)),
                Row("时间换算", null, HotkeyBox(S.HotConv, 3, v => S.HotConv = v))
            )));
        }

        UIElement HotkeyBox(string cur, int id, Action<string> set)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var status = T.Text("", 11.5, 0.55);
            status.Margin = new Thickness(0, 0, 10, 0);
            Action refresh = () =>
            {
                string v = id == 1 ? S.HotShow : id == 2 ? S.HotMini : S.HotConv;
                status.Text = string.IsNullOrEmpty(v) ? "未设置" : M.HotkeyOk[id] ? "" : "已被占用";
                status.Foreground = M.HotkeyOk[id] ? T.Fg(0.55) : T.B("#F87171");
            };
            var tb = T.Input(string.IsNullOrEmpty(cur) ? "" : cur, 13);
            tb.IsReadOnly = true; tb.Width = 130; tb.Cursor = Cursors.Hand; tb.FontFamily = T.Num; tb.HorizontalContentAlignment = HorizontalAlignment.Center;
            var box = T.Box(tb);
            tb.GotKeyboardFocus += delegate { box.BorderBrush = T.AccentBrush; };
            tb.LostKeyboardFocus += delegate { box.BorderBrush = T.Fg(0.12); };
            tb.PreviewKeyDown += (o, e) =>
            {
                e.Handled = true;
                var k = e.Key == Key.System ? e.SystemKey : e.Key;
                if (k == Key.Escape || k == Key.Tab) { Keyboard.ClearFocus(); return; }
                if (k == Key.Back || k == Key.Delete) { tb.Text = ""; set(""); M.RegisterHotkeys(); S.Save(); refresh(); return; }
                if (HotkeyUtil.IsModifier(k)) return;
                var mods = Keyboard.Modifiers;
                if (mods == ModifierKeys.None) return;
                string s = HotkeyUtil.Format(mods, k);
                tb.Text = s; set(s); M.RegisterHotkeys(); S.Save(); refresh();
            };
            refresh();
            sp.Children.Add(status); sp.Children.Add(box);
            return sp;
        }

        // ═════════════════════ 关于 ═════════════════════
        void AboutTab()
        {
            body.Children.Add(Section("世界时钟 2.0"));
            var tips = new StackPanel();
            string[] lines = {
                "单击托盘图标显示 / 隐藏，或按 " + S.HotShow,
                "在卡片上滚动鼠标滚轮可以快速换算时间",
                "右键卡片：复制时间发给客户、设为基准、排序、删除",
                "会议时间规划：自动找出各城市都方便的时段，点击某一列即可复制会议时间",
                "夏令时（如智利）由 Windows 时区数据自动处理",
                "天气来自 Open-Meteo（免费、无需账号），只发送城市的经纬度",
            };
            foreach (var l in lines)
            {
                var g = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
                g.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = T.AccentGrad(), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                var tx = T.Text(l, 12.5, 0.75); tx.TextWrapping = TextWrapping.Wrap; tx.MaxWidth = 440;
                g.Children.Add(tx);
                tips.Children.Add(g);
            }
            body.Children.Add(Card(tips, new Thickness(16, 10, 16, 10)));

            body.Children.Add(Section("数据"));
            var msg = T.Text("", 12, 0.65);
            var btns = new WrapPanel();
            Action<string, Action, string> add = (text, act, kind) => { var p = T.Pill(text, act, kind); p.Margin = new Thickness(0, 0, 8, 8); btns.Children.Add(p); };
            add("打开配置文件夹", () => { try { IO.Directory.CreateDirectory(AppSettings.Dir); System.Diagnostics.Process.Start("explorer.exe", "\"" + AppSettings.Dir + "\""); } catch { } }, "normal");
            add("编辑节假日", () => { try { Holidays.Load(true); System.Diagnostics.Process.Start("notepad.exe", "\"" + Holidays.FilePath + "\""); msg.Text = "保存后点「重新加载节假日」"; } catch { } }, "normal");
            add("重新加载节假日", () => { Holidays.Load(true); M.ApplyAll(true); msg.Text = "已重新加载"; }, "normal");
            add("退出程序", () => M.ExitApp(), "danger");
            var sp = new StackPanel();
            var d = T.Text("配置保存在 " + AppSettings.Dir, 12, 0.55); d.TextWrapping = TextWrapping.Wrap; d.Margin = new Thickness(0, 0, 0, 10);
            sp.Children.Add(d); sp.Children.Add(btns); sp.Children.Add(msg);
            body.Children.Add(Card(sp, new Thickness(16, 14, 16, 10)));
        }
    }
}

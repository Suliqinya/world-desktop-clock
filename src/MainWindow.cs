// 主窗口：卡片 / 迷你模式 / 时间换算 / 倒计时 / 拖边缩放 / 托盘 / 快捷键 / 提醒调度
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WF = System.Windows.Forms;
using SD = System.Drawing;

namespace WorldClock
{
    class MiniItem { public CityInfo City; public TimeZoneInfo Tz; public TextBlock Time; public Ellipse Dot; }
    class CdRow { public Reminder R; public TextBlock Remain, Sub; }
    class Snooze { public DateTime Utc; public string Title, Body; }

    public class MainWindow : Window
    {
        public readonly AppSettings S;
        readonly Grid outer = new Grid();
        readonly ScaleTransform scaleTf = new ScaleTransform(1, 1);
        Grid root;
        StackPanel cardsPanel, headerBtns;
        Border convPanel;
        FlatSlider convSlider;
        TextBlock convLabel, convSub;
        IconButton pinBtn, convBtn;
        readonly List<CityCard> cards = new List<CityCard>();
        readonly List<MiniItem> minis = new List<MiniItem>();
        readonly List<CdRow> cdRows = new List<CdRow>();
        readonly List<Snooze> snoozes = new List<Snooze>();
        readonly DispatcherTimer timer, saveTimer, themeSaveTimer;
        bool converting; double convHours;
        int lastSec = -1, lastChimeHour = -1, lastMinute = -1;
        WF.NotifyIcon tray;
        IntPtr hwnd;
        SettingsWindow settingsWin;
        PlannerWindow plannerWin;
        bool exiting, hiddenTipShown, rebuildPending;
        readonly string shotPath;
        public readonly bool[] HotkeyOk = new bool[4];

        public MainWindow(AppSettings s, string shot)
        {
            S = s; shotPath = shot;
            Title = "世界时钟";
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; SizeToContent = SizeToContent.WidthAndHeight;
            UseLayoutRounding = true;
            Icon = AppIcon.Wpf(64);
            Content = outer;

            MouseEnter += delegate { if (headerBtns != null) T.Fade(headerBtns, 1); };
            MouseLeave += delegate { if (headerBtns != null) T.Fade(headerBtns, 0); };

            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            saveTimer.Tick += delegate { saveTimer.Stop(); S.Save(); };
            themeSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            themeSaveTimer.Tick += delegate { themeSaveTimer.Stop(); if (!S.NoSave) T.Cur.SaveCurrent(); };
            LocationChanged += delegate
            {
                if (shotPath != null || !IsVisible) return;
                S.Left = Left; S.Top = Top; saveTimer.Stop(); saveTimer.Start();
            };

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += (o, e) => { if (!exiting) { e.Cancel = true; HideToTray(); } };

            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += delegate { Tick(false); KeepAboveTaskbar(); };

            Weather.Updated = () => { foreach (var c in cards) c.SetWeather(S.ShowWeather ? Weather.Get(c.City) : null); };

            scaleTf.ScaleX = scaleTf.ScaleY = S.Scale;
            Opacity = S.Opacity;
            Topmost = S.Layer == "top";
            Rebuild();
            timer.Start();

            if (shotPath == null) InitTray();
            else { Left = -20000; Top = -20000; ShowActivated = false; }
        }

        // ───────── 生命周期 ─────────
        void OnSourceInitialized(object sender, EventArgs e)
        {
            hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(hwnd);
            if (src != null) src.AddHook(WndProc);
            ApplyExStyle();
            ApplyLayer();
            if (shotPath == null) RegisterHotkeys();
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (shotPath != null) return;
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            double left = S.Left, top = S.Top;
            bool ok = !double.IsNaN(left) && !double.IsNaN(top) &&
                      left > vl - ActualWidth + 80 && left < vl + vw - 80 && top > vt - 20 &&
                      top < vt + vh - Math.Min(80, ActualHeight / 2);
            if (ok) { Left = left; Top = top; }
            else { var wa = SystemParameters.WorkArea; Left = wa.Right - ActualWidth - 8; Top = wa.Top + 8; }
        }

        IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_WINDOWPOSCHANGING && S.Layer == "bottom")
            {
                var wp = (Native.WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(Native.WINDOWPOS));
                wp.hwndInsertAfter = Native.HWND_BOTTOM;
                wp.flags &= ~Native.SWP_NOZORDER;
                Marshal.StructureToPtr(wp, lParam, false);
            }
            else if (msg == Native.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == 1) ToggleVisible();
                else if (id == 2) ToggleMini();
                else if (id == 3) { ShowFront(); if (S.Mini) { S.Mini = false; ApplyAll(true); } SetConverting(!converting); }
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void RegisterHotkeys()
        {
            if (hwnd == IntPtr.Zero) return;
            string[] keys = { null, S.HotShow, S.HotMini, S.HotConv };
            for (int id = 1; id <= 3; id++)
            {
                Native.UnregisterHotKey(hwnd, id);
                uint m, vk;
                HotkeyOk[id] = HotkeyUtil.Parse(keys[id], out m, out vk) && Native.RegisterHotKey(hwnd, id, m | 0x4000, vk);
            }
        }

        // ───────── 设置应用 ─────────
        public void ApplyAll(bool rebuild)
        {
            Opacity = Math.Max(0.3, Math.Min(1, S.Opacity));
            scaleTf.ScaleX = scaleTf.ScaleY = S.Scale;
            ApplyLayer();
            ApplyExStyle();
            if (rebuild) RequestRebuild();
            else { UpdatePin(); Tick(true); }
            S.Save();
        }

        public void ThemeChanged()
        {
            T.SetTheme(T.Cur);
            themeSaveTimer.Stop(); themeSaveTimer.Start();
            RequestRebuild();
            if (settingsWin != null) settingsWin.ScheduleRestyle();
            if (plannerWin != null) plannerWin.Build();
        }

        void ApplyLayer()
        {
            Topmost = S.Layer == "top";
            if (hwnd != IntPtr.Zero && S.Layer == "bottom")
                Native.SetWindowPos(hwnd, Native.HWND_BOTTOM, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        void KeepAboveTaskbar()
        {
            Native.RECT clockRect;
            if (shotPath != null || S.Layer != "top" || !IsVisible || WindowState == WindowState.Minimized ||
                hwnd == IntPtr.Zero || !Native.GetWindowRect(hwnd, out clockRect))
                return;

            foreach (string taskbarClass in Native.TaskbarClasses)
            {
                IntPtr taskbar = IntPtr.Zero;
                while ((taskbar = Native.FindWindowEx(IntPtr.Zero, taskbar, taskbarClass, null)) != IntPtr.Zero)
                {
                    Native.RECT taskbarRect;
                    if (!Native.IsWindowVisible(taskbar) || !Native.GetWindowRect(taskbar, out taskbarRect) ||
                        !clockRect.Intersects(taskbarRect))
                        continue;

                    // The taskbar can move ahead of other topmost windows when clicked.
                    for (var above = Native.GetWindow(hwnd, Native.GW_HWNDPREV); above != IntPtr.Zero;
                         above = Native.GetWindow(above, Native.GW_HWNDPREV))
                    {
                        if (above != taskbar) continue;
                        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                        return;
                    }
                }
            }
        }

        void ApplyExStyle()
        {
            if (hwnd == IntPtr.Zero) return;
            int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE) | Native.WS_EX_TOOLWINDOW;
            if (S.ClickThrough) ex |= Native.WS_EX_TRANSPARENT; else ex &= ~Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);
        }

        void UpdatePin()
        {
            if (pinBtn == null) return;
            bool top = S.Layer == "top";
            pinBtn.Glyph = top ? "" : "";
            pinBtn.Active = top;
            pinBtn.ToolTip = top ? "取消置顶" : "置顶显示";
        }

        void TogglePin() { S.Layer = S.Layer == "top" ? "normal" : "top"; ApplyAll(false); RefreshSettings(); }
        public void ToggleMini() { ShowFront(); S.Mini = !S.Mini; ApplyAll(true); RefreshSettings(); }

        public TimeZoneInfo BaseTz()
        {
            var b = S.BaseCity;
            return b != null ? (TimeUtil.Find(b.TzId) ?? TimeZoneInfo.Local) : TimeZoneInfo.Local;
        }
        public string BaseName()
        {
            var b = S.BaseCity;
            return b != null ? b.Name : "系统时区";
        }

        // ───────── 构建界面 ─────────
        public void RequestRebuild()
        {
            if (rebuildPending) return;
            rebuildPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Rebuild));
        }

        public void Rebuild()
        {
            rebuildPending = false;
            FontFamily = T.UI; Foreground = T.Fg(1);
            outer.Children.Clear();
            cards.Clear(); minis.Clear(); cdRows.Clear();
            headerBtns = null; pinBtn = null; convBtn = null; convPanel = null;

            root = new Grid { Margin = new Thickness(16), LayoutTransform = scaleTf };
            outer.Children.Add(root);
            var layer = T.Shell(root);
            var content = new StackPanel { Margin = S.Mini ? new Thickness(14, 8, 8, 8) : new Thickness(14, 10, 14, 14) };
            layer.Children.Add(content);

            root.MouseLeftButtonDown += (o, e) =>
            {
                if (e.ClickCount == 2 && S.Mini) { ToggleMini(); return; }
                if (!S.Locked && e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
            };
            root.MouseRightButtonUp += (o, e) => { e.Handled = true; ShowMainMenu(); };

            if (S.Mini) BuildMini(content);
            else { BuildHeader(content); BuildConv(content); BuildCards(content); BuildCountdowns(content); }
            if (S.Resizable && !S.Locked) AddGrips();

            lastSec = -1; lastMinute = -1;
            Tick(true);
        }

        void BuildHeader(StackPanel content)
        {
            var header = new Grid { Margin = new Thickness(4, 0, 0, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = T.AccentGrad(), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            title.Children.Add(T.Text("世界时钟", 12.5, 0.62));
            var bt = T.Text("  ·  基准 " + BaseName(), 12, 0.42);
            bt.ToolTip = "「快 / 慢 X 小时」以此为准，可在设置 → 城市里更改";
            title.Children.Add(bt);
            header.Children.Add(title);

            headerBtns = new StackPanel { Orientation = Orientation.Horizontal, Opacity = IsMouseOver ? 1 : 0, Margin = new Thickness(8, 0, 0, 0) };
            convBtn = new IconButton("", "时间换算  " + S.HotConv, () => SetConverting(!converting));
            convBtn.Active = converting;
            pinBtn = new IconButton("", "置顶显示", TogglePin);
            headerBtns.Children.Add(convBtn);
            headerBtns.Children.Add(new IconButton("", "会议时间规划", OpenPlanner));
            headerBtns.Children.Add(new IconButton("", "迷你模式  " + S.HotMini, ToggleMini));
            headerBtns.Children.Add(pinBtn);
            headerBtns.Children.Add(new IconButton("", "设置", () => OpenSettings(-1)));
            headerBtns.Children.Add(new IconButton("", "隐藏到托盘  " + S.HotShow, HideToTray));
            Grid.SetColumn(headerBtns, 1);
            header.Children.Add(headerBtns);
            content.Children.Add(header);
            UpdatePin();
        }

        void BuildConv(StackPanel content)
        {
            var cp = new StackPanel();
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tt = new StackPanel();
            tt.Children.Add(T.Text("时间换算", 13, 0.95));
            convSub = T.Text("", 11.5, 0.6);
            tt.Children.Add(convSub);
            convLabel = new TextBlock { FontFamily = T.Num, FontSize = 22, Foreground = T.HighlightBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(convLabel, 1);
            top.Children.Add(tt); top.Children.Add(convLabel);
            convSlider = new FlatSlider(-24, 24, 0.5, convHours, true) { Margin = new Thickness(0, 8, 0, 4) };
            convSlider.Changed = v => { convHours = v; Tick(true); };
            var bottom = new Grid();
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottom.Children.Add(T.Text("拖动滑块，或在卡片上滚动鼠标滚轮", 11, 0.5));
            var reset = T.Pill("回到现在", () => SetConverting(false), "normal");
            reset.Padding = new Thickness(10, 4, 10, 4);
            Grid.SetColumn(reset, 1);
            bottom.Children.Add(reset);
            cp.Children.Add(top); cp.Children.Add(convSlider); cp.Children.Add(bottom);
            convPanel = new Border
            {
                CornerRadius = new CornerRadius(Math.Min(14, T.Cur.CardRadius)), Background = T.Fg(0.06), BorderBrush = T.Fg(0.08), BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), Child = cp,
                Visibility = converting ? Visibility.Visible : Visibility.Collapsed
            };
            content.Children.Add(convPanel);
        }

        void BuildCards(StackPanel content)
        {
            cardsPanel = new StackPanel { Orientation = S.Horizontal ? Orientation.Horizontal : Orientation.Vertical };
            var opts = new CardOpts { Horiz = S.Horizontal, Timeline = S.ShowTimeline, Weather = S.ShowWeather, Holidays = S.ShowHolidays };
            foreach (var c in S.Cities)
            {
                var card = new CityCard(c, opts);
                var cc = card;
                card.MouseWheel += (o, e) =>
                {
                    e.Handled = true;
                    if (!converting) SetConverting(true);
                    convSlider.Value = convSlider.Value + (e.Delta > 0 ? 0.5 : -0.5);
                };
                card.MouseRightButtonUp += (o, e) => { e.Handled = true; ShowCardMenu(cc); };
                if (S.ShowWeather) card.SetWeather(Weather.Get(c));
                cards.Add(card);
                cardsPanel.Children.Add(card);
            }
            if (cards.Count > 0) cards[cards.Count - 1].Margin = new Thickness(0);
            else
            {
                var hint = new Border
                {
                    Width = 300, Padding = new Thickness(16), CornerRadius = new CornerRadius(14), Background = T.Fg(0.06), Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = "还没有城市，点击这里添加", FontSize = 13, Foreground = T.Fg(0.7), HorizontalAlignment = HorizontalAlignment.Center }
                };
                hint.MouseLeftButtonUp += delegate { OpenSettings(0); };
                cardsPanel.Children.Add(hint);
            }
            content.Children.Add(cardsPanel);
        }

        void BuildCountdowns(StackPanel content)
        {
            var list = S.Reminders.Where(r => r.Enabled && r.Countdown).ToList();
            if (list.Count == 0) return;
            var wrap = new WrapPanel { Margin = new Thickness(0, 10, 0, 0), MaxWidth = S.Horizontal ? double.PositiveInfinity : (cards.Count > 0 ? cards[0].Width : 356) };
            if (S.Horizontal && cards.Count > 0) wrap.MaxWidth = cards.Count * (cards[0].Width + 10);
            foreach (var r in list.Take(4))
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var title = new StackPanel { Orientation = Orientation.Horizontal };
                title.Children.Add(T.Glyph("", 12, T.AccentBrush));
                var tt = T.Text(r.Title, 13, 0.95); tt.Margin = new Thickness(7, 0, 0, 0); tt.FontWeight = FontWeights.SemiBold; tt.MaxWidth = 170;
                title.Children.Add(tt);
                var sub = T.Text("", 11.5, 0.55); sub.Margin = new Thickness(0, 3, 0, 0);
                left.Children.Add(title); left.Children.Add(sub);
                var remain = new TextBlock { FontFamily = T.Num, FontSize = 20, Foreground = T.Fg(1), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                Grid.SetColumn(remain, 1);
                g.Children.Add(left); g.Children.Add(remain);
                double w = S.Horizontal ? (cards.Count > 0 ? cards[0].Width : 216) : (cards.Count > 0 ? cards[0].Width : 356);
                var row = new Border
                {
                    CornerRadius = new CornerRadius(Math.Min(14, T.Cur.CardRadius)), Background = T.Fg(0.06), BorderBrush = T.Fg(0.08), BorderThickness = new Thickness(1),
                    Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, S.Horizontal ? 10 : 0, 8), Child = g, Width = w,
                    ToolTip = "倒计时，可在 设置 → 提醒 里管理"
                };
                var rr = r;
                row.MouseRightButtonUp += (o, e) =>
                {
                    e.Handled = true;
                    PopMenu.Show(this, new List<PopMenu.Item> {
                        PopMenu.Item.Of("", "不在主界面显示此倒计时", () => { rr.Countdown = false; ApplyAll(true); RefreshSettings(); }),
                        PopMenu.Item.Of("", "管理提醒…", () => OpenSettings(2)),
                    });
                };
                cdRows.Add(new CdRow { R = r, Remain = remain, Sub = sub });
                wrap.Children.Add(row);
            }
            content.Children.Add(wrap);
        }

        void BuildMini(StackPanel content)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            bool first = true;
            foreach (var c in S.Cities)
            {
                if (!first) sp.Children.Add(new Border { Width = 1, Height = 18, Background = T.Fg(0.14), Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
                first = false;
                var item = new MiniItem { City = c, Tz = TimeUtil.Find(c.TzId) ?? TimeZoneInfo.Utc };
                item.Dot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
                var name = T.Text(c.Name, 12.5, 0.72);
                name.Margin = new Thickness(0, 0, 8, 0);
                item.Time = new TextBlock { FontFamily = T.Num, FontSize = 19, FontWeight = T.Weight(T.Cur.TimeWeight), Foreground = T.Fg(1), VerticalAlignment = VerticalAlignment.Center };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(item.Dot); row.Children.Add(name); row.Children.Add(item.Time);
                sp.Children.Add(row);
                minis.Add(item);
            }
            if (S.Cities.Count == 0) sp.Children.Add(T.Text("没有城市", 12.5, 0.6));
            var expand = new IconButton("", "退出迷你模式（双击也可以）", ToggleMini, 24, 10);
            expand.Margin = new Thickness(10, 0, 0, 0);
            headerBtns = new StackPanel { Orientation = Orientation.Horizontal, Opacity = IsMouseOver ? 1 : 0 };
            headerBtns.Children.Add(expand);
            sp.Children.Add(headerBtns);
            content.Children.Add(sp);
        }

        // ── 拖动边缘调整大小（等比缩放） ──
        void AddGrips()
        {
            const double m = 16, t = 12;
            var clear = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            Action<HorizontalAlignment, VerticalAlignment, double, double, Thickness, Cursor, int, int> add = (ha, va, w, h, margin, cur, dx, dy) =>
            {
                var g = new Border { HorizontalAlignment = ha, VerticalAlignment = va, Margin = margin, Background = clear, Cursor = cur };
                if (!double.IsNaN(w)) g.Width = w;
                if (!double.IsNaN(h)) g.Height = h;
                HookGrip(g, dx, dy);
                outer.Children.Add(g);
            };
            double n = double.NaN, off = m - t / 2;
            add(HorizontalAlignment.Left, VerticalAlignment.Stretch, t, n, new Thickness(off, m + 14, 0, m + 14), Cursors.SizeWE, -1, 0);
            add(HorizontalAlignment.Right, VerticalAlignment.Stretch, t, n, new Thickness(0, m + 14, off, m + 14), Cursors.SizeWE, 1, 0);
            add(HorizontalAlignment.Stretch, VerticalAlignment.Top, n, t, new Thickness(m + 14, off, m + 14, 0), Cursors.SizeNS, 0, -1);
            add(HorizontalAlignment.Stretch, VerticalAlignment.Bottom, n, t, new Thickness(m + 14, 0, m + 14, off), Cursors.SizeNS, 0, 1);
            add(HorizontalAlignment.Left, VerticalAlignment.Top, 22, 22, new Thickness(off - 2, off - 2, 0, 0), Cursors.SizeNWSE, -1, -1);
            add(HorizontalAlignment.Right, VerticalAlignment.Bottom, 22, 22, new Thickness(0, 0, off - 2, off - 2), Cursors.SizeNWSE, 1, 1);
            add(HorizontalAlignment.Right, VerticalAlignment.Top, 22, 22, new Thickness(0, off - 2, off - 2, 0), Cursors.SizeNESW, 1, -1);
            add(HorizontalAlignment.Left, VerticalAlignment.Bottom, 22, 22, new Thickness(off - 2, 0, 0, off - 2), Cursors.SizeNESW, -1, 1);
        }

        void HookGrip(Border g, int dx, int dy)
        {
            Point start = new Point();
            double s0 = 1, w0 = 1, h0 = 1, right0 = 0, bottom0 = 0;
            g.MouseLeftButtonDown += (o, e) =>
            {
                e.Handled = true;
                g.CaptureMouse();
                start = ScreenDip(e);
                s0 = S.Scale; w0 = Math.Max(50, ActualWidth - 32); h0 = Math.Max(30, ActualHeight - 32);
                right0 = Left + ActualWidth; bottom0 = Top + ActualHeight;
            };
            g.MouseMove += (o, e) =>
            {
                if (!g.IsMouseCaptured) return;
                var p = ScreenDip(e);
                double rw = dx == 0 ? 0 : (w0 + dx * (p.X - start.X)) / w0;
                double rh = dy == 0 ? 0 : (h0 + dy * (p.Y - start.Y)) / h0;
                double r = dx != 0 && dy != 0 ? Math.Max(rw, rh) : (dx != 0 ? rw : rh);
                double ns = Math.Max(0.5, Math.Min(3, s0 * r));
                if (Math.Abs(ns - S.Scale) < 0.002) return;
                S.Scale = ns;
                scaleTf.ScaleX = scaleTf.ScaleY = ns;
                UpdateLayout();
                if (dx < 0) Left = right0 - ActualWidth;
                if (dy < 0) Top = bottom0 - ActualHeight;
            };
            g.MouseLeftButtonUp += (o, e) =>
            {
                e.Handled = true;
                if (!g.IsMouseCaptured) return;
                g.ReleaseMouseCapture();
                S.Save();
                RefreshSettings();
            };
        }

        Point ScreenDip(MouseEventArgs e)
        {
            var p = PointToScreen(e.GetPosition(this));
            var src = PresentationSource.FromVisual(this);
            return src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.Transform(p) : p;
        }

        // ───────── 每秒刷新 ─────────
        DateTime BaseUtc()
        {
            return converting ? TimeUtil.Floor30(DateTime.UtcNow).AddHours(convHours) : DateTime.UtcNow;
        }

        void Tick(bool force)
        {
            var now = DateTime.UtcNow;
            if (!force && now.Second == lastSec) return;
            lastSec = now.Second;
            var utc = BaseUtc();
            var btz = BaseTz();
            foreach (var c in cards) c.Update(utc, converting, S, btz);

            foreach (var m in minis)
            {
                var t = TimeZoneInfo.ConvertTimeFromUtc(now, m.Tz);
                m.Time.Text = S.Use24h ? t.ToString("HH:mm") : (t.Hour < 12 ? "上午" : "下午") + (t.Hour % 12 == 0 ? 12 : t.Hour % 12) + t.ToString(":mm");
                string lb;
                int sc = TimeUtil.Score(t, S.ShowHolidays ? m.City.Country : "", out lb);
                m.Dot.Fill = T.B(sc == 2 ? "#34D399" : sc == 1 ? "#FB923C" : "#9AA8C7");
                m.Dot.ToolTip = lb;
            }

            if (converting && convSub != null)
            {
                var bt = TimeZoneInfo.ConvertTimeFromUtc(utc, btz);
                convSub.Text = BaseName() + " " + TimeUtil.DateText(bt) + " " + bt.ToString("HH:mm");
                convLabel.Text = convHours == 0 ? "±0" : (convHours > 0 ? "+" : "−") + TimeUtil.Span(TimeSpan.FromHours(convHours));
            }

            foreach (var row in cdRows)
            {
                var n = row.R.NextUtc(now);
                if (n.HasValue)
                {
                    var lt = TimeZoneInfo.ConvertTimeFromUtc(n.Value, row.R.Tz);
                    row.Remain.Text = TimeUtil.Remain(n.Value - now);
                    row.Sub.Text = row.R.City + " " + TimeUtil.DateText(lt) + " " + lt.ToString("HH:mm");
                }
                else { row.Remain.Text = "已结束"; row.Sub.Text = row.R.Describe(); }
            }

            if (now.Minute != lastMinute)
            {
                lastMinute = now.Minute;
                if (S.ShowWeather) foreach (var c in cards) c.SetWeather(Weather.Get(c.City));
            }

            if (tray != null && (now.Second % 5 == 0 || force)) UpdateTrayText();

            if (shotPath == null)
            {
                CheckReminders(now);
                if (S.Chime)
                {
                    var l = DateTime.Now;
                    if (l.Minute == 0 && l.Hour != lastChimeHour) { lastChimeHour = l.Hour; try { System.Media.SystemSounds.Asterisk.Play(); } catch { } }
                    if (l.Minute != 0) lastChimeHour = -1;
                }
            }
        }

        void CheckReminders(DateTime now)
        {
            bool changed = false;
            foreach (var r in S.Reminders)
            {
                if (!r.Enabled) continue;
                var n = r.NextUtc(now.AddMinutes(-2));
                if (!n.HasValue) continue;
                if (now >= n.Value.AddMinutes(-r.Lead) && r.LastFired < n.Value.Ticks)
                {
                    r.LastFired = n.Value.Ticks;
                    changed = true;
                    Fire(r, n.Value);
                }
            }
            for (int i = snoozes.Count - 1; i >= 0; i--)
            {
                if (now < snoozes[i].Utc) continue;
                var sz = snoozes[i];
                snoozes.RemoveAt(i);
                NotifyWindow.Popup(sz.Title, sz.Body, () => snoozes.Add(new Snooze { Utc = DateTime.UtcNow.AddMinutes(10), Title = sz.Title, Body = sz.Body }));
            }
            if (changed) S.Save();
        }

        void Fire(Reminder r, DateTime occUtc)
        {
            var lt = TimeZoneInfo.ConvertTimeFromUtc(occUtc, r.Tz);
            var bt = TimeZoneInfo.ConvertTimeFromUtc(occUtc, BaseTz());
            string body = r.City + " " + lt.ToString("HH:mm");
            if (S.BaseCity == null || S.BaseCity.TzId != r.TzId) body += "（" + BaseName() + " " + bt.ToString("HH:mm") + "）";
            var left = occUtc - DateTime.UtcNow;
            if (left.TotalMinutes >= 1) body += " · 还有 " + TimeUtil.Span(TimeSpan.FromMinutes(Math.Ceiling(left.TotalMinutes)));
            string title = string.IsNullOrWhiteSpace(r.Title) ? "提醒" : r.Title;
            NotifyWindow.Popup(title, body, () => snoozes.Add(new Snooze { Utc = DateTime.UtcNow.AddMinutes(10), Title = title, Body = body }));
        }

        void UpdateTrayText()
        {
            if (tray == null) return;
            var sb = new StringBuilder();
            var utc = DateTime.UtcNow;
            foreach (var c in S.Cities)
            {
                var tz = TimeUtil.Find(c.TzId);
                if (tz == null) continue;
                string line = c.Name + " " + TimeZoneInfo.ConvertTimeFromUtc(utc, tz).ToString("HH:mm");
                if (sb.Length + line.Length + 1 > 63) break;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(line);
            }
            try { tray.Text = sb.Length > 0 ? sb.ToString() : "世界时钟"; } catch { }
        }

        public void SetConverting(bool on)
        {
            converting = on;
            if (convPanel != null) convPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (convBtn != null) convBtn.Active = on;
            if (!on) { convHours = 0; if (convSlider != null) convSlider.Value = 0; }
            Tick(true);
        }

        public void SetConvHours(double h) { if (!converting) SetConverting(true); convSlider.Value = h; }

        // ───────── 菜单 ─────────
        void ShowMainMenu()
        {
            PopMenu.Show(this, new List<PopMenu.Item> {
                PopMenu.Item.Check("", "时间换算", converting, () => { if (S.Mini) { S.Mini = false; ApplyAll(true); } SetConverting(!converting); }),
                PopMenu.Item.Of("", "会议时间规划…", OpenPlanner),
                PopMenu.Item.Check("", "迷你模式", S.Mini, ToggleMini),
                PopMenu.Item.Separator(),
                PopMenu.Item.Of("", "添加 / 管理城市…", () => OpenSettings(0)),
                PopMenu.Item.Of("", "更换主题…", () => OpenSettings(1)),
                PopMenu.Item.Of("", "提醒与倒计时…", () => OpenSettings(2)),
                PopMenu.Item.Separator(),
                PopMenu.Item.Check("", "置顶显示", S.Layer == "top", TogglePin),
                PopMenu.Item.Check("", "锁定位置", S.Locked, () => { S.Locked = !S.Locked; ApplyAll(true); RefreshSettings(); }),
                PopMenu.Item.Check("", "拖动边缘调整大小", S.Resizable, () => { S.Resizable = !S.Resizable; ApplyAll(true); RefreshSettings(); }),
                PopMenu.Item.Check("", "横向排列", S.Horizontal, () => { S.Horizontal = !S.Horizontal; ApplyAll(true); RefreshSettings(); }),
                PopMenu.Item.Separator(),
                PopMenu.Item.Of("", "设置…", () => OpenSettings(-1)),
                PopMenu.Item.Of("", "隐藏到托盘", HideToTray),
                new PopMenu.Item { Glyph = "", Text = "退出", Act = ExitApp, Danger = true },
            });
        }

        void ShowCardMenu(CityCard card)
        {
            var c = card.City;
            PopMenu.Show(this, new List<PopMenu.Item> {
                PopMenu.Item.Of("", "复制「" + c.Name + "」的时间", () => { try { Clipboard.SetText(card.CopyText(BaseUtc())); } catch { } }),
                PopMenu.Item.Of("", "复制所有城市时间", CopyAll),
                PopMenu.Item.Separator(),
                PopMenu.Item.Check("", "设为时差基准", c.IsBase, () => { foreach (var x in S.Cities) x.IsBase = false; c.IsBase = true; ApplyAll(true); RefreshSettings(); }),
                PopMenu.Item.Of("", "为「" + c.Name + "」添加提醒…", () => OpenSettings(2)),
                PopMenu.Item.Separator(),
                PopMenu.Item.Of("", "上移", () => Move(c, -1)),
                PopMenu.Item.Of("", "下移", () => Move(c, 1)),
                PopMenu.Item.Of("", "重命名 / 编辑…", () => OpenSettings(0)),
                new PopMenu.Item { Glyph = "", Text = "删除此城市", Danger = true, Act = () => { S.Cities.Remove(c); ApplyAll(true); RefreshSettings(); } },
            });
        }

        void CopyAll()
        {
            var utc = BaseUtc();
            try { Clipboard.SetText(string.Join(Environment.NewLine, cards.Select(c => c.CopyText(utc)))); } catch { }
        }

        void Move(CityInfo c, int d)
        {
            int idx = S.Cities.IndexOf(c), j = idx + d;
            if (idx < 0 || j < 0 || j >= S.Cities.Count) return;
            S.Cities[idx] = S.Cities[j]; S.Cities[j] = c;
            ApplyAll(true); RefreshSettings();
        }

        // ───────── 子窗口 / 托盘 ─────────
        public void OpenSettings(int tab)
        {
            if (settingsWin == null)
            {
                settingsWin = new SettingsWindow(this, S, tab < 0 ? 0 : tab);
                settingsWin.Closed += delegate { settingsWin = null; };
                settingsWin.Show();
            }
            else if (tab >= 0) settingsWin.ShowTab(tab);
            settingsWin.Activate();
        }
        public SettingsWindow CreateSettingsForShot(int tab) { settingsWin = new SettingsWindow(this, S, tab); return settingsWin; }

        public void OpenPlanner()
        {
            if (plannerWin == null)
            {
                plannerWin = new PlannerWindow(this, S);
                plannerWin.Closed += delegate { plannerWin = null; };
                plannerWin.Show();
            }
            plannerWin.Activate();
        }
        public PlannerWindow CreatePlannerForShot() { plannerWin = new PlannerWindow(this, S); return plannerWin; }

        public void RefreshSettings() { if (settingsWin != null) settingsWin.BuildAll(); if (plannerWin != null) plannerWin.Build(); }

        void InitTray()
        {
            tray = new WF.NotifyIcon { Icon = AppIcon.Tray(), Text = "世界时钟", Visible = true };
            tray.MouseClick += (o, e) => { if (e.Button == WF.MouseButtons.Left) ToggleVisible(); };
            var menu = new WF.ContextMenuStrip { Renderer = new WF.ToolStripProfessionalRenderer(new DarkColors()), ShowImageMargin = false, ShowCheckMargin = true };
            menu.Font = new SD.Font("Microsoft YaHei UI", 9.5f);
            menu.Opening += delegate { FillTrayMenu(menu); };
            FillTrayMenu(menu);
            tray.ContextMenuStrip = menu;
            UpdateTrayText();
        }

        void FillTrayMenu(WF.ContextMenuStrip m)
        {
            m.Items.Clear();
            Action<string, bool, Action> add = (text, check, act) =>
            {
                var it = new WF.ToolStripMenuItem(text) { Checked = check, ForeColor = SD.Color.White, Padding = new WF.Padding(0, 3, 0, 3) };
                it.Click += delegate { act(); };
                m.Items.Add(it);
            };
            add(IsVisible ? "隐藏时钟" : "显示时钟", false, ToggleVisible);
            add("迷你模式", S.Mini, ToggleMini);
            add("会议时间规划…", false, OpenPlanner);
            m.Items.Add(new WF.ToolStripSeparator());
            add("置顶显示", S.Layer == "top", TogglePin);
            add("锁定位置", S.Locked, () => { S.Locked = !S.Locked; ApplyAll(true); RefreshSettings(); });
            add("拖动边缘调整大小", S.Resizable, () => { S.Resizable = !S.Resizable; ApplyAll(true); RefreshSettings(); });
            add("鼠标穿透", S.ClickThrough, () => { S.ClickThrough = !S.ClickThrough; ApplyAll(false); RefreshSettings(); });
            add("开机自动启动", AutoStart.IsEnabled(), () => { AutoStart.Set(!AutoStart.IsEnabled()); RefreshSettings(); });
            m.Items.Add(new WF.ToolStripSeparator());
            add("时间换算", converting, () => { ShowFront(); if (S.Mini) { S.Mini = false; ApplyAll(true); } SetConverting(!converting); });
            add("更换主题…", false, () => OpenSettings(1));
            add("设置…", false, () => OpenSettings(-1));
            m.Items.Add(new WF.ToolStripSeparator());
            add("退出", false, ExitApp);
        }

        class DarkColors : WF.ProfessionalColorTable
        {
            static readonly SD.Color Bg = SD.Color.FromArgb(28, 33, 50), Hi = SD.Color.FromArgb(52, 60, 92), Bd = SD.Color.FromArgb(60, 66, 90);
            public override SD.Color ToolStripDropDownBackground { get { return Bg; } }
            public override SD.Color MenuBorder { get { return Bd; } }
            public override SD.Color MenuItemBorder { get { return Hi; } }
            public override SD.Color MenuItemSelected { get { return Hi; } }
            public override SD.Color MenuItemSelectedGradientBegin { get { return Hi; } }
            public override SD.Color MenuItemSelectedGradientEnd { get { return Hi; } }
            public override SD.Color ImageMarginGradientBegin { get { return Bg; } }
            public override SD.Color ImageMarginGradientMiddle { get { return Bg; } }
            public override SD.Color ImageMarginGradientEnd { get { return Bg; } }
            public override SD.Color SeparatorDark { get { return Bd; } }
            public override SD.Color SeparatorLight { get { return Bg; } }
            public override SD.Color CheckBackground { get { return SD.Color.FromArgb(80, 92, 160); } }
            public override SD.Color CheckSelectedBackground { get { return SD.Color.FromArgb(96, 110, 190); } }
            public override SD.Color CheckPressedBackground { get { return SD.Color.FromArgb(96, 110, 190); } }
        }

        public void ShowFront()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            ApplyLayer();
        }

        void ToggleVisible() { if (IsVisible) HideToTray(); else ShowFront(); }

        void HideToTray()
        {
            Hide();
            if (!hiddenTipShown && tray != null)
            {
                hiddenTipShown = true;
                tray.ShowBalloonTip(2500, "世界时钟", "已隐藏到托盘，单击托盘图标或按 " + S.HotShow + " 重新显示。", WF.ToolTipIcon.Info);
            }
        }

        public void ShowWelcome()
        {
            if (tray != null)
                tray.ShowBalloonTip(4000, "世界时钟已启动", "已设置为开机自动启动。右键时钟或托盘图标可以添加城市、换主题。", WF.ToolTipIcon.Info);
        }

        public void ExitApp()
        {
            exiting = true;
            S.Save();
            if (!S.NoSave) T.Cur.SaveCurrent();
            if (hwnd != IntPtr.Zero) for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(hwnd, i);
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            Application.Current.Shutdown();
        }
    }
}

// 入口、程序图标、截图自检模式
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IO = System.IO;
using WF = System.Windows.Forms;
using SD = System.Drawing;
using SD2 = System.Drawing.Drawing2D;

[assembly: System.Reflection.AssemblyTitle("世界时钟")]
[assembly: System.Reflection.AssemblyDescription("多时区桌面时钟")]
[assembly: System.Reflection.AssemblyProduct("世界时钟")]
[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.0.0")]

namespace WorldClock
{
    public static class AppIcon
    {
        public static SD.Bitmap Draw(int size)
        {
            var bmp = new SD.Bitmap(size, size, SD.Imaging.PixelFormat.Format32bppArgb);
            using (var g = SD.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SD2.SmoothingMode.AntiAlias;
                g.Clear(SD.Color.Transparent);
                float s = size, pad = Math.Max(0.5f, s * 0.03f);
                var r = new SD.RectangleF(pad, pad, s - 2 * pad, s - 2 * pad);
                using (var br = new SD2.LinearGradientBrush(r, SD.Color.FromArgb(255, 108, 120, 255), SD.Color.FromArgb(255, 176, 104, 247), 45f))
                    g.FillEllipse(br, r);
                float cx = s / 2, cy = s / 2;
                if (size >= 24)
                {
                    using (var pen = new SD.Pen(SD.Color.FromArgb(55, 255, 255, 255), Math.Max(1f, s * 0.022f)))
                    {
                        g.DrawEllipse(pen, cx - s * 0.2f, pad + s * 0.03f, s * 0.4f, s - 2 * pad - s * 0.06f);
                        g.DrawLine(pen, pad + s * 0.05f, cy, s - pad - s * 0.05f, cy);
                    }
                    using (var pen = new SD.Pen(SD.Color.FromArgb(230, 255, 255, 255), Math.Max(1.2f, s * 0.045f)))
                    {
                        pen.StartCap = SD2.LineCap.Round; pen.EndCap = SD2.LineCap.Round;
                        float r1 = s * 0.30f, r2 = s * 0.38f;
                        for (int k = 0; k < 4; k++)
                        {
                            double ang = k * Math.PI / 2;
                            g.DrawLine(pen, cx + (float)Math.Sin(ang) * r1, cy - (float)Math.Cos(ang) * r1, cx + (float)Math.Sin(ang) * r2, cy - (float)Math.Cos(ang) * r2);
                        }
                    }
                }
                using (var pen = new SD.Pen(SD.Color.White, Math.Max(1.4f, s * 0.075f)))
                {
                    pen.StartCap = SD2.LineCap.Round; pen.EndCap = SD2.LineCap.Round;
                    g.DrawLine(pen, cx, cy, cx, cy - s * 0.25f);
                    g.DrawLine(pen, cx, cy, cx + s * 0.155f, cy + s * 0.09f);
                }
                float d = Math.Max(2f, s * 0.12f);
                g.FillEllipse(SD.Brushes.White, cx - d / 2, cy - d / 2, d, d);
            }
            return bmp;
        }

        static readonly Dictionary<int, ImageSource> wpfCache = new Dictionary<int, ImageSource>();
        public static ImageSource Wpf(int size)
        {
            ImageSource img;
            if (wpfCache.TryGetValue(size, out img)) return img;
            using (var bmp = Draw(size))
            using (var ms = new IO.MemoryStream())
            {
                bmp.Save(ms, SD.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                wpfCache[size] = bi;
                return bi;
            }
        }

        public static SD.Icon Tray()
        {
            int sz = Math.Max(16, WF.SystemInformation.SmallIconSize.Width);
            var bmp = Draw(sz);
            return SD.Icon.FromHandle(bmp.GetHicon());
        }

        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var datas = new List<byte[]>();
            foreach (int sz in sizes)
                using (var b = Draw(sz)) using (var ms = new IO.MemoryStream())
                {
                    if (sz >= 128) b.Save(ms, SD.Imaging.ImageFormat.Png);
                    else WriteDib(b, ms);
                    datas.Add(ms.ToArray());
                }
            using (var fs = IO.File.Create(path))
            using (var w = new IO.BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(datas[i].Length); w.Write(offset);
                    offset += datas[i].Length;
                }
                foreach (var p in datas) w.Write(p);
            }
        }

        static void WriteDib(SD.Bitmap b, IO.Stream s)
        {
            int n = b.Width;
            var w = new IO.BinaryWriter(s);
            w.Write(40); w.Write(n); w.Write(n * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = n - 1; y >= 0; y--)
                for (int x = 0; x < n; x++)
                {
                    var c = b.GetPixel(x, y);
                    w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                }
            w.Write(new byte[((n + 31) / 32) * 4 * n]);
            w.Flush();
        }
    }

    public static class Program
    {
        static Mutex mutex;

        [STAThread]
        public static int Main(string[] args)
        {
            var argl = args.ToList();
            int mi = argl.IndexOf("--make-icon");
            if (mi >= 0 && mi + 1 < argl.Count) { AppIcon.WriteIco(argl[mi + 1]); return 0; }

            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }

            int si = argl.IndexOf("--shot");
            string shot = si >= 0 && si + 1 < argl.Count ? argl[si + 1] : null;

            if (shot == null)
            {
                bool created;
                mutex = new Mutex(true, "WorldDesktopClock_SingleInstance", out created);
                var showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "WorldDesktopClock_ShowEvent");
                if (!created) { showEvt.Set(); return 0; }
                var app0 = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Holidays.Load(true);
                T.SetTheme(Theme.LoadCurrent());
                return Run(app0, AppSettings.Load(), null, argl, showEvt);
            }

            // —— 截图自检模式：不读写配置、不改注册表、不联网 ——
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Holidays.Load(false);
            Weather.Offline = true;
            int ti = argl.IndexOf("--theme");
            Theme th = ti >= 0 && ti + 1 < argl.Count ? Theme.BuiltIns.FirstOrDefault(x => x.Name == argl[ti + 1]) : null;
            T.SetTheme((th ?? Theme.BuiltIns[0]).Clone());
            var s = AppSettings.Defaults();
            s.NoSave = true;
            if (argl.Contains("--demo"))
            {
                s.Cities.Add(Presets.All.First(p => p.Name == "纽约").Clone());
                s.Cities.Add(Presets.All.First(p => p.Name == "伦敦").Clone());
            }
            Weather.Seed(s.Cities[0], 12, 1);
            Weather.Seed(s.Cities[1], 24, 0);
            if (s.Cities.Count > 2) { Weather.Seed(s.Cities[2], 6, 61); Weather.Seed(s.Cities[3], 9, 3); }
            if (argl.Contains("--reminder"))
            {
                var c = s.Cities[1];
                s.Reminders.Add(new Reminder { Title = "智利黑五促销开始", TzId = c.TzId, City = c.Name, Country = c.Country, Repeat = "once", Date = DateTime.UtcNow.AddDays(3).Date, Hour = 0, Minute = 0, Countdown = true });
                s.Reminders.Add(new Reminder { Title = "圣地亚哥上班了，可以联系了", TzId = c.TzId, City = c.Name, Country = c.Country, Repeat = "workdays", Hour = 9, Minute = 0, Countdown = true });
            }
            if (argl.Contains("--horizontal")) s.Horizontal = true;
            if (argl.Contains("--mini")) s.Mini = true;
            if (argl.Contains("--12h")) s.Use24h = false;
            if (argl.Contains("--resizable")) s.Resizable = true;
            return Run(app, s, shot, argl, null);
        }

        static int Run(Application app, AppSettings s, string shot, List<string> argl, EventWaitHandle showEvt)
        {
            app.DispatcherUnhandledException += (o, e) =>
            {
                try { IO.Directory.CreateDirectory(AppSettings.Dir); IO.File.AppendAllText(IO.Path.Combine(AppSettings.Dir, "error.log"), DateTime.Now + "  " + e.Exception + Environment.NewLine); } catch { }
                e.Handled = true;
            };

            var w = new MainWindow(s, shot);

            if (shot == null)
            {
                if (s.IsNew) { AutoStart.Set(true); s.Save(); }
                else { AutoStart.Refresh(); s.Save(); }   // 保存一次，把旧版配置升级为新格式
                var th = new Thread(() =>
                {
                    while (true)
                    {
                        showEvt.WaitOne();
                        app.Dispatcher.BeginInvoke(new Action(w.ShowFront));
                    }
                }) { IsBackground = true };
                th.Start();
                w.Show();
                if (s.IsNew) w.ShowWelcome();
                return app.Run();
            }

            w.Show();
            int ci = argl.IndexOf("--convert");
            if (ci >= 0 && ci + 1 < argl.Count) w.SetConvHours(double.Parse(argl[ci + 1], CultureInfo.InvariantCulture));
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                FrameworkElement target = (FrameworkElement)w.Content;
                int tab = argl.IndexOf("--settings");
                if (tab >= 0)
                {
                    int t = tab + 1 < argl.Count && argl[tab + 1].Length == 1 ? int.Parse(argl[tab + 1]) : 0;
                    var sw = w.CreateSettingsForShot(t);
                    sw.Left = -20000; sw.Top = -20000; sw.ShowActivated = false; sw.WindowStartupLocation = WindowStartupLocation.Manual;
                    sw.Show();
                    sw.UpdateLayout();
                    target = sw.Root;
                }
                else if (argl.Contains("--planner"))
                {
                    var pw = w.CreatePlannerForShot();
                    pw.Left = -20000; pw.Top = -20000; pw.ShowActivated = false; pw.WindowStartupLocation = WindowStartupLocation.Manual;
                    pw.Show();
                    target = pw.Root;
                }
                else if (argl.Contains("--notify"))
                {
                    NotifyWindow.Popup("圣地亚哥上班了，可以联系了", "圣地亚哥 09:00（北京 20:00）", () => { });
                }
                else target = (FrameworkElement)((System.Windows.Controls.Grid)w.Content).Children[0];
                app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    int sc = argl.IndexOf("--scroll");
                    var swin = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                    if (sc >= 0 && swin != null)
                    {
                        swin.Sv.ScrollToVerticalOffset(double.Parse(argl[sc + 1], CultureInfo.InvariantCulture));
                        swin.UpdateLayout();
                        target = swin.Root;
                    }
                    if (argl.Contains("--notify"))
                    {
                        var nw = Application.Current.Windows.OfType<NotifyWindow>().FirstOrDefault();
                        if (nw != null) { nw.BeginAnimation(UIElement.OpacityProperty, null); nw.Opacity = 1; nw.UpdateLayout(); target = (FrameworkElement)nw.Content; }
                    }
                    Snapshot(target, shot);
                    app.Shutdown();
                }));
            }));
            return app.Run();
        }

        static void Snapshot(FrameworkElement el, string path)
        {
            double w = el.ActualWidth + 32, h = el.ActualHeight + 32;
            const double scale = 2;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(T.C("#3B4A6B"), T.C("#6F5B7E"), 30), null, new Rect(0, 0, w, h));
                var parent = VisualTreeHelper.GetParent(el) as Visual;
                dc.DrawRectangle(new VisualBrush(parent ?? el) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, w, h));
            }
            var rtb = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = IO.File.Create(path)) enc.Save(fs);
        }
    }
}

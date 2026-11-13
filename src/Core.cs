// 数据层：城市、设置、提醒、节假日、天气、时间工具
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using IO = System.IO;

namespace WorldClock
{
    public class CityInfo
    {
        public string Name, Sub, TzId, Alias, Country;
        public double Lat = double.NaN, Lon = double.NaN;
        public bool IsBase;

        public CityInfo(string name, string sub, string tz) : this(name, sub, tz, "", "", double.NaN, double.NaN) { }
        public CityInfo(string name, string sub, string tz, string alias, string country, double lat, double lon)
        {
            Name = name; Sub = sub ?? ""; TzId = tz; Alias = alias ?? ""; Country = country ?? ""; Lat = lat; Lon = lon;
        }
        public CityInfo Clone() { var c = (CityInfo)MemberwiseClone(); c.IsBase = false; return c; }
        public bool HasGeo { get { return !double.IsNaN(Lat) && !double.IsNaN(Lon); } }
    }

    public static class Presets
    {
        static CityInfo P(string n, string s, string tz, string alias, string cc, double lat, double lon)
        { return new CityInfo(n, s, tz, alias, cc, lat, lon); }

        public static readonly CityInfo[] All = {
            P("北京", "中国", "China Standard Time", "Beijing China", "CN", 39.90, 116.41),
            P("圣地亚哥", "智利", "Pacific SA Standard Time", "Santiago Chile", "CL", -33.45, -70.67),
            P("上海", "中国", "China Standard Time", "Shanghai", "CN", 31.23, 121.47),
            P("香港", "中国香港", "China Standard Time", "Hong Kong", "HK", 22.32, 114.17),
            P("台北", "中国台湾", "Taipei Standard Time", "Taipei", "TW", 25.03, 121.57),
            P("东京", "日本", "Tokyo Standard Time", "Tokyo Japan", "JP", 35.68, 139.69),
            P("首尔", "韩国", "Korea Standard Time", "Seoul Korea", "KR", 37.57, 126.98),
            P("新加坡", "新加坡", "Singapore Standard Time", "Singapore", "SG", 1.35, 103.82),
            P("曼谷", "泰国", "SE Asia Standard Time", "Bangkok Thailand", "TH", 13.76, 100.50),
            P("胡志明市", "越南", "SE Asia Standard Time", "Ho Chi Minh Vietnam", "VN", 10.82, 106.63),
            P("雅加达", "印度尼西亚", "SE Asia Standard Time", "Jakarta Indonesia", "ID", -6.21, 106.85),
            P("新德里", "印度", "India Standard Time", "New Delhi India", "IN", 28.61, 77.21),
            P("迪拜", "阿联酋", "Arabian Standard Time", "Dubai UAE", "AE", 25.20, 55.27),
            P("莫斯科", "俄罗斯", "Russian Standard Time", "Moscow Russia", "RU", 55.76, 37.62),
            P("伊斯坦布尔", "土耳其", "Turkey Standard Time", "Istanbul Turkey", "TR", 41.01, 28.98),
            P("开罗", "埃及", "Egypt Standard Time", "Cairo Egypt", "EG", 30.04, 31.24),
            P("约翰内斯堡", "南非", "South Africa Standard Time", "Johannesburg South Africa", "ZA", -26.20, 28.05),
            P("伦敦", "英国", "GMT Standard Time", "London UK England", "GB", 51.51, -0.13),
            P("巴黎", "法国", "Romance Standard Time", "Paris France", "FR", 48.86, 2.35),
            P("柏林", "德国", "W. Europe Standard Time", "Berlin Germany", "DE", 52.52, 13.40),
            P("马德里", "西班牙", "Romance Standard Time", "Madrid Spain", "ES", 40.42, -3.70),
            P("罗马", "意大利", "W. Europe Standard Time", "Rome Italy", "IT", 41.90, 12.50),
            P("纽约", "美国", "Eastern Standard Time", "New York USA", "US", 40.71, -74.01),
            P("芝加哥", "美国", "Central Standard Time", "Chicago USA", "US", 41.88, -87.63),
            P("洛杉矶", "美国", "Pacific Standard Time", "Los Angeles USA", "US", 34.05, -118.24),
            P("多伦多", "加拿大", "Eastern Standard Time", "Toronto Canada", "CA", 43.65, -79.38),
            P("温哥华", "加拿大", "Pacific Standard Time", "Vancouver Canada", "CA", 49.28, -123.12),
            P("墨西哥城", "墨西哥", "Central Standard Time (Mexico)", "Mexico City", "MX", 19.43, -99.13),
            P("圣保罗", "巴西", "E. South America Standard Time", "Sao Paulo Brazil", "BR", -23.55, -46.63),
            P("布宜诺斯艾利斯", "阿根廷", "Argentina Standard Time", "Buenos Aires Argentina", "AR", -34.60, -58.38),
            P("利马", "秘鲁", "SA Pacific Standard Time", "Lima Peru", "PE", -12.05, -77.04),
            P("波哥大", "哥伦比亚", "SA Pacific Standard Time", "Bogota Colombia", "CO", 4.71, -74.07),
            P("悉尼", "澳大利亚", "AUS Eastern Standard Time", "Sydney Australia", "AU", -33.87, 151.21),
            P("奥克兰", "新西兰", "New Zealand Standard Time", "Auckland New Zealand", "NZ", -36.85, 174.76),
            P("檀香山", "美国夏威夷", "Hawaiian Standard Time", "Honolulu Hawaii", "US", 21.31, -157.86),
            P("UTC", "协调世界时", "UTC", "UTC GMT", "", double.NaN, double.NaN),
        };
    }

    // ───────────────────────────── 提醒 / 倒计时 ─────────────────────────────
    public class Reminder
    {
        public string Title = "", TzId = "", City = "", Country = "";
        public int Hour = 9, Minute = 0, Lead = 0;
        public string Repeat = "workdays";   // workdays | daily | once
        public DateTime Date = DateTime.Today;
        public bool Enabled = true, Countdown = false;
        public long LastFired;

        public TimeZoneInfo Tz { get { return TimeUtil.Find(TzId) ?? TimeZoneInfo.Local; } }

        bool ToUtc(DateTime day, TimeZoneInfo tz, out DateTime u)
        {
            u = DateTime.MinValue;
            var local = new DateTime(day.Year, day.Month, day.Day, Hour, Minute, 0, DateTimeKind.Unspecified);
            if (tz.IsInvalidTime(local)) return false;
            u = TimeZoneInfo.ConvertTimeToUtc(local, tz);
            return true;
        }

        // 下一次（>= fromUtc）发生的 UTC 时间
        public DateTime? NextUtc(DateTime fromUtc)
        {
            var tz = Tz;
            DateTime u;
            if (Repeat == "once")
            {
                if (!ToUtc(Date, tz, out u)) return null;
                return u >= fromUtc ? (DateTime?)u : null;
            }
            var cityNow = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, tz);
            for (int d = 0; d < 20; d++)
            {
                var day = cityNow.Date.AddDays(d);
                if (Repeat == "workdays" && (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday || Holidays.Get(Country, day) != null)) continue;
                if (ToUtc(day, tz, out u) && u >= fromUtc) return u;
            }
            return null;
        }

        public string TimeText { get { return Hour.ToString("00") + ":" + Minute.ToString("00"); } }
        public string RepeatText { get { return Repeat == "once" ? Date.Month + "月" + Date.Day + "日" : Repeat == "daily" ? "每天" : "工作日"; } }
        public string Describe() { return RepeatText + " · " + City + " " + TimeText + (Lead > 0 ? " · 提前 " + Lead + " 分钟" : ""); }

        public string Serialize()
        {
            return string.Join("|", new[] { TimeUtil.Clean(Title), TzId, TimeUtil.Clean(City), Country, TimeText, Repeat,
                Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Lead.ToString(), Enabled ? "1" : "0", Countdown ? "1" : "0", LastFired.ToString() });
        }

        public static Reminder Parse(string s)
        {
            var p = s.Split('|');
            if (p.Length < 11) return null;
            try
            {
                var r = new Reminder { Title = p[0], TzId = p[1], City = p[2], Country = p[3], Repeat = p[5] };
                var hm = p[4].Split(':');
                r.Hour = int.Parse(hm[0]); r.Minute = int.Parse(hm[1]);
                r.Date = DateTime.ParseExact(p[6], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                r.Lead = int.Parse(p[7]); r.Enabled = p[8] == "1"; r.Countdown = p[9] == "1"; r.LastFired = long.Parse(p[10]);
                return r;
            }
            catch { return null; }
        }
    }

    // ───────────────────────────── 设置 ─────────────────────────────
    public class AppSettings
    {
        public List<CityInfo> Cities = new List<CityInfo>();
        public List<Reminder> Reminders = new List<Reminder>();
        public double Left = double.NaN, Top = double.NaN;
        public bool Use24h = true, ShowSeconds = true, Horizontal = false, ShowTimeline = true, Chime = false;
        public bool Locked = false, ClickThrough = false, Resizable = false, Mini = false, ShowWeather = true, ShowHolidays = true;
        public string Layer = "normal";   // top | normal | bottom
        public double Opacity = 1.0, Scale = 1.0;
        public string HotShow = "Ctrl+Alt+T", HotMini = "Ctrl+Alt+M", HotConv = "Ctrl+Alt+C";
        public bool IsNew, NoSave;

        public static string Dir { get { return IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorldClock"); } }
        public static string FilePath { get { return IO.Path.Combine(Dir, "settings.ini"); } }
        public static string ThemesDir { get { return IO.Path.Combine(Dir, "themes"); } }
        public static string CurrentThemePath { get { return IO.Path.Combine(Dir, "current.theme"); } }

        public CityInfo BaseCity { get { return Cities.FirstOrDefault(c => c.IsBase); } }

        public static AppSettings Defaults()
        {
            var s = new AppSettings();
            var bj = Presets.All[0].Clone(); bj.IsBase = true;
            s.Cities.Add(bj);
            s.Cities.Add(Presets.All[1].Clone());
            return s;
        }

        public static AppSettings Load()
        {
            if (!IO.File.Exists(FilePath)) { var d = Defaults(); d.IsNew = true; return d; }
            var s = new AppSettings();
            bool legacy = false;
            try
            {
                foreach (var raw in IO.File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = raw.IndexOf('=');
                    if (i <= 0 || raw.StartsWith("#")) continue;
                    string k = raw.Substring(0, i).Trim(), v = raw.Substring(i + 1);
                    bool b = v.Trim() == "1";
                    switch (k)
                    {
                        case "City":
                            {
                                var p = v.Split('|');
                                if (p.Length < 3) break;
                                var c = new CityInfo(p[0], p[1], p[2]);
                                if (p.Length >= 7)
                                {
                                    c.Country = p[3]; c.Lat = Dbl(p[4], double.NaN); c.Lon = Dbl(p[5], double.NaN); c.IsBase = p[6] == "1";
                                }
                                else
                                {
                                    legacy = true;
                                    var pre = Presets.All.FirstOrDefault(x => x.TzId == c.TzId && x.Name == c.Name);
                                    if (pre != null) { c.Country = pre.Country; c.Lat = pre.Lat; c.Lon = pre.Lon; }
                                }
                                s.Cities.Add(c);
                                break;
                            }
                        case "Reminder": { var r = Reminder.Parse(v); if (r != null) s.Reminders.Add(r); break; }
                        case "Left": s.Left = Dbl(v, double.NaN); break;
                        case "Top": s.Top = Dbl(v, double.NaN); break;
                        case "Use24h": s.Use24h = b; break;
                        case "ShowSeconds": s.ShowSeconds = b; break;
                        case "Horizontal": s.Horizontal = b; break;
                        case "ShowTimeline": s.ShowTimeline = b; break;
                        case "Chime": s.Chime = b; break;
                        case "Locked": s.Locked = b; break;
                        case "ClickThrough": s.ClickThrough = b; break;
                        case "Resizable": s.Resizable = b; break;
                        case "Mini": s.Mini = b; break;
                        case "ShowWeather": s.ShowWeather = b; break;
                        case "ShowHolidays": s.ShowHolidays = b; break;
                        case "Layer": s.Layer = v.Trim(); break;
                        case "Opacity": s.Opacity = Dbl(v, 1); break;
                        case "Scale": s.Scale = Dbl(v, 1); break;
                        case "HotShow": s.HotShow = v.Trim(); break;
                        case "HotMini": s.HotMini = v.Trim(); break;
                        case "HotConv": s.HotConv = v.Trim(); break;
                    }
                }
            }
            catch { return Defaults(); }
            // 旧版配置升级：把第一个城市设为基准
            if (legacy && s.BaseCity == null && s.Cities.Count > 0) s.Cities[0].IsBase = true;
            if (s.Layer != "top" && s.Layer != "normal" && s.Layer != "bottom") s.Layer = "normal";
            s.Opacity = Math.Max(0.3, Math.Min(1, s.Opacity));
            s.Scale = Math.Max(0.5, Math.Min(3, s.Scale));
            return s;
        }

        static double Dbl(string v, double def)
        {
            double r;
            return double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : def;
        }
        static string B(bool b) { return b ? "1" : "0"; }
        static string F(double d) { return d.ToString("R", CultureInfo.InvariantCulture); }

        public void Save()
        {
            if (NoSave) return;
            try
            {
                IO.Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("# 世界时钟 配置文件");
                sb.AppendLine("# City=名称|副标题|Windows时区ID|国家代码|纬度|经度|是否基准");
                sb.AppendLine("Left=" + F(Left));
                sb.AppendLine("Top=" + F(Top));
                sb.AppendLine("Use24h=" + B(Use24h));
                sb.AppendLine("ShowSeconds=" + B(ShowSeconds));
                sb.AppendLine("Horizontal=" + B(Horizontal));
                sb.AppendLine("ShowTimeline=" + B(ShowTimeline));
                sb.AppendLine("Chime=" + B(Chime));
                sb.AppendLine("Locked=" + B(Locked));
                sb.AppendLine("ClickThrough=" + B(ClickThrough));
                sb.AppendLine("Resizable=" + B(Resizable));
                sb.AppendLine("Mini=" + B(Mini));
                sb.AppendLine("ShowWeather=" + B(ShowWeather));
                sb.AppendLine("ShowHolidays=" + B(ShowHolidays));
                sb.AppendLine("Layer=" + Layer);
                sb.AppendLine("Opacity=" + F(Opacity));
                sb.AppendLine("Scale=" + F(Scale));
                sb.AppendLine("HotShow=" + HotShow);
                sb.AppendLine("HotMini=" + HotMini);
                sb.AppendLine("HotConv=" + HotConv);
                foreach (var c in Cities)
                    sb.AppendLine("City=" + TimeUtil.Clean(c.Name) + "|" + TimeUtil.Clean(c.Sub) + "|" + c.TzId + "|" + TimeUtil.Clean(c.Country) + "|" + F(c.Lat) + "|" + F(c.Lon) + "|" + B(c.IsBase));
                foreach (var r in Reminders) sb.AppendLine("Reminder=" + r.Serialize());
                IO.File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    public static class AutoStart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "WorldDesktopClock";
        static string Cmd { get { return "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --autostart"; } }

        public static bool IsEnabled()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(Key)) { return k != null && k.GetValue(Name) != null; } }
            catch { return false; }
        }
        public static void Set(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (on) k.SetValue(Name, Cmd); else k.DeleteValue(Name, false);
                }
            }
            catch { }
        }
        public static void Refresh() { if (IsEnabled()) Set(true); }
    }

    // ───────────────────────────── 节假日 ─────────────────────────────
    public static class Holidays
    {
        static readonly Dictionary<string, Dictionary<DateTime, string>> map = new Dictionary<string, Dictionary<DateTime, string>>(StringComparer.OrdinalIgnoreCase);
        public static string FilePath { get { return IO.Path.Combine(AppSettings.Dir, "holidays.ini"); } }

        public static void Load(bool useFile)
        {
            map.Clear();
            string text = DefaultText;
            if (useFile)
            {
                try
                {
                    if (!IO.File.Exists(FilePath))
                    {
                        IO.Directory.CreateDirectory(AppSettings.Dir);
                        IO.File.WriteAllText(FilePath, DefaultText, new UTF8Encoding(true));
                    }
                    text = IO.File.ReadAllText(FilePath, Encoding.UTF8);
                }
                catch { }
            }
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var p = line.Split('|');
                if (p.Length < 3) continue;
                string cc = p[0].Trim(), name = p[2].Trim();
                var range = p[1].Split('~');
                DateTime a, b;
                if (!DateTime.TryParseExact(range[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out a)) continue;
                b = a;
                if (range.Length > 1 && !DateTime.TryParseExact(range[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out b)) b = a;
                if (b < a) b = a;
                Dictionary<DateTime, string> d;
                if (!map.TryGetValue(cc, out d)) { d = new Dictionary<DateTime, string>(); map[cc] = d; }
                for (var x = a; x <= b && (x - a).TotalDays < 60; x = x.AddDays(1)) d[x] = name;
            }
        }

        public static string Get(string cc, DateTime date)
        {
            if (string.IsNullOrEmpty(cc)) return null;
            Dictionary<DateTime, string> d; string n;
            return map.TryGetValue(cc, out d) && d.TryGetValue(date.Date, out n) ? n : null;
        }

        public static bool Next(string cc, DateTime today, int days, out DateTime when, out string name)
        {
            for (int i = 1; i <= days; i++)
            {
                var d = today.Date.AddDays(i);
                var n = Get(cc, d);
                if (n != null) { when = d; name = n; return true; }
            }
            when = DateTime.MinValue; name = null;
            return false;
        }

        const string DefaultText = @"# 世界时钟 · 节假日数据
# 格式：国家代码|日期|名称     日期可以是单日 2026-09-18，也可以是区间 2026-10-01~2026-10-07
# 国家代码要和「设置 → 城市」里每个城市的代码一致（CN 中国、CL 智利、US 美国……）
# 内置数据仅供参考，请以各国官方公布为准。可自由增删，保存后在「设置 → 关于」点「重新加载节假日」。

# —— 中国（2026 年按国务院办公厅安排；2027 年为预估，官方公布后请修改）——
CN|2026-01-01~2026-01-03|元旦
CN|2026-02-15~2026-02-23|春节
CN|2026-04-04~2026-04-06|清明节
CN|2026-05-01~2026-05-05|劳动节
CN|2026-06-19~2026-06-21|端午节
CN|2026-09-25~2026-09-27|中秋节
CN|2026-10-01~2026-10-07|国庆节
CN|2027-01-01~2027-01-03|元旦
CN|2027-02-05~2027-02-12|春节（预估）
CN|2027-04-03~2027-04-05|清明节（预估）
CN|2027-05-01~2027-05-05|劳动节（预估）
CN|2027-06-09|端午节（预估）
CN|2027-09-15|中秋节（预估）
CN|2027-10-01~2027-10-07|国庆节（预估）

# —— 智利 ——
CL|2026-01-01|新年
CL|2026-04-03|耶稣受难日
CL|2026-04-04|圣周六
CL|2026-05-01|劳动节
CL|2026-05-21|海军节
CL|2026-06-21|原住民日
CL|2026-06-29|圣彼得与圣保罗节
CL|2026-07-16|卡门圣母节
CL|2026-08-15|圣母升天节
CL|2026-09-18|独立日（国庆）
CL|2026-09-19|陆军节
CL|2026-10-12|两个世界相遇日
CL|2026-10-31|福音派教会日
CL|2026-11-01|诸圣节
CL|2026-12-08|圣母无原罪节
CL|2026-12-25|圣诞节
CL|2027-01-01|新年
CL|2027-03-26|耶稣受难日
CL|2027-03-27|圣周六
CL|2027-05-01|劳动节
CL|2027-05-21|海军节
CL|2027-06-21|原住民日
CL|2027-06-28|圣彼得与圣保罗节
CL|2027-07-16|卡门圣母节
CL|2027-08-15|圣母升天节
CL|2027-09-18|独立日（国庆）
CL|2027-09-19|陆军节
CL|2027-10-11|两个世界相遇日
CL|2027-10-31|福音派教会日
CL|2027-11-01|诸圣节
CL|2027-12-08|圣母无原罪节
CL|2027-12-25|圣诞节

# —— 美国（联邦假日）——
US|2026-01-01|元旦
US|2026-01-19|马丁·路德·金纪念日
US|2026-02-16|总统日
US|2026-05-25|阵亡将士纪念日
US|2026-06-19|六月节
US|2026-07-03|独立日（补休）
US|2026-09-07|劳动节
US|2026-10-12|哥伦布日
US|2026-11-11|退伍军人节
US|2026-11-26|感恩节
US|2026-12-25|圣诞节
US|2027-01-01|元旦
US|2027-01-18|马丁·路德·金纪念日
US|2027-02-15|总统日
US|2027-05-31|阵亡将士纪念日
US|2027-06-18|六月节（补休）
US|2027-07-05|独立日（补休）
US|2027-09-06|劳动节
US|2027-10-11|哥伦布日
US|2027-11-11|退伍军人节
US|2027-11-25|感恩节
US|2027-12-24|圣诞节（补休）
";
    }

    // ───────────────────────────── 天气（Open-Meteo，免费免密钥） ─────────────────────────────
    public static class Weather
    {
        public class Info { public double Temp = double.NaN; public int Code = -1; public DateTime Time; public bool Ok; }
        static readonly Dictionary<string, Info> cache = new Dictionary<string, Info>();
        static readonly HashSet<string> pending = new HashSet<string>();
        public static Action Updated;
        public static bool Offline;   // 截图模式下不联网

        static string Key(CityInfo c)
        {
            return c.Lat.ToString("0.00", CultureInfo.InvariantCulture) + "," + c.Lon.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static void Seed(CityInfo c, double temp, int code)
        {
            lock (cache) cache[Key(c)] = new Info { Temp = temp, Code = code, Time = DateTime.UtcNow, Ok = true };
        }

        public static Info Get(CityInfo c)
        {
            if (c == null || !c.HasGeo) return null;
            string k = Key(c);
            Info i;
            lock (cache)
            {
                cache.TryGetValue(k, out i);
                bool stale = i == null || (DateTime.UtcNow - i.Time).TotalMinutes > (i.Ok ? 30 : 10);
                if (stale && !Offline && !pending.Contains(k)) { pending.Add(k); Fetch(c, k); }
            }
            return i != null && i.Ok ? i : null;
        }

        static void Fetch(CityInfo c, string k)
        {
            var disp = Application.Current != null ? Application.Current.Dispatcher : null;
            double lat = c.Lat, lon = c.Lon;
            ThreadPool.QueueUserWorkItem(delegate
            {
                var info = new Info { Time = DateTime.UtcNow };
                try
                {
                    string url = string.Format(CultureInfo.InvariantCulture,
                        "https://api.open-meteo.com/v1/forecast?latitude={0:0.00}&longitude={1:0.00}&current=temperature_2m,weather_code", lat, lon);
                    using (var wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        wc.Headers[HttpRequestHeader.UserAgent] = "WorldClock/2.0";
                        string json = wc.DownloadString(url);
                        var cur = Regex.Match(json, "\"current\"\\s*:\\s*\\{([^}]*)\\}");
                        if (cur.Success)
                        {
                            var t = Regex.Match(cur.Groups[1].Value, "\"temperature_2m\"\\s*:\\s*(-?[0-9.]+)");
                            var w = Regex.Match(cur.Groups[1].Value, "\"weather_code\"\\s*:\\s*([0-9]+)");
                            if (t.Success && w.Success)
                            {
                                info.Temp = double.Parse(t.Groups[1].Value, CultureInfo.InvariantCulture);
                                info.Code = int.Parse(w.Groups[1].Value);
                                info.Ok = true;
                            }
                        }
                    }
                }
                catch { }
                lock (cache) { cache[k] = info; pending.Remove(k); }
                if (disp != null && info.Ok) disp.BeginInvoke(new Action(() => { if (Updated != null) Updated(); }));
            });
        }

        public static string Describe(int code)
        {
            if (code == 0) return "晴";
            if (code == 1) return "晴间多云";
            if (code == 2) return "多云";
            if (code == 3) return "阴";
            if (code == 45 || code == 48) return "雾";
            if (code >= 51 && code <= 57) return "毛毛雨";
            if (code == 61 || code == 80) return "小雨";
            if (code == 63 || code == 81) return "中雨";
            if (code == 65 || code == 82) return "大雨";
            if (code == 66 || code == 67) return "冻雨";
            if (code >= 71 && code <= 77) return "雪";
            if (code == 85 || code == 86) return "阵雪";
            if (code >= 95) return "雷雨";
            return "";
        }
    }

    // ───────────────────────────── 时间工具 ─────────────────────────────
    public static class TimeUtil
    {
        public static readonly string[] WD = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

        public static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace("\r", "").Replace("\n", ""); }

        public static string Offset(TimeSpan off)
        {
            string sign = off < TimeSpan.Zero ? "-" : "+";
            var a = off.Duration();
            return "UTC" + sign + (int)a.TotalHours + (a.Minutes > 0 ? ":" + a.Minutes.ToString("00") : "");
        }

        public static string Span(TimeSpan d)
        {
            var a = d.Duration();
            int h = (int)a.TotalHours, m = a.Minutes;
            string s = "";
            if (h > 0) s += h + " 小时";
            if (m > 0) s += (s.Length > 0 ? " " : "") + m + " 分";
            return s.Length == 0 ? "0 分" : s;
        }

        public static string Diff(TimeSpan d)
        {
            if (d == TimeSpan.Zero) return "与基准相同";
            return (d > TimeSpan.Zero ? "快 " : "慢 ") + Span(d);
        }

        public static string Remain(TimeSpan d)
        {
            if (d < TimeSpan.Zero) d = TimeSpan.Zero;
            if (d.TotalDays >= 1) return (int)d.TotalDays + "天 " + d.Hours.ToString("00") + ":" + d.Minutes.ToString("00") + ":" + d.Seconds.ToString("00");
            return d.Hours.ToString("00") + ":" + d.Minutes.ToString("00") + ":" + d.Seconds.ToString("00");
        }

        public static string DateText(DateTime t) { return t.Month + "月" + t.Day + "日 " + WD[(int)t.DayOfWeek]; }

        public static DateTime Floor30(DateTime utc)
        {
            long t = TimeSpan.FromMinutes(30).Ticks;
            return new DateTime(utc.Ticks - utc.Ticks % t, DateTimeKind.Utc);
        }

        static readonly Dictionary<string, TimeZoneInfo> tzCache = new Dictionary<string, TimeZoneInfo>();
        public static TimeZoneInfo Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            TimeZoneInfo z;
            if (tzCache.TryGetValue(id, out z)) return z;
            try { z = TimeZoneInfo.FindSystemTimeZoneById(id); } catch { z = null; }
            tzCache[id] = z;
            return z;
        }

        static List<TimeZoneInfo> zones;
        public static List<TimeZoneInfo> Zones
        {
            get { if (zones == null) { try { zones = TimeZoneInfo.GetSystemTimeZones().ToList(); } catch { zones = new List<TimeZoneInfo>(); } } return zones; }
        }

        public static string ZoneName(TimeZoneInfo z)
        {
            string d = z.DisplayName;
            int i = d.IndexOf(')');
            if (i >= 0) d = d.Substring(i + 1).Trim();
            int j = d.IndexOfAny(new[] { ',', '，', '、' });
            if (j > 0) d = d.Substring(0, j).Trim();
            return d.Length > 0 ? d : z.Id;
        }

        // 某地某时刻的状态评分：2 工作时间，1 早晚还醒着，0 休息
        public static int Score(DateTime local, string country, out string label)
        {
            double h = local.Hour + local.Minute / 60.0;
            bool off = local.DayOfWeek == DayOfWeek.Saturday || local.DayOfWeek == DayOfWeek.Sunday || Holidays.Get(country, local) != null;
            if (h >= 23 || h < 7) { label = "休息中"; return 0; }
            if (off) { label = Holidays.Get(country, local) != null ? "节假日" : "周末"; return (h >= 9 && h < 21) ? 1 : 0; }
            if (h >= 9 && h < 18) { label = "工作时间"; return 2; }
            label = "非工作时间";
            return (h >= 8 && h < 22) ? 1 : 0;
        }
    }

    // 一天中的天空配色
    public static class Sky
    {
        static readonly double[] H = { 0, 4.5, 6, 7.5, 10, 15.5, 17.5, 18.75, 20, 24 };
        static readonly string[] A = { "#1A2148", "#232A5E", "#E0895E", "#4C98DD", "#3A8BDD", "#3D82CF", "#EE8A5E", "#6B4C9E", "#262E62", "#1A2148" };
        static readonly string[] B = { "#0B0F26", "#2E2B60", "#7A4A90", "#2D69B5", "#1E57A8", "#285DA3", "#9A4E8A", "#2E2A66", "#121736", "#0B0F26" };

        public static void Get(double h, out Color a, out Color b)
        {
            h = ((h % 24) + 24) % 24;
            int i = 0;
            while (i < H.Length - 2 && h >= H[i + 1]) i++;
            double f = (h - H[i]) / (H[i + 1] - H[i]);
            a = T.Mix(T.C(A[i]), T.C(A[i + 1]), f);
            b = T.Mix(T.C(B[i]), T.C(B[i + 1]), f);
        }
        public static bool IsDay(double h) { return h >= 6.25 && h < 18.4; }
    }

    static class Native
    {
        public const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80;
        public const int WM_WINDOWPOSCHANGING = 0x0046, WM_HOTKEY = 0x0312;
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;
        public const uint GW_HWNDPREV = 3;
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly string[] TaskbarClasses = { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public bool Intersects(RECT other)
            {
                return Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
            }
        }
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint command);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT rect);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    }

    public static class HotkeyUtil
    {
        public static bool Parse(string s, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            foreach (var raw in s.Split('+'))
            {
                var part = raw.Trim();
                switch (part.ToLowerInvariant())
                {
                    case "ctrl": mods |= 2; break;
                    case "alt": mods |= 1; break;
                    case "shift": mods |= 4; break;
                    case "win": mods |= 8; break;
                    default:
                        Key k;
                        string name = part.Length == 1 && char.IsDigit(part[0]) ? "D" + part : part;
                        if (!Enum.TryParse(name, true, out k)) return false;
                        vk = (uint)KeyInterop.VirtualKeyFromKey(k);
                        break;
                }
            }
            return vk != 0 && mods != 0;
        }

        public static string Format(ModifierKeys m, Key k)
        {
            var parts = new List<string>();
            if ((m & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((m & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((m & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((m & ModifierKeys.Windows) != 0) parts.Add("Win");
            string ks = k.ToString();
            if (ks.Length == 2 && ks[0] == 'D' && char.IsDigit(ks[1])) ks = ks.Substring(1);
            parts.Add(ks);
            return string.Join("+", parts);
        }

        public static bool IsModifier(Key k)
        {
            return k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt || k == Key.LeftShift || k == Key.RightShift || k == Key.LWin || k == Key.RWin;
        }
    }
}

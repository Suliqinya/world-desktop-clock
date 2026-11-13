// 主题系统：所有颜色、字体、圆角、卡片样式都可自定义，也可以用「主题代码」直接编辑 / 导入导出
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Media;
using IO = System.IO;

namespace WorldClock
{
    public class Theme
    {
        public string Name = "未命名";
        public Color Text = Colors.White;
        public Color Accent = T.C("#7C8CFF"), Accent2 = T.C("#B07CFF"), Highlight = T.C("#FFD58A");
        public Color Panel = T.C("#1B2032"), Panel2 = T.C("#0E1019"), Card = Colors.White;
        public double PanelOpacity = 0.95, BorderOpacity = 0.16, Radius = 22;
        public double CardOpacity = 0.07, CardRadius = 16, SkyStrength = 0.5;
        public double BgImageOpacity = 0.35, TimeSize = 44, NameSize = 17;
        public bool Shadow = true, Glow = true;
        public string CardStyle = "sky";          // sky | soft | glass | accent | outline | solid
        public string FontUI = "Microsoft YaHei UI", FontNum = "Bahnschrift", TimeWeight = "Normal", BgImage = "";

        public static readonly string[] CardStyles = { "sky", "soft", "glass", "accent", "outline", "solid" };
        public static readonly string[] CardStyleNames = { "天空", "柔和", "玻璃", "渐变", "描边", "纯色" };
        public static readonly string[] Weights = { "Light", "Normal", "Medium", "SemiBold", "Bold" };
        public static readonly string[] WeightNames = { "细", "常规", "中等", "半粗", "粗" };

        public Theme Clone() { return (Theme)MemberwiseClone(); }

        static string D(double d) { return d.ToString("0.###", CultureInfo.InvariantCulture); }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 世界时钟主题。修改后点「应用代码」即可预览");
            sb.AppendLine("# 颜色：#RRGGBB 或 #AARRGGBB　　不透明度 / 强度：0 ~ 1");
            sb.AppendLine("# CardStyle：sky 天空 / soft 柔和 / glass 玻璃 / accent 渐变 / outline 描边 / solid 纯色");
            sb.AppendLine("# TimeWeight：Light / Normal / Medium / SemiBold / Bold");
            sb.AppendLine("Name=" + Name);
            sb.AppendLine("Text=" + T.Hex(Text));
            sb.AppendLine("Accent=" + T.Hex(Accent));
            sb.AppendLine("Accent2=" + T.Hex(Accent2));
            sb.AppendLine("Highlight=" + T.Hex(Highlight));
            sb.AppendLine("Panel=" + T.Hex(Panel));
            sb.AppendLine("Panel2=" + T.Hex(Panel2));
            sb.AppendLine("PanelOpacity=" + D(PanelOpacity));
            sb.AppendLine("BorderOpacity=" + D(BorderOpacity));
            sb.AppendLine("Radius=" + D(Radius));
            sb.AppendLine("Shadow=" + (Shadow ? 1 : 0));
            sb.AppendLine("BgImage=" + BgImage);
            sb.AppendLine("BgImageOpacity=" + D(BgImageOpacity));
            sb.AppendLine("CardStyle=" + CardStyle);
            sb.AppendLine("Card=" + T.Hex(Card));
            sb.AppendLine("CardOpacity=" + D(CardOpacity));
            sb.AppendLine("CardRadius=" + D(CardRadius));
            sb.AppendLine("SkyStrength=" + D(SkyStrength));
            sb.AppendLine("Glow=" + (Glow ? 1 : 0));
            sb.AppendLine("FontUI=" + FontUI);
            sb.AppendLine("FontNum=" + FontNum);
            sb.AppendLine("TimeSize=" + D(TimeSize));
            sb.AppendLine("TimeWeight=" + TimeWeight);
            sb.AppendLine("NameSize=" + D(NameSize));
            return sb.ToString();
        }

        // 返回无法识别的行数（用于提示）
        public static Theme Parse(string text, Theme baseline, out int bad)
        {
            var t = (baseline ?? new Theme()).Clone();
            bad = 0;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int i = line.IndexOf('=');
                if (i <= 0) { bad++; continue; }
                string k = line.Substring(0, i).Trim().ToLowerInvariant(), v = line.Substring(i + 1).Trim();
                Color c; double d;
                bool isNum = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                bool ok = true;
                switch (k)
                {
                    case "name": t.Name = v; break;
                    case "text": ok = T.TryC(v, out c); if (ok) t.Text = c; break;
                    case "accent": ok = T.TryC(v, out c); if (ok) t.Accent = c; break;
                    case "accent2": ok = T.TryC(v, out c); if (ok) t.Accent2 = c; break;
                    case "highlight": ok = T.TryC(v, out c); if (ok) t.Highlight = c; break;
                    case "panel": ok = T.TryC(v, out c); if (ok) t.Panel = c; break;
                    case "panel2": ok = T.TryC(v, out c); if (ok) t.Panel2 = c; break;
                    case "card": ok = T.TryC(v, out c); if (ok) t.Card = c; break;
                    case "panelopacity": ok = isNum; if (ok) t.PanelOpacity = Clamp(d, 0, 1); break;
                    case "borderopacity": ok = isNum; if (ok) t.BorderOpacity = Clamp(d, 0, 1); break;
                    case "radius": ok = isNum; if (ok) t.Radius = Clamp(d, 0, 60); break;
                    case "cardopacity": ok = isNum; if (ok) t.CardOpacity = Clamp(d, 0, 1); break;
                    case "cardradius": ok = isNum; if (ok) t.CardRadius = Clamp(d, 0, 60); break;
                    case "skystrength": ok = isNum; if (ok) t.SkyStrength = Clamp(d, 0, 1); break;
                    case "bgimageopacity": ok = isNum; if (ok) t.BgImageOpacity = Clamp(d, 0, 1); break;
                    case "timesize": ok = isNum; if (ok) t.TimeSize = Clamp(d, 16, 120); break;
                    case "namesize": ok = isNum; if (ok) t.NameSize = Clamp(d, 9, 40); break;
                    case "shadow": t.Shadow = v == "1" || v.ToLowerInvariant() == "true"; break;
                    case "glow": t.Glow = v == "1" || v.ToLowerInvariant() == "true"; break;
                    case "cardstyle": ok = CardStyles.Contains(v.ToLowerInvariant()); if (ok) t.CardStyle = v.ToLowerInvariant(); break;
                    case "fontui": t.FontUI = v; break;
                    case "fontnum": t.FontNum = v; break;
                    case "timeweight": ok = Weights.Any(w => w.Equals(v, StringComparison.OrdinalIgnoreCase)); if (ok) t.TimeWeight = Weights.First(w => w.Equals(v, StringComparison.OrdinalIgnoreCase)); break;
                    case "bgimage": t.BgImage = v; break;
                    default: ok = false; break;
                }
                if (!ok) bad++;
            }
            return t;
        }

        public static Theme Parse(string text) { int bad; return Parse(text, null, out bad); }

        static double Clamp(double v, double a, double b) { return Math.Max(a, Math.Min(b, v)); }

        // ── 内置主题 ──
        static readonly string[] BuiltInSrc = {
@"Name=深夜
CardStyle=sky",
@"Name=晨光
Text=#1E2433
Accent=#4F6BFF
Accent2=#8B5CF6
Highlight=#C2410C
Panel=#FBFCFF
Panel2=#E9EDF7
PanelOpacity=0.97
BorderOpacity=0.10
CardStyle=soft
SkyStrength=0.42
Card=#1E2433
CardOpacity=0.05",
@"Name=极光
Text=#E9FFF7
Accent=#2DD4BF
Accent2=#60A5FA
Highlight=#FDE68A
Panel=#0B2230
Panel2=#071A1C
PanelOpacity=0.93
CardStyle=glass
Card=#5EEAD4
CardOpacity=0.09",
@"Name=落日
Text=#FFFFFF
Accent=#F97316
Accent2=#DB2777
Highlight=#FFF1B8
Panel=#22151F
Panel2=#120B12
PanelOpacity=0.95
CardStyle=accent",
@"Name=纸张
Text=#3B3127
Accent=#C2410C
Accent2=#EA580C
Highlight=#B45309
Panel=#F7F1E6
Panel2=#EFE4D0
PanelOpacity=0.98
BorderOpacity=0.14
Radius=14
CardStyle=solid
Card=#FFFDF8
CardOpacity=0.85
CardRadius=10
Glow=0
FontNum=Georgia",
@"Name=霓虹
Text=#F5F3FF
Accent=#FF2BD6
Accent2=#00E5FF
Highlight=#00E5FF
Panel=#0A0912
Panel2=#140A22
PanelOpacity=0.95
Radius=8
CardStyle=outline
Card=#FF2BD6
CardOpacity=0.05
CardRadius=6
FontNum=Consolas",
@"Name=森林
Text=#ECFDF3
Accent=#84CC16
Accent2=#22C55E
Highlight=#FDE047
Panel=#13261C
Panel2=#0A1610
PanelOpacity=0.95
CardStyle=glass
Card=#BBF7D0
CardOpacity=0.07",
@"Name=樱花
Text=#4C0519
Accent=#E11D48
Accent2=#F472B6
Highlight=#BE123C
Panel=#FFF5F7
Panel2=#FFE1E8
PanelOpacity=0.97
BorderOpacity=0.12
CardStyle=soft
SkyStrength=0.3
Card=#FFFFFF
CardOpacity=0.6",
@"Name=极简
Text=#FFFFFF
Accent=#FFFFFF
Accent2=#9CA3AF
Highlight=#FFFFFF
Panel=#000000
Panel2=#0A0A0A
PanelOpacity=0.8
BorderOpacity=0.12
Radius=18
Shadow=0
CardStyle=outline
Card=#FFFFFF
CardOpacity=0
Glow=0
FontNum=Segoe UI Light
TimeWeight=Light",
        };

        static List<Theme> builtIns;
        public static List<Theme> BuiltIns
        {
            get
            {
                if (builtIns == null) builtIns = BuiltInSrc.Select(s => Parse(s)).ToList();
                return builtIns;
            }
        }

        public static List<Theme> UserThemes()
        {
            var list = new List<Theme>();
            try
            {
                if (IO.Directory.Exists(AppSettings.ThemesDir))
                    foreach (var f in IO.Directory.GetFiles(AppSettings.ThemesDir, "*.theme").OrderBy(x => x))
                    {
                        try
                        {
                            var t = Parse(IO.File.ReadAllText(f, Encoding.UTF8));
                            if (string.IsNullOrWhiteSpace(t.Name) || t.Name == "未命名") t.Name = IO.Path.GetFileNameWithoutExtension(f);
                            list.Add(t);
                        }
                        catch { }
                    }
            }
            catch { }
            return list;
        }

        public static string UserThemePath(string name)
        {
            var safe = new string(name.Select(ch => IO.Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
            return IO.Path.Combine(AppSettings.ThemesDir, safe + ".theme");
        }

        public static bool IsBuiltIn(string name) { return BuiltIns.Any(t => t.Name == name); }

        public static Theme LoadCurrent()
        {
            try
            {
                if (IO.File.Exists(AppSettings.CurrentThemePath))
                    return Parse(IO.File.ReadAllText(AppSettings.CurrentThemePath, Encoding.UTF8));
            }
            catch { }
            return BuiltIns[0].Clone();
        }

        public void SaveCurrent()
        {
            try
            {
                IO.Directory.CreateDirectory(AppSettings.Dir);
                IO.File.WriteAllText(AppSettings.CurrentThemePath, Serialize(), new UTF8Encoding(true));
            }
            catch { }
        }
    }
}

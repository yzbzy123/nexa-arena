using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace NexaArena
{
    public sealed class HotkeyOption
    {
        public string Action;
        public bool Enabled = true;
        public uint Modifiers = 3;
        public int Key;
        public HotkeyOption Copy() { return (HotkeyOption)MemberwiseClone(); }
        public override string ToString()
        {
            if (!Enabled) return "未启用";
            return ((Modifiers & 2) != 0 ? "Ctrl + " : "") + ((Modifiers & 1) != 0 ? "Alt + " : "") +
                ((Modifiers & 4) != 0 ? "Shift + " : "") + ((Keys)Key).ToString();
        }
    }

    public sealed class ToolboxSettings
    {
        public bool MinimizeToTray = true;
        public string SourceGame = "CS2", TargetGame = "VALORANT";
        public string SourceResolution = "1920x1080", TargetResolution = "1920x1080";
        public string SensitivityMethod = "screen-center";
        public decimal CustomCsPerValorant = 3.5m;
        public decimal SourceDpi = 800, TargetDpi = 800, Sensitivity = 1;
        [XmlIgnore]
        public List<HotkeyOption> Hotkeys = Defaults();
        [XmlArray("Hotkeys")]
        public HotkeyOption[] SerializedHotkeys
        {
            get { return Hotkeys.ToArray(); }
            set { Hotkeys = value == null ? Defaults() : value.ToList(); }
        }
        public static List<HotkeyOption> Defaults()
        {
            return new List<HotkeyOption> {
                new HotkeyOption { Action="switch", Key=(int)Keys.S },
                new HotkeyOption { Action="restore", Key=(int)Keys.H },
                new HotkeyOption { Action="window", Key=(int)Keys.N },
                new HotkeyOption { Action="microphone", Key=(int)Keys.M, Enabled=false }
            };
        }
        public ToolboxSettings Copy()
        {
            ToolboxSettings copy = (ToolboxSettings)MemberwiseClone();
            copy.Hotkeys = Hotkeys.Select(x => x.Copy()).ToList();
            return copy;
        }
        public static string FilePath { get { return Path.Combine(SavedState.DirectoryPath, "toolbox-settings.xml"); } }
        public static ToolboxSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new ToolboxSettings();
                using (var reader = System.Xml.XmlReader.Create(FilePath, new System.Xml.XmlReaderSettings {
                    DtdProcessing=System.Xml.DtdProcessing.Prohibit, XmlResolver=null }))
                {
                    ToolboxSettings settings = (ToolboxSettings)new XmlSerializer(typeof(ToolboxSettings)).Deserialize(reader);
                    Validate(settings);
                    return settings;
                }
            }
            catch (Exception ex) { ErrorLog.Write(ex); return new ToolboxSettings(); }
        }
        public static void Validate(ToolboxSettings settings)
        {
            if (settings.SourceGame != "CS2" && settings.SourceGame != "VALORANT") throw new ArgumentException("无效的来源游戏。");
            if (settings.TargetGame != "CS2" && settings.TargetGame != "VALORANT") throw new ArgumentException("无效的目标游戏。");
            SensitivityProjection.Resolve(settings.SourceResolution);
            SensitivityProjection.Resolve(settings.TargetResolution);
            SensitivityFormulas.Validate(settings.SensitivityMethod, (double)settings.CustomCsPerValorant);
            if (settings.SourceDpi < 50 || settings.SourceDpi > 100000 || settings.TargetDpi < 50 || settings.TargetDpi > 100000 ||
                settings.Sensitivity <= 0 || settings.Sensitivity > 100) throw new ArgumentException("DPI 或灵敏度超出范围。");
            HotkeyRules.Validate(settings.Hotkeys);
        }
        public void Save()
        {
            Validate(this);
            Directory.CreateDirectory(SavedState.DirectoryPath);
            string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temp)) new XmlSerializer(typeof(ToolboxSettings)).Serialize(stream, this);
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal static class HotkeyRules
    {
        public static readonly string[] Actions = { "switch", "restore", "window", "microphone" };
        public static readonly string[] Titles = { "切换 4:3", "恢复显示", "显示主窗口", "麦克风静音/取消" };
        public static bool IsSupportedKey(int key)
        {
            return key >= (int)Keys.A && key <= (int)Keys.Z || key >= (int)Keys.D0 && key <= (int)Keys.D9 ||
                key >= (int)Keys.F1 && key <= (int)Keys.F11;
        }
        public static void Validate(List<HotkeyOption> keys)
        {
            if (keys == null || keys.Count != Actions.Length || Actions.Any(a => keys.Count(k => k != null && k.Action == a) != 1))
                throw new ArgumentException("快捷键配置不完整。");
            HashSet<string> combos = new HashSet<string>();
            foreach (HotkeyOption key in keys)
            {
                if (!IsSupportedKey(key.Key) || (key.Modifiers & ~7U) != 0) throw new ArgumentException("不支持的快捷键。");
                if (!key.Enabled) continue;
                if ((key.Modifiers & 3) == 0) throw new ArgumentException("快捷键至少需要 Ctrl 或 Alt，避免影响游戏操作。");
                if (!combos.Add(key.Modifiers + ":" + key.Key)) throw new ArgumentException("两个功能不能使用同一组快捷键。");
                if (key.Modifiers == 1 && key.Key == (int)Keys.F4) throw new ArgumentException("Alt+F4 是关闭窗口快捷键，请换一组。");
            }
        }
    }

    internal static class SensitivityMath
    {
        public static string Format(double value) { return value.ToString("0.############",CultureInfo.InvariantCulture); }
        public static double Yaw(string game)
        {
            if (game == "CS2") return 0.022;
            if (game == "VALORANT") return 0.07;
            throw new ArgumentException("未知游戏。");
        }
        public static double Convert(string source, string target, double sensitivity, double sourceDpi, double targetDpi)
        {
            Positive(sensitivity); Positive(sourceDpi); Positive(targetDpi);
            return sensitivity * sourceDpi * Yaw(source) / (targetDpi * Yaw(target));
        }
        public static double Cm360(string game, double sensitivity, double dpi)
        {
            Positive(sensitivity); Positive(dpi);
            return 360.0 * 2.54 / (dpi * sensitivity * Yaw(game));
        }
        private static void Positive(double value)
        {
            if (value <= 0 || double.IsInfinity(value) || double.IsNaN(value)) throw new ArgumentException("请输入大于 0 的有效数值。");
        }
    }

    internal sealed class Cs2CommandOptions
    {
        public int FpsLimit = 300;
        public bool ShowFps, Practice, UnlimitedAmmo, BuyAnywhere, GrenadePreview;
        public string BuyKey = "F6";
        public string[] Items = new string[0];
    }

    internal static class Cs2Commands
    {
        public static readonly string[] ItemIds = { "ak47", "m4a1_silencer", "awp", "deagle", "vesthelm", "defuser", "smokegrenade", "flashbang", "hegrenade", "molotov", "incgrenade" };
        public static readonly string[] ItemNames = { "AK-47", "M4A1-S", "AWP", "沙漠之鹰", "头盔＋防弹衣", "拆弹器", "烟雾弹", "闪光弹", "高爆手雷", "燃烧瓶（T）", "燃烧弹（CT）" };
        public static string Generate(Cs2CommandOptions o)
        {
            if (o.FpsLimit < 0 || o.FpsLimit > 1000) throw new ArgumentException("FPS 上限应为 0–1000（0 为不限制）。");
            if (!Regex.IsMatch(o.BuyKey ?? "", @"^(?:[A-Za-z0-9]|F(?:[1-9]|1[01]))$")) throw new ArgumentException("买枪按键不合法。");
            if (o.Items == null || o.Items.Any(i => !ItemIds.Contains(i))) throw new ArgumentException("包含未知购买物品。");
            StringBuilder b = new StringBuilder();
            b.AppendLine("// Nexa Arena - CS2 commands. Review before execution.");
            b.AppendLine("// Save your original fps_max / cl_showfps and bind before applying.");
            b.AppendLine("fps_max " + o.FpsLimit.ToString(CultureInfo.InvariantCulture));
            b.AppendLine("cl_showfps " + (o.ShowFps ? "1" : "0"));
            if (o.Items.Length > 0)
            {
                b.AppendLine("// Buy requires money, buy zone/time and the item in your loadout.");
                b.AppendLine("// This replaces the selected key's binding; inspect it with: bind " + o.BuyKey.ToLowerInvariant());
                b.AppendLine("bind \"" + o.BuyKey.ToLowerInvariant() + "\" \"" + string.Join("; ", o.Items.Distinct().Select(i => "buy " + i)) + "\"");
            }
            if (o.Practice)
            {
                b.AppendLine();
                b.AppendLine("// LOCAL PRACTICE ONLY. Disconnect/reload the map to leave this setup.");
                b.AppendLine("sv_cheats 1");
                b.AppendLine("mp_freezetime 0");
                b.AppendLine("mp_roundtime_defuse 60");
                b.AppendLine("mp_roundtime_hostage 60");
                b.AppendLine("sv_infinite_ammo " + (o.UnlimitedAmmo ? "1" : "0"));
                b.AppendLine("mp_buy_anywhere " + (o.BuyAnywhere ? "1" : "0"));
                b.AppendLine("mp_buytime " + (o.BuyAnywhere ? "3600" : "20"));
                b.AppendLine("sv_grenade_trajectory_prac_pipreview " + (o.GrenadePreview ? "1" : "0"));
                b.AppendLine("sv_grenade_trajectory_prac_trailtime " + (o.GrenadePreview ? "8" : "0"));
                b.AppendLine("mp_restartgame 1");
            }
            return b.ToString();
        }
        public const string RestoreHelp = "不会自动执行或覆盖 autoexec。运行前可在控制台分别输入 fps_max、cl_showfps、bind 按键，记下原值。\r\n还原：把对应值/绑定改回；unbind 按键只会清空绑定，不会找回旧绑定。练习房参数在退出并重新载入正常模式后还原；不要将练习配置设为启动配置。\r\n购买受阵营、配装、金钱、购买区和购买时间限制；生成指令不代表服务器一定允许。";
    }
}

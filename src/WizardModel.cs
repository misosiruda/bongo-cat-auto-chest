using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.Win32;

namespace BongoAutoChest.Setup
{
    public sealed class ChestSettings
    {
        public bool Enabled { get; set; }
        public bool AutoOwn { get; set; }
        public bool AutoOthers { get; set; }
        public decimal MinDelaySeconds { get; set; }
        public decimal MaxDelaySeconds { get; set; }
        public ChestSettings() { Enabled = true; AutoOwn = true; AutoOthers = false; MinDelaySeconds = 3; MaxDelaySeconds = 8; }
        public string Validate()
        {
            if (Enabled && !AutoOwn && !AutoOthers) return "자동으로 열 상자를 하나 이상 선택하거나 자동 개봉을 꺼 주세요.";
            if (MinDelaySeconds < 1 || MaxDelaySeconds > 300 || MinDelaySeconds > MaxDelaySeconds)
                return "대기 시간은 1~300초이며, 최소 시간이 최대 시간보다 작거나 같아야 해요.";
            return null;
        }
        public static ChestSettings Read(string path)
        {
            var settings = new ChestSettings();
            if (!File.Exists(path)) return settings;
            foreach (string raw in File.ReadAllLines(path))
            {
                string[] pair = raw.Split(new[] { '=' },2);
                if (pair.Length != 2) continue;
                string key = pair[0].Trim(), value = pair[1].Trim();
                bool b; decimal n;
                if (key == "Enabled" && bool.TryParse(value,out b)) settings.Enabled = b;
                if (key == "AutoOwn" && bool.TryParse(value,out b)) settings.AutoOwn = b;
                if (key == "AutoOthers" && bool.TryParse(value,out b)) settings.AutoOthers = b;
                if (decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out n) && n >= 1 && n <= 300)
                {
                    if (key == "MinDelaySeconds") settings.MinDelaySeconds = n;
                    if (key == "MaxDelaySeconds") settings.MaxDelaySeconds = n;
                }
            }
            if (settings.MaxDelaySeconds < settings.MinDelaySeconds) settings.MaxDelaySeconds = settings.MinDelaySeconds;
            return settings;
        }
    }
    public static class GameLocation
    {
        public static bool IsGame(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path,"BongoCat.exe"))
                && File.Exists(Path.Combine(path,"BongoCat_Data","Managed","Assembly-CSharp.dll"));
        }
        public static IEnumerable<string> Libraries(string steam, string vdf)
        {
            yield return steam;
            foreach (Match m in Regex.Matches(vdf,"\"path\"\\s+\"([^\"]+)\""))
                yield return m.Groups[1].Value.Replace("\\\\","\\");
        }
        public static string Discover()
        {
            string steam = null;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                if (key != null) steam = key.GetValue("SteamPath") as string;
            if (string.IsNullOrEmpty(steam)) steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam");
            string vdf = Path.Combine(steam,"steamapps","libraryfolders.vdf");
            foreach (string library in Libraries(steam,File.Exists(vdf) ? File.ReadAllText(vdf) : "").Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string manifest = Path.Combine(library,"steamapps","appmanifest_3419430.acf");
                if (!File.Exists(manifest)) continue;
                var match = Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"");
                if (!match.Success) continue;
                string game = Path.Combine(library,"steamapps","common",match.Groups[1].Value);
                if (IsGame(game)) return Path.GetFullPath(game);
            }
            return "";
        }
        // Windows command-line quoting for ProcessStartInfo; no command is evaluated by a shell.
        public static string Quote(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\',slashes * 2 + 1); result.Append(c); }
                else { result.Append('\\',slashes); result.Append(c); }
                slashes = 0;
            }
            result.Append('\\',slashes * 2); result.Append('"'); return result.ToString();
        }
    }
}

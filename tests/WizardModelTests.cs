using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using BongoAutoChest.Setup;
class WizardModelTests
{
    static void Check(bool ok,string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main()
    {
        var settings = new ChestSettings();
        Check(settings.AutoOwn && !settings.AutoOthers && settings.Enabled,"new users open only own chests");
        settings.AutoOwn = false; Check(settings.Validate() != null,"active automation requires at least one target");
        settings.Enabled = false; Check(settings.Validate() == null,"paused automation permits no targets");
        settings.MinDelaySeconds = 9; settings.MaxDelaySeconds = 3; Check(settings.Validate() != null,"inverted delays rejected");
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path,"Enabled=false\nAutoOwn=false\nAutoOthers=true\nMinDelaySeconds=4.5\nMaxDelaySeconds=9\nUnknown=keep");
            settings = ChestSettings.Read(path);
            Check(!settings.Enabled && !settings.AutoOwn && settings.AutoOthers,"existing explicit choices preserved");
            Check(settings.MinDelaySeconds == 4.5M && settings.MaxDelaySeconds == 9,"fractional delays preserved");
            var json = new JavaScriptSerializer();
            var copy = json.Deserialize<ChestSettings>(json.Serialize(settings));
            Check(copy.AutoOthers && !copy.AutoOwn && copy.MinDelaySeconds == 4.5M,"backend request roundtrip");
            File.WriteAllText(path,"AutoOthers=not-a-bool\nMinDelaySeconds=NaN\nMaxDelaySeconds=999999");
            settings = ChestSettings.Read(path);
            Check(!settings.AutoOthers && settings.MinDelaySeconds == 3 && settings.MaxDelaySeconds == 8,"invalid values use own-only defaults");
        } finally { File.Delete(path); }
        Check(GameLocation.Libraries(@"C:\Steam","\"path\" \"D:\\\\SteamLibrary\"").Contains(@"D:\SteamLibrary"),"secondary Steam library parsing");
        Check(GameLocation.Quote(@"D:\games with spaces\") == "\"D:\\games with spaces\\\\\"","trailing backslash command-line quoting");
        Check(GameLocation.Quote("abc\"def") == "\"abc\\\"def\"","embedded quote escaping");
        Check(!GameLocation.IsGame(Path.GetTempPath()),"arbitrary folders rejected");
        Console.WriteLine("12 wizard model checks passed.");
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BongoCat;
using BongoCat.Multiplayer;
using Heathen.SteamworksIntegration;
using UnityEngine;

namespace BongoAutoChest
{
    public static class AutoChest
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ShopItemField = typeof(Shop).GetField("_shopItem", Private);
        private static readonly FieldInfo OpeningField = typeof(Shop).GetField("_openingChest", Private);
        private static readonly FieldInfo WaitingField = typeof(ShopItem).GetField("_waitingForServer", Private);
        private static readonly FieldInfo PriceField = typeof(ShopItem).GetField("_price", Private);
        private static readonly FieldInfo TargetField = typeof(OpenChestForMember).GetField("_targetUser", Private);
        private static readonly FieldInfo TypeField = typeof(OpenChestForMember).GetField("_chestType", Private);
        private static readonly FieldInfo ActiveField = typeof(OpenChestForMember).GetField("_setChestActive", Private);
        private static readonly FieldInfo ClickedField = typeof(OpenChestForMember).GetField("_hasClicked", Private);
        private static readonly FieldInfo MemberField = typeof(SetActiveBasedOnLobbyMemberValue).GetField("_lobbyMemberData", Private);
        private static readonly FieldInfo KeyField = typeof(SetActiveBasedOnLobbyMemberValue).GetField("_key", Private);
        private static readonly FieldInfo ObjectField = typeof(SetActiveBasedOnLobbyMemberValue).GetField("_gameObject", Private);
        private static readonly Dictionary<string, ReadyGate> Gates = new Dictionary<string, ReadyGate>();
        private static readonly Dictionary<int, string> OwnRequests = new Dictionary<int, string>();
        private static readonly System.Random Random = new System.Random();
        private static OpenChestForMember[] RemoteButtons = new OpenChestForMember[0];
        private static string ConfigPath, LogPath;
        private static bool Initialized, Enabled = true, Own = true, Others = false, HotkeyWasDown, Faulted;
        private static double MinDelay = 3, MaxDelay = 8, NextAction, NextConfig, NextScan, NextObserve;
        private static DateTime ConfigStamp;
        private static string LastSummary = "";
        private static double NextSummary;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        private sealed class Candidate
        {
            public string Key;
            public ReadyGate Gate;
            public Shop Shop;
            public ShopItem Item;
            public OpenChestForMember Button;
            public bool IsOwn;
        }

        // Called on Unity's main thread from MainCat.Update.
        public static void Tick()
        {
            if (Faulted) return;
            try { TickCore(); }
            catch (Exception ex)
            {
                Faulted = true;
                Enabled = false;
                Log("ERROR: automation stopped; restart after fixing: " + ex.ToString());
            }
        }

        private static void TickCore()
        {
            double now = Time.realtimeSinceStartup;
            if (!Initialized)
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                ConfigPath = Path.Combine(root, "BongoAutoChest.ini");
                LogPath = Path.Combine(root, "BongoAutoChest.log");
                foreach (FieldInfo field in new[] { ShopItemField, OpeningField, WaitingField, PriceField,
                    TargetField, TypeField, ActiveField, ClickedField, MemberField, KeyField, ObjectField })
                    if (field == null) throw new InvalidOperationException("Incompatible game fields.");
                Initialized = true;
                if (!File.Exists(ConfigPath)) SaveConfig();
                ReadConfig();
                Schedule(now);
                Log("Loaded v1; own=" + Own.ToString() + "; others=" + Others.ToString() + "; enabled=" + Enabled.ToString()
                    + "; delay=" + MinDelay.ToString() + ".." + MaxDelay.ToString() + "s; Ctrl+Alt+F9 toggles.");
            }
            bool hotkey = Down(0x11) && Down(0x12) && Down(0x78);
            if (hotkey && !HotkeyWasDown)
            {
                Enabled = !Enabled;
                SaveConfig();
                Schedule(now);
                Log("Toggle: " + (Enabled ? "ON" : "OFF"));
            }
            HotkeyWasDown = hotkey;
            if (now >= NextConfig)
            {
                NextConfig = now + 2;
                if (File.Exists(ConfigPath) && File.GetLastWriteTime(ConfigPath) != ConfigStamp)
                {
                    ReadConfig();
                    Schedule(now);
                    Log("Config reloaded; enabled=" + Enabled.ToString() + "; own=" + Own.ToString() + "; others=" + Others.ToString());
                }
            }
            if (now < NextObserve) return;
            NextObserve = now + 0.5;
            var candidates = new List<Candidate>();
            ObserveOwn(Shop.NormalShop, "own:cosmetic", candidates);
            ObserveOwn(Shop.EmoteShop, "own:emote", candidates);
            if (now >= NextScan)
            {
                NextScan = now + 3;
                RemoteButtons = Resources.FindObjectsOfTypeAll<OpenChestForMember>();
            }
            int remoteReady = 0;
            if (LobbyData.Current.IsValid)
                foreach (OpenChestForMember button in RemoteButtons)
                    if (ObserveRemote(button, candidates)) remoteReady++;

            if (now >= NextSummary)
            {
                NextSummary = now + 30;
                string summary = "Status: enabled=" + Enabled.ToString() + "; lobby=" + LobbyData.Current.IsValid.ToString()
                    + "; remoteReady=" + remoteReady.ToString() + "; candidates=" + candidates.Count.ToString()
                    + "; ownPending=" + OwnRequests.Count.ToString();
                if (summary != LastSummary) { LastSummary = summary; Log(summary); }
            }
            // Own purchases deduct points on completion, so reserve the entire dispatcher while pending.
            if (!Enabled || now < NextAction || OwnRequests.Count > 0
                || IsBusy(Shop.NormalShop) || IsBusy(Shop.EmoteShop) || candidates.Count == 0) return;
            var ownCandidates = candidates.FindAll(delegate(Candidate c) { return c.IsOwn; });
            var pool = ownCandidates.Count > 0 ? ownCandidates : candidates;
            Candidate selected = pool[Random.Next(pool.Count)];
            selected.Gate.MarkAttempt();
            Schedule(now);
            int price = (int)PriceField.GetValue(selected.Item);
            if (selected.IsOwn)
            {
                OwnRequests[selected.Item.GetInstanceID()] = selected.Key;
                Log("REQUEST " + selected.Key + "; price=" + price.ToString());
                selected.Item.Buy();
                if (!(bool)WaitingField.GetValue(selected.Item))
                {
                    OwnRequests.Remove(selected.Item.GetInstanceID());
                    Log("NOT_STARTED " + selected.Key + "; no automatic retry in this ready cycle.");
                }
            }
            else
            {
                selected.Button.OnClicked();
                bool sent = (bool)ClickedField.GetValue(selected.Button);
                Log((sent ? "SENT " : "NOT_STARTED ") + selected.Key + "; price=" + price.ToString()
                    + "; recipient reward not confirmed by this log.");
            }
        }

        private static bool Down(int code) { return (GetAsyncKeyState(code) & 0x8000) != 0; }
        private static ReadyGate Gate(string key)
        {
            ReadyGate gate;
            if (!Gates.TryGetValue(key, out gate)) { gate = new ReadyGate(); Gates.Add(key, gate); }
            return gate;
        }
        private static bool IsBusy(Shop shop)
        {
            if (!shop) return false;
            ShopItem item = (ShopItem)ShopItemField.GetValue(shop);
            return (bool)OpeningField.GetValue(shop) || (item && (bool)WaitingField.GetValue(item));
        }
        private static void ObserveOwn(Shop shop, string key, List<Candidate> candidates)
        {
            if (!shop) return;
            ReadyGate gate = Gate(key);
            gate.Observe(shop.ChestIsReady);
            ShopItem item = (ShopItem)ShopItemField.GetValue(shop);
            if (Own && gate.CanAttempt && item && item.gameObject.activeSelf && !IsBusy(shop) && item.CanBuy())
                candidates.Add(new Candidate { Key = key, Gate = gate, Shop = shop, Item = item, IsOwn = true });
        }
        private static bool ObserveRemote(OpenChestForMember button, List<Candidate> candidates)
        {
            if (!button || !button.gameObject.scene.IsValid()) return false;
            SteamUserData target = (SteamUserData)TargetField.GetValue(button);
            if (!target || target.Data.Equals(UserData.Me)) return false;
            LobbyMemberInstance instance;
            if (!MultiplayerLobbyReferences.GameplayInstances.TryGetValue(target.Data, out instance) || !instance) return false;
            var active = (SetActiveBasedOnLobbyMemberValue)ActiveField.GetValue(button);
            if (!active) return false;
            var member = (SteamLobbyMemberData)MemberField.GetValue(active);
            if (!member || !member.Data.lobby.Equals(LobbyData.Current)) return false;
            string metadataKey = (string)KeyField.GetValue(active);
            bool ready;
            if (!bool.TryParse(member.Data[metadataKey], out ready)) return false;
            string key = "remote:" + target.Data.IdStr + ":" + metadataKey;
            ReadyGate gate = Gate(key);
            bool cleared = gate.Observe(ready);
            if (cleared) Log("READY_CLEARED " + key + "; not an item-delivery acknowledgement.");
            if (!ready)
            {
                // Rearm the existing UI lock only after observing a real not-ready state.
                button.Reset();
                return false;
            }
            bool clicked = (bool)ClickedField.GetValue(button);
            if (clicked) gate.MarkAttempt(); // Includes a manual click by the user.
            var visibleChest = (GameObject)ObjectField.GetValue(active);
            if (!Others || !gate.CanAttempt || instance.IsHidden || !button.isActiveAndEnabled
                || !visibleChest || !visibleChest.activeInHierarchy) return true;
            ChestType type = (ChestType)TypeField.GetValue(button);
            Shop shop = type == ChestType.Emote ? Shop.EmoteShop : Shop.NormalShop;
            if (!shop) return true;
            ShopItem item = (ShopItem)ShopItemField.GetValue(shop);
            if (item && item.CanBuy())
                candidates.Add(new Candidate { Key = key, Gate = gate, Shop = shop, Item = item, Button = button });
            return true;
        }

        // Prefix in ShopItem.Callback; does not alter the game's result or purchase flow.
        public static void OnOwnResult(ShopItem item, int itemId, bool chargePets)
        {
            try
            {
                string key;
                if (item && OwnRequests.TryGetValue(item.GetInstanceID(), out key))
                {
                    OwnRequests.Remove(item.GetInstanceID());
                    Log((itemId == -1 ? "FAILED " : "RECEIVED ") + key + "; item=" + itemId.ToString());
                    Schedule(Time.realtimeSinceStartup);
                }
            }
            catch (Exception ex) { Log("Result logging error: " + ex.Message); }
        }
        private static void Schedule(double now) { NextAction = now + MinDelay + (Random.Next(1000000) / 1000000.0) * (MaxDelay - MinDelay); }
        private static void ReadConfig()
        {
            foreach (string raw in File.ReadAllText(ConfigPath).Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#") || !line.Contains("=")) continue;
                string[] parts = line.Split(new[] { '=' }, 2);
                string key = parts[0].Trim(), value = parts[1].Trim();
                bool b; double d;
                if (key == "Enabled" && bool.TryParse(value, out b)) Enabled = b;
                if (key == "AutoOwn" && bool.TryParse(value, out b)) Own = b;
                if (key == "AutoOthers" && bool.TryParse(value, out b)) Others = b;
                if (key == "MinDelaySeconds" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && !double.IsNaN(d) && !double.IsInfinity(d)) MinDelay = Math.Max(1, Math.Min(300, d));
                if (key == "MaxDelaySeconds" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && !double.IsNaN(d) && !double.IsInfinity(d)) MaxDelay = Math.Max(1, Math.Min(300, d));
            }
            if (MaxDelay < MinDelay) MaxDelay = MinDelay;
            ConfigStamp = File.GetLastWriteTime(ConfigPath);
        }
        private static void SaveConfig()
        {
            File.WriteAllText(ConfigPath, string.Join(Environment.NewLine, new[] {
                "# Ctrl+Alt+F9 toggles all automatic opening. Changes reload every 2 seconds.",
                "Enabled=" + Enabled.ToString().ToLowerInvariant(),
                "AutoOwn=" + Own.ToString().ToLowerInvariant(),
                "AutoOthers=" + Others.ToString().ToLowerInvariant(),
                "MinDelaySeconds=" + MinDelay.ToString(CultureInfo.InvariantCulture),
                "MaxDelaySeconds=" + MaxDelay.ToString(CultureInfo.InvariantCulture)
            }));
            ConfigStamp = File.GetLastWriteTime(ConfigPath);
        }
        private static void Log(string message)
        {
            try
            {
                string text = "[BongoAutoChest] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message;
                Debug.Log(text);
                if (LogPath != null)
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text + Environment.NewLine);
                    using (Stream stream = File.Open(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch { }
        }
    }
}

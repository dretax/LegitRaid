using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fougerite;
using Fougerite.Events;
using RustPP.Social;
using UnityEngine;

namespace LegitRaid
{
    public class LegitRaid : Fougerite.Module
    {
        public Dictionary<ulong, int> OwnerTimeData;
        public Dictionary<ulong, int> RaiderTime;
        public int RaidTime = 20;
        public int MaxRaidTime = 60;
        public bool AllowAllModerators = false;
        public bool RustPP = false;
        public bool CanOpenChestIfThereIsNoStructureClose = false;
        public bool AutoWhiteListFriends = false;
        public readonly List<string> WhiteListedIDs = new List<string>();
        public readonly List<string> DSNames = new List<string>();
        public readonly IEnumerable<string> Guns = new string[]
        {
            "M4", "MP5A4", "9mm Pistol", "Hunting Bow", "Bolt Action Rifle", "Shotgun", "Pipe Shotgun", "HandCannon",
            "P250", "Revolver"
        };
        public IniParser Settings;
        public string PathC;
        public string PathLog;
        public System.IO.StreamWriter file;

        public const string red = "[color #FF0000]";
        public const string yellow = "[color yellow]";
        public const string green = "[color green]";
        public const string orange = "[color #ffa500]";

        public override string Name
        {
            get { return "LegitRaid"; }
        }

        public override string Author
        {
            get { return "DreTaX"; }
        }

        public override string Description
        {
            get { return "LegitRaid"; }
        }

        public override Version Version
        {
            get { return new Version("1.2.2"); }
        }

        public override void Initialize()
        {
            if (!File.Exists(Path.Combine(ModuleFolder, "Logs.log"))) { File.Create(Path.Combine(ModuleFolder, "Logs.log")).Dispose(); }
            PathLog = Path.Combine(ModuleFolder, "Logs.log");
            PathC = Path.Combine(ModuleFolder, "Settings.ini");
            RaiderTime = new Dictionary<ulong, int>();
            OwnerTimeData = new Dictionary<ulong, int>();
            if (!File.Exists(PathC))
            {
                File.Create(PathC).Dispose();
                Settings = new IniParser(PathC);
                Settings.AddSetting("Settings", "RaidTime", "20");
                Settings.AddSetting("Settings", "MaxRaidTime", "60");
                Settings.AddSetting("Settings", "AllowAllModerators", "False");
                Settings.AddSetting("Settings", "CanOpenChestIfThereIsNoStructureClose", "True");
                Settings.AddSetting("Settings", "DataStoreTables", "ExamplePluginDataStoreName,ExamplePluginDataStoreName2,ExamplePluginDataStoreName3");
                Settings.AddSetting("Settings", "WhiteListedIDs", "76561197967414xxx,76561197961635xxx,76561197961634xxx");
                Settings.AddSetting("Settings", "AutoWhiteListFriends", "False");
                Settings.Save();
            }
            else
            {
                Settings = new IniParser(PathC);
                RaidTime = int.Parse(Settings.GetSetting("Settings", "RaidTime"));
                MaxRaidTime = int.Parse(Settings.GetSetting("Settings", "MaxRaidTime"));
                AllowAllModerators = Settings.GetBoolSetting("Settings", "AllowAllModerators");
                CanOpenChestIfThereIsNoStructureClose = Settings.GetBoolSetting("Settings", "CanOpenChestIfThereIsNoStructureClose");
                AutoWhiteListFriends = Settings.GetBoolSetting("Settings", "AutoWhiteListFriends");
                var Collect = Settings.GetSetting("Settings", "WhiteListedIDs");
                var splits = Collect.Split(Convert.ToChar(","));
                foreach (var x in splits)
                {
                    WhiteListedIDs.Add(x);
                }
                var Collect2 = Settings.GetSetting("Settings", "DataStoreTables");
                var splits2 = Collect2.Split(Convert.ToChar(","));
                foreach (var x in splits2)
                {
                    DSNames.Add(x);
                }
            }
            Fougerite.Hooks.OnLootUse += OnLootUse;
            Fougerite.Hooks.OnEntityDestroyed += OnEntityDestroyed;
            Fougerite.Hooks.OnEntityHurt += OnEntityHurt;
            Fougerite.Hooks.OnModulesLoaded += OnModulesLoaded;
            Fougerite.Hooks.OnCommand += OnCommand;
            Fougerite.Hooks.OnServerSaved += OnServerSaved;
            Fougerite.Hooks.OnEntityDeployedWithPlacer += OnEntityDeployedWithPlacer;
        }

        public override void DeInitialize()
        {
            Fougerite.Hooks.OnLootUse -= OnLootUse;
            Fougerite.Hooks.OnEntityDestroyed -= OnEntityDestroyed;
            Fougerite.Hooks.OnEntityDeployedWithPlacer -= OnEntityDeployedWithPlacer;
            Fougerite.Hooks.OnEntityHurt -= OnEntityHurt;
            Fougerite.Hooks.OnModulesLoaded -= OnModulesLoaded;
            Fougerite.Hooks.OnCommand -= OnCommand;
            Fougerite.Hooks.OnServerSaved -= OnServerSaved;
        }

        public void OnEntityDeployedWithPlacer(Fougerite.Player player, Entity e, Fougerite.Player actualplacer)
        {
            if (actualplacer == null || e == null)
            {
                return;
            }
            if (!e.Name.ToLower().Contains("storage") && !e.Name.ToLower().Contains("stash"))
            {
                return;
            }
            var id = GetHouseOwner(e);
            if (id == 0)
            {
                return;
            }
            if (actualplacer.UID != id)
            {
                if (DataStore.GetInstance().Get("LegitRaidED", id) != null)
                {
                    List<string> list = (List<string>) DataStore.GetInstance().Get("LegitRaidED", id);
                    if (!list.Contains(actualplacer.SteamID))
                    {
                        list.Add(actualplacer.SteamID);
                    }
                    DataStore.GetInstance().Add("LegitRaidED", id, list);
                }
                else
                {
                    List<string> list = new List<string>();
                    list.Add(actualplacer.SteamID);
                    DataStore.GetInstance().Add("LegitRaidED", id, list);
                }
            }
        }

        public ulong GetHouseOwner(Entity e)
        {
            RaycastHit cachedRaycast;
            StructureComponent cachedStructure;
            Collider cachedCollider;
            StructureMaster cachedMaster;
            Facepunch.MeshBatch.MeshBatchInstance cachedhitInstance;
            bool cachedBoolean;
            var entitypos = e.Location;
            if (!Facepunch.MeshBatch.MeshBatchPhysics.Raycast(new Ray(entitypos, new Vector3(0f, -1f, 0f)),
                out cachedRaycast, out cachedBoolean, out cachedhitInstance))
            {
                return 0;
            }
            if (cachedhitInstance != null)
            {
                cachedCollider = cachedhitInstance.physicalColliderReferenceOnly;
                if (cachedCollider == null)
                {
                    return 0;
                }
                cachedStructure = cachedCollider.GetComponent<StructureComponent>();
                if (cachedStructure != null && cachedStructure._master != null)
                {
                    cachedMaster = cachedStructure._master;
                    var id = cachedMaster.ownerID;
                    return id;
                }
            }
            return 0;
        }

        public void OnServerSaved()
        {
            var instance = DataStore.GetInstance();
            instance.Flush("LOwnerTimeData");
            instance.Flush("LRaiderTime");
            foreach (var x in RaiderTime.Keys)
            {
                instance.Add("LRaiderTime", x, RaiderTime[x]);
            }
            foreach (var x in OwnerTimeData.Keys)
            {
                instance.Add("LOwnerTimeData", x, OwnerTimeData[x]);
            }
        }

        public void OnCommand(Fougerite.Player player, string cmd, string[] args)
        {
            if (cmd == "legitraid")
            {
                player.MessageFrom("LegitRaid", green + "LegitRaid " + yellow + " V" + Version + " [COLOR#FFFFFF] By " + Author);
                if (player.Admin)
                {
                    Settings = new IniParser(PathC);
                    RaidTime = int.Parse(Settings.GetSetting("Settings", "RaidTime"));
                    MaxRaidTime = int.Parse(Settings.GetSetting("Settings", "MaxRaidTime"));
                    AllowAllModerators = Settings.GetBoolSetting("Settings", "AllowAllModerators");
                    CanOpenChestIfThereIsNoStructureClose = Settings.GetBoolSetting("Settings", "CanOpenChestIfThereIsNoStructureClose");
                    AutoWhiteListFriends = Settings.GetBoolSetting("Settings", "AutoWhiteListFriends");
                    var Collect = Settings.GetSetting("Settings", "WhiteListedIDs");
                    var splits = Collect.Split(Convert.ToChar(","));
                    WhiteListedIDs.Clear();
                    foreach (var x in splits)
                    {
                        WhiteListedIDs.Add(x);
                    }
                    var Collect2 = Settings.GetSetting("Settings", "DataStoreTables");
                    var splits2 = Collect2.Split(Convert.ToChar(","));
                    foreach (var x in splits2)
                    {
                        DSNames.Add(x);
                    }
                    player.MessageFrom("LegitRaid", "Reloaded!");
                }
            }
            else if (cmd == "flushlegita")
            {
                if (player.Admin)
                {
                    DataStore.GetInstance().Flush("LegitRaidED");
                    player.MessageFrom("LegitRaid", "Flushed!");
                }
            }
            else if (cmd == "friendraid")
            {
                if (!RustPP || AutoWhiteListFriends)
                {
                    player.MessageFrom("LegitRaid", "Friends are AutoWhitelisted on this Server.");
                    return;
                }
                bool contains = DataStore.GetInstance().ContainsKey("LegitRaid", player.UID);
                if (!contains)
                {
                    DataStore.GetInstance().Add("LegitRaid", player.UID, true);
                    player.MessageFrom("LegitRaid", "Your friends can now open your chests.");
                }
                else
                {
                    DataStore.GetInstance().Remove("LegitRaid", player.UID);
                    player.MessageFrom("LegitRaid", "Your friends can not open your chests.");
                }
            }
            else if (cmd == "raida")
            {
                if (player.Admin || (player.Moderator && AllowAllModerators) || (player.Moderator && WhiteListedIDs.Contains(player.SteamID)))
                {
                    bool contains = DataStore.GetInstance().ContainsKey("LegitRaidA", player.UID);
                    if (!contains)
                    {
                        DataStore.GetInstance().Add("LegitRaidA", player.UID, true);
                        player.MessageFrom("LegitRaid", "You can now open all the chests");
                        file = new System.IO.StreamWriter(PathLog, true);
                        file.WriteLine(DateTime.Now + " " + player.Name + "-" + player.SteamID + " entered all loot mode");
                        file.Close();
                    }
                    else
                    {
                        DataStore.GetInstance().Remove("LegitRaidA", player.UID);
                        player.MessageFrom("LegitRaid", "Disabled");
                        file = new System.IO.StreamWriter(PathLog, true);
                        file.WriteLine(DateTime.Now + " " + player.Name + "-" + player.SteamID + " quit all loot mode");
                        file.Close();
                    }
                }
            }
        }

        public void OnModulesLoaded()
        {
            RustPP = Fougerite.Server.GetServer().HasRustPP;
            var instance = DataStore.GetInstance();
            if (instance.GetTable("LRaiderTime") != null)
            {
                foreach (var x in instance.Keys("LRaiderTime"))
                {
                    try
                    {
                        RaiderTime[(ulong) x] = (int) instance.Get("LRaiderTime", x);
                    }
                    catch
                    {

                    }
                }
            }
            if (instance.GetTable("LOwnerTimeData") != null)
            {
                foreach (var x in instance.Keys("LOwnerTimeData"))
                {
                    try
                    {
                        OwnerTimeData[(ulong) x] = (int) instance.Get("LOwnerTimeData", x);
                    }
                    catch
                    {

                    }
                }
            }
            instance.Flush("LOwnerTimeData");
            instance.Flush("LRaiderTime");
        }

        public void OnEntityHurt(HurtEvent he)
        {
            if (he.AttackerIsPlayer && he.VictimIsEntity)
            {
                if (he.Attacker != null && he.Entity != null)
                {
                    Fougerite.Entity entity = he.Entity;
                    if (entity.Name.ToLower().Contains("box") || entity.Name.ToLower().Contains("stash"))
                    {
                        if (!he.WeaponName.Contains("explosive") && !he.WeaponName.Contains("grenade") &&
                            !Guns.Contains(he.WeaponName) && !OwnerTimeData.ContainsKey(entity.UOwnerID))
                        {
                            he.DamageAmount = 0f;
                        }
                    }
                }
            }
        }

        public void OnEntityDestroyed(DestroyEvent de)
        {
            if (de.Attacker != null && de.Entity != null && !de.IsDecay)
            {
                if (((Fougerite.Player) de.Attacker).UID == de.Entity.UOwnerID)
                {
                    return;
                }
                if ((de.WeaponName.ToLower().Contains("explosive") || de.WeaponName.ToLower().Contains("grenade") 
                    || de.WeaponName.ToLower().Contains("hatchet") || de.WeaponName.ToLower().Contains("axe") 
                    || de.WeaponName.ToLower().Contains("rock")) && (de.Entity.Name.ToLower().Contains("wall") 
                    || de.Entity.Name.ToLower().Contains("door")))
                {
                    Fougerite.Entity entity = de.Entity;
                    OwnerTimeData[entity.UOwnerID] = System.Environment.TickCount;
                    if (RaiderTime.ContainsKey(entity.UOwnerID))
                    {
                        if (MaxRaidTime < RaiderTime[entity.UOwnerID] + RaidTime)
                        {
                            RaiderTime[entity.UOwnerID] = MaxRaidTime;
                            return;
                        }
                        RaiderTime[entity.UOwnerID] = RaiderTime[entity.UOwnerID] + RaidTime;
                    }
                    else
                    {
                        RaiderTime[entity.UOwnerID] = RaidTime;
                    }
                }
            }
        }

        public void OnLootUse(LootStartEvent lootstartevent)
        {
            if (!lootstartevent.IsObject || DataStore.GetInstance().ContainsKey("LegitRaidA", lootstartevent.Player.UID)
                || DataStore.GetInstance().ContainsKey("HGIG", lootstartevent.Player.SteamID)) {return;}
            if (DSNames.Any(table => DataStore.GetInstance().ContainsKey(table, lootstartevent.Player.SteamID) ||
                                     DataStore.GetInstance().ContainsKey(table, lootstartevent.Player.UID)))
            {
                return;
            }
            if (lootstartevent.Entity.UOwnerID == lootstartevent.Player.UID)
            {
                return;
            }
            if (CanOpenChestIfThereIsNoStructureClose)
            {
                var objects = Physics.OverlapSphere(lootstartevent.Entity.Location, 3.8f);
                var names = new List<string>();
                foreach (var x in objects.Where(x => !names.Contains(x.name.ToLower())))
                {
                    names.Add(x.name.ToLower());
                }
                string ncollected = string.Join(" ", names.ToArray());
                if (ncollected.Contains("shelter") && !ncollected.Contains("door"))
                {
                    return;
                }
                if (!ncollected.Contains("meshbatch"))
                {
                    return;
                }
            }
            if (RustPP)
            {
                var friendc = Fougerite.Server.GetServer().GetRustPPAPI().GetFriendsCommand.GetFriendsLists();
                if (friendc.ContainsKey(lootstartevent.Entity.UOwnerID))
                { 
                    var fs = (RustPP.Social.FriendList) friendc[lootstartevent.Entity.UOwnerID];
                    bool isfriend = fs.Cast<FriendList.Friend>().Any(friend => friend.GetUserID() == lootstartevent.Player.UID);
                    if ((isfriend && DataStore.GetInstance().Get("LegitRaid", lootstartevent.Entity.UOwnerID) != null) || (isfriend && AutoWhiteListFriends))
                    {
                        return;
                    }
                }
            }
            if (OwnerTimeData.ContainsKey(lootstartevent.Entity.UOwnerID))
            {
                var id = lootstartevent.Entity.UOwnerID;
                var ticks = OwnerTimeData[id];
                var calc = System.Environment.TickCount - ticks;
                int timeraid = RaidTime;
                if (RaiderTime.ContainsKey(id))
                {
                    timeraid = RaiderTime[id];
                }
                if (calc < 0 || double.IsNaN(calc) || double.IsNaN(ticks))
                {
                    lootstartevent.Cancel();
                    lootstartevent.Player.Notice("", "You need to use C4/Grenade on wall and raid within " + RaidTime + " mins!", 8f);
                    lootstartevent.Player.MessageFrom("LegitRaid", orange + "If your friend owns the chest tell him to add you with /addfriend name");
                    lootstartevent.Player.MessageFrom("LegitRaid", orange + "After that tell him to type /friendraid !");
                    OwnerTimeData.Remove(id);
                    if (RaiderTime.ContainsKey(id))
                    {
                        RaiderTime.Remove(id);
                    }
                }
                if (calc >= (RaidTime + timeraid) * 60000)
                {
                    lootstartevent.Cancel();
                    lootstartevent.Player.Notice("", "You need to use C4/Grenade on wall and raid within " + RaidTime + " mins!", 8f);
                    lootstartevent.Player.MessageFrom("LegitRaid", orange + "If your friend owns the chest tell him to add you with /addfriend name");
                    lootstartevent.Player.MessageFrom("LegitRaid", orange + "After that tell him to type /friendraid !");
                    OwnerTimeData.Remove(id);
                    if (RaiderTime.ContainsKey(id))
                    {
                        RaiderTime.Remove(id);
                    }
                }
                else
                {
                    var done = Math.Round((float)((calc / 1000) / 60));
                    lootstartevent.Player.Notice("You can loot until: " + (timeraid - done) + " minutes!");
                }
            }
            else
            {
                var id = GetHouseOwner(lootstartevent.Entity);
                if (DataStore.GetInstance().Get("LegitRaidED", id) != null)
                {
                    List<string> list = (List<string>)DataStore.GetInstance().Get("LegitRaidED", id);
                    if (list.Contains(lootstartevent.Entity.OwnerID) && OwnerTimeData.ContainsKey(id))
                    {
                        var ticks = OwnerTimeData[id];
                        var calc = System.Environment.TickCount - ticks;
                        int timeraid = RaidTime;
                        if (RaiderTime.ContainsKey(id))
                        {
                            timeraid = RaiderTime[id];
                        }
                        if (calc < 0 || double.IsNaN(calc) || double.IsNaN(ticks))
                        {
                            lootstartevent.Cancel();
                            lootstartevent.Player.Notice("", "You need to use C4/Grenade on wall and raid within " + RaidTime + " mins!", 8f);
                            lootstartevent.Player.MessageFrom("LegitRaid", orange + "If your friend owns the chest tell him to add you with /addfriend name");
                            lootstartevent.Player.MessageFrom("LegitRaid", orange + "After that tell him to type /friendraid !");
                            OwnerTimeData.Remove(id);
                            if (RaiderTime.ContainsKey(id))
                            {
                                RaiderTime.Remove(id);
                            }
                        }
                        if (calc >= (RaidTime + timeraid) * 60000)
                        {
                            lootstartevent.Cancel();
                            lootstartevent.Player.Notice("", "You need to use C4/Grenade on wall and raid within " + RaidTime + " mins!", 8f);
                            lootstartevent.Player.MessageFrom("LegitRaid", orange + "If your friend owns the chest tell him to add you with /addfriend name");
                            lootstartevent.Player.MessageFrom("LegitRaid", orange + "After that tell him to type /friendraid !");
                            OwnerTimeData.Remove(id);
                            if (RaiderTime.ContainsKey(id))
                            {
                                RaiderTime.Remove(id);
                            }
                        }
                        else
                        {
                            var done = Math.Round((float)((calc / 1000) / 60));
                            lootstartevent.Player.Notice("You can loot until: " + (timeraid - done) + " minutes!");
                        }
                        return;
                    }
                }
                lootstartevent.Cancel();
                lootstartevent.Player.Notice("", "You need to use C4/Grenade on wall and raid within " + RaidTime + " mins!", 8f);
                lootstartevent.Player.MessageFrom("LegitRaid", orange + "If your friend owns the chest tell him to add you with /addfriend name");
                lootstartevent.Player.MessageFrom("LegitRaid", orange + "After that tell him to type /friendraid !");
            }
        }
    }
}

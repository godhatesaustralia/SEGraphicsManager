using Sandbox.Game;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.VisualScripting.Utils;
using VRageMath;

namespace IngameScript
{
    public class Info
    {
        public readonly string Name;
        public double Data => value;
        protected double value, lastValue;
        protected bool dataChanged => value != lastValue;
        protected Func<double> update;
        public Info(string n = "", Func<double> u = null)
        {
            Name = n;
            update = u;
        }
        public virtual void Update() => value = update.Invoke();
    }

    public abstract class UtilityBase
    {
        #region fields

        protected MyGridProgram Program;
        protected GraphicsManager GCM;
        protected IMyGridTerminalSystem TerminalSystem;
        protected const char
            cmd = '!',
            bar = 'b',
            pct = '%';
        protected string invalid = "••", name;
        public string Name => name.ToUpper();
        protected SpriteData jitSprite = new SpriteData();
        protected const double bad = double.NaN;
        protected Dictionary<long, string> Formats = new Dictionary<long, string>();
        #endregion

        public virtual void Reset(GraphicsManager manager, MyGridProgram program)
        {
            GCM = manager;
            Program = program;
            TerminalSystem = program.GridTerminalSystem;
            jitSprite.uID = long.MinValue;
            GetBlocks();
        }

        protected void Validate(double v, ref SpriteData d, string def = "")
        {
            if (double.IsNaN(v)) d.Data = invalid;
            else d.SetData(v, def);
        }
        public virtual void Update()
        {

        }

        public abstract void GetBlocks();

        public abstract void Setup(ref Dictionary<string, Action<SpriteData>> commands);


    }

    // SO...command formatting. Depends on the general command, but here's the idea
    // this is all for the DATA field of the sprite.
    // <required param 1>$<required param 2>$...$<required param n>

    public class GasUtilities : UtilityBase
    {
        List<IMyGasTank>
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
        List<IMyGasGenerator> Gens = new List<IMyGasGenerator>();
        UseRate Ice;
        ItemInfo IceOre;
        const int keen = 5;
        DateTime tsH2;
        double lastH2; // sigh
        Info h2, o2;

        public GasUtilities()
        {
            name = "Gas";
            Ice = new UseRate("Ore!Ice");
        }

        #region UtilityBase

        public override void GetBlocks()
        {
            HydrogenTanks.Clear();
            OxygenTanks.Clear();
            Gens.Clear();
            TerminalSystem.GetBlocksOfType(HydrogenTanks, (b) => b.IsSameConstructAs(GCM.Me) && b.BlockDefinition.SubtypeId.Contains("Hyd"));
            TerminalSystem.GetBlocksOfType(OxygenTanks, (b) => !HydrogenTanks.Contains(b) && b.IsSameConstructAs(GCM.Me));
            TerminalSystem.GetBlocksOfType(Gens, (b) => b.IsSameConstructAs(GCM.Me));
            IceOre = new ItemInfo("Ore", "Ice", Gens.ToList<IMyTerminalBlock>());
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {           //s fucking hate this, this sucks ass
            h2 = new Info("h2%", HydrogenStatus);
            o2 = new Info("o2%", OxygenStatus);
            // JIT
            HydrogenTime();
            HydrogenStatus();
            OxygenStatus();

            commands.Add("!h2%", b =>
                b.SetData(h2.Data, "#0.#%"));

            commands.Add("!o2%", b =>
                b.SetData(o2.Data, "#0.#%"));

            commands.Add("!h2b", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, h2.Data);
            });
            commands.Add("!o2b", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, o2.Data);
            });
            commands.Add("!h2t", b =>
            {
                var t = HydrogenTime();
                if (t == TimeSpan.Zero)
                    b.Data = invalid;
                if (b.Format == "")
                    if (t.TotalHours >= 1)
                        b.Data = string.Format("{0,2}h {1,2}m", (long)t.TotalHours, (long)t.Minutes);
                    else
                        b.Data = string.Format("{0,2}m {1,2}s", (long)t.TotalMinutes, (long)t.Seconds);
                else
                    b.SetData(t.TotalSeconds);
            });

            commands.Add("!ice", b =>
            {
                var rate = 0d;
                b.Data = InventoryUtilities.TryGetUseRate(ref Ice, ref IceOre, out rate) ? (rate / keen).ToString("G4") : invalid;
            });
        }

        public override void Update()
        {
            h2.Update();
            o2.Update();
        }

        #endregion
        double HydrogenStatus() => TankStatus(ref HydrogenTanks);
        double OxygenStatus() => TankStatus(ref OxygenTanks);

        double TankStatus(ref List<IMyGasTank> tanks)
        {
            var amt = 0d;
            var total = amt;
            for (int i = 0; i < tanks.Count; i++)
            {
                amt += tanks[i].FilledRatio * tanks[i].Capacity;
                total += tanks[i].Capacity;
            }
            return amt / total;
        }

        TimeSpan HydrogenTime()
        {
            var ts = DateTime.Now;
            var rate = MathHelperD.Clamp(lastH2 - h2.Data, 1E-50, double.MaxValue) / (ts - tsH2).TotalSeconds;
            var value = h2.Data / rate;
            lastH2 = h2.Data;
            tsH2 = ts;
            if (rate < 1E-15 || double.IsNaN(value) || value > 1E5)
                return TimeSpan.Zero;
            return TimeSpan.FromSeconds(value);
        }
    }

    public class UseRate
    {
        double lastQ = 0, valid = 0;
        const int wait = 5;
        DateTime lastTS, validTS;
        public readonly string iID;
        public UseRate(string i)
        { iID = i; }
        public double Rate(double q)
        {
            var t = DateTime.Now;
            double r = -(q - lastQ) / (t - lastTS).TotalSeconds;
            lastQ = q;
            lastTS = t;
            if (r > 0.01)
            {
                //if (storage.Count == max)
                //    storage.Dequeue();
                //storage.Enqueue(r);
                //valid = storage.Average();
                valid = r;
                validTS = t;
            }
            else if ((t - validTS).TotalSeconds > wait) // 5 is max observed
                return r;
            return valid;
        }
    }

    public class InventoryItem : Info
    {
        public readonly MyItemType Type;
        public string ID => Tag + '!' + Type.SubtypeId;
        public readonly string Tag, Format;
        protected readonly InventoryUtilities Inventory;
        public InventoryItem(string t, string st, string f = "")
        {
            Tag = t;
            Format = f;
            if (st == InventoryUtilities.J)
                return;
            Type = new MyItemType(InventoryUtilities.myObjectBuilder + '_' + Tag, st);
        }
        protected InventoryItem(InventoryItem d)
        {
            Tag = d.Tag;
            Format = d.Format;
            Type = d.Type;
        }
        public void AddAmount(int i) => value += i;
        public void Clear() => value = 0;

        public string ToString(bool f = false) => f ? value.ToString(Format) : value.ToString();
    }

    public class ItemInfo : InventoryItem
    {
        List<IMyTerminalBlock> Inventories = null;
        bool manifest;
        List<string> Names = null;
        public ItemInfo(string t, string st, List<IMyTerminalBlock> i = null, List<string> n = null) : base(new InventoryItem(t, st))
        {
            if (i != null)
                Inventories = i.ToList();
            manifest = n != null;
            if (manifest)
                Names = n.ToList();
        }
        public ItemInfo(InventoryItem d, List<IMyTerminalBlock> i = null) : base(d)
        {
            if (i != null)
                Inventories = i.ToList();
        }
        public override void Update()
        {
            //DebugString = "";
            int i = Inventories.Count - 1, q;
            value = 0;
            //var sum = 0d;
            for (; i >= 0; i--)
            //using (Debug.Measure((key) => { DebugString += $"{i}. Polling {inv.CustomName}, {key.TotalMilliseconds} ms\n"; sum += key.TotalMilliseconds; }))
            {
                q = 0;
                if (Inventories[i].Closed)
                    Inventories.RemoveAtFast(i);
                var inv = Inventories[i]?.GetInventory();
                if (inv == null)
                    continue;
                q = inv.GetItemAmount(Type).ToIntSafe();
                value += q;
            }
            //DebugString += $"SUM = {sum} ms";
        }
    }

    public class InventoryUtilities : UtilityBase
    {
        public static string myObjectBuilder = "MyObjectBuilder", J = "JIT";
        public string Section, DebugString;   
        int updateStep = 5, ibPtr;
        bool ignoreTanks, ignoreGuns, ignoreSMConnectors, vanilla;
        public bool needsUpdate;
        List<IMyTerminalBlock> InventoryBlocks = new List<IMyTerminalBlock>();
        public int Count => InventoryBlocks.Count;
        Dictionary<long, string[]>
            itemKeys = new Dictionary<long, string[]>(),
            itemTags = new Dictionary<long, string[]>();
        List<MyInventoryItem> ItemScan = new List<MyInventoryItem>();
        // you know i had to do it to em
        public SortedList<string, InventoryItem> Items = new SortedList<string, InventoryItem>();

        DebugAPI Debug;
        public int Pointer => ibPtr + 1;

        public InventoryUtilities(string s, DebugAPI api = null)
        {
            Section = s;
            Debug = api;
        }

        #region inventorystuff
        //--------------------------------------------------
        // [COMPONENTS]
        //--------------------------------------------------

        // Bulletproof Glass
        //      MyObjectBuilder_Component/BulletproofGlass
        // Canvas
        //      MyObjectBuilder_Component/Canvas
        // Computer
        //      MyObjectBuilder_Component/Computer
        // Construction Comp.
        //      MyObjectBuilder_Component/Construction
        // Detector Comp.
        //      MyObjectBuilder_Component/Detector
        // Display
        //      MyObjectBuilder_Component/Display
        // Engineer Plushie
        //      MyObjectBuilder_Component/EngineerPlushie
        // Explosives
        //      MyObjectBuilder_Component/Explosives
        // Girder
        //      MyObjectBuilder_Component/Girder
        // Gravity Comp.
        //      MyObjectBuilder_Component/GravityGenerator
        // Interior Plate
        //      MyObjectBuilder_Component/InteriorPlate
        // Large Steel Tube
        //      MyObjectBuilder_Component/LargeTube
        // Medical Comp.
        //      MyObjectBuilder_Component/Medical
        // Metal Grid
        //      MyObjectBuilder_Component/MetalGrid
        // Motor
        //      MyObjectBuilder_Component/Motor
        // Power Cell
        //      MyObjectBuilder_Component/PowerCell
        // Radio-comm Comp.
        //      MyObjectBuilder_Component/RadioCommunication
        // Reactor Comp.
        //      MyObjectBuilder_Component/Reactor
        // Saberoid Plushie
        //      MyObjectBuilder_Component/SabiroidPlushie
        // Small Steel Tube
        //      MyObjectBuilder_Component/SmallTube
        // Solar Cell
        //      MyObjectBuilder_Component/SolarCell
        // Steel Plate
        //      MyObjectBuilder_Component/SteelPlate
        // Superconductor
        //      MyObjectBuilder_Component/Superconductor
        // Thruster Comp.
        //      MyObjectBuilder_Component/Thrust
        // Zone Chip
        //      MyObjectBuilder_Component/ZoneChip

        //--------------------------------------------------
        // [AMMOMAGAZINES]
        //--------------------------------------------------

        // 5.56x45mm NATO magazine [LEGACY]
        //      MyObjectBuilder_AmmoMagazine/NATO_5p56x45mm
        // Artillery Shell
        //      MyObjectBuilder_AmmoMagazine/LargeCalibreAmmo
        // Assault Cannon Shell
        //      MyObjectBuilder_AmmoMagazine/MediumCalibreAmmo
        // Autocannon Magazine
        //      MyObjectBuilder_AmmoMagazine/AutocannonClip
        // Gatling Ammo Box
        //      MyObjectBuilder_AmmoMagazine/NATO_25x184mm
        // Large Railgun Sabot
        //      MyObjectBuilder_AmmoMagazine/LargeRailgunAmmo
        // MR-20 Rifle Magazine
        //      MyObjectBuilder_AmmoMagazine/AutomaticRifleGun_Mag_20rd
        // MR-30E Rifle Magazine
        //      MyObjectBuilder_AmmoMagazine/UltimateAutomaticRifleGun_Mag_30rd
        // MR-50A Rifle Magazine
        //      MyObjectBuilder_AmmoMagazine/RapidFireAutomaticRifleGun_Mag_50rd
        // MR-8P Rifle Magazine
        //      MyObjectBuilder_AmmoMagazine/PreciseAutomaticRifleGun_Mag_5rd
        // Rocket
        //      MyObjectBuilder_AmmoMagazine/Missile200mm
        // S-10 Pistol Magazine
        //      MyObjectBuilder_AmmoMagazine/SemiAutoPistolMagazine
        // S-10E Pistol Magazine
        //      MyObjectBuilder_AmmoMagazine/ElitePistolMagazine
        // S-20A Pistol Magazine
        //      MyObjectBuilder_AmmoMagazine/FullAutoPistolMagazine
        // Small Railgun Sabot
        //      MyObjectBuilder_AmmoMagazine/SmallRailgunAmmo

        //--------------------------------------------------
        // [TOOLS/MISC]
        //--------------------------------------------------

        // Clang Kola
        //      MyObjectBuilder_ConsumableItem/ClangCola
        // Cosmic Coffee
        //      MyObjectBuilder_ConsumableItem/CosmicCoffee
        // Datapad
        //      MyObjectBuilder_Datapad/Datapad
        // Elite Grinder
        //      MyObjectBuilder_PhysicalGunObject/AngleGrinder4Item
        // Elite Hand Drill
        //      MyObjectBuilder_PhysicalGunObject/HandDrill4Item
        // Elite Welder
        //      MyObjectBuilder_PhysicalGunObject/Welder4Item
        // Enhanced Grinder
        //      MyObjectBuilder_PhysicalGunObject/AngleGrinder2Item
        // Enhanced Hand Drill
        //      MyObjectBuilder_PhysicalGunObject/HandDrill2Item
        // Enhanced Welder
        //      MyObjectBuilder_PhysicalGunObject/Welder2Item
        // Grinder
        //      MyObjectBuilder_PhysicalGunObject/AngleGrinderItem
        // Hand Drill
        //      MyObjectBuilder_PhysicalGunObject/HandDrillItem
        // Hydrogen Bottle
        //      MyObjectBuilder_GasContainerObject/HydrogenBottle
        // Medkit
        //      MyObjectBuilder_ConsumableItem/Medkit
        // MR-20 Rifle
        //      MyObjectBuilder_PhysicalGunObject/AutomaticRifleItem
        // MR-30E Rifle
        //      MyObjectBuilder_PhysicalGunObject/UltimateAutomaticRifleItem
        // MR-50A Rifle
        //      MyObjectBuilder_PhysicalGunObject/RapidFireAutomaticRifleItem
        // MR-8P Rifle
        //      MyObjectBuilder_PhysicalGunObject/PreciseAutomaticRifleItem
        // Oxygen Bottle
        //      MyObjectBuilder_OxygenContainerObject/OxygenBottle
        // Package
        //      MyObjectBuilder_Package/Package
        // Powerkit
        //      MyObjectBuilder_ConsumableItem/Powerkit
        // PRO-1 Rocket Launcher
        //      MyObjectBuilder_PhysicalGunObject/AdvancedHandHeldLauncherItem
        // Proficient Grinder
        //      MyObjectBuilder_PhysicalGunObject/AngleGrinder3Item
        // Proficient Hand Drill
        //      MyObjectBuilder_PhysicalGunObject/HandDrill3Item
        // Proficient Welder
        //      MyObjectBuilder_PhysicalGunObject/Welder3Item
        // RO-1 Rocket Launcher
        //      MyObjectBuilder_PhysicalGunObject/BasicHandHeldLauncherItem
        // S-10 Pistol
        //      MyObjectBuilder_PhysicalGunObject/SemiAutoPistolItem
        // S-10E Pistol
        //      MyObjectBuilder_PhysicalGunObject/ElitePistolItem
        // S-20A Pistol
        //      MyObjectBuilder_PhysicalGunObject/FullAutoPistolItem
        // Space Credit
        //      MyObjectBuilder_PhysicalObject/SpaceCredit
        // Welder
        //      MyObjectBuilder_PhysicalGunObject/WelderItem
        #endregion

        public void ScanInventories()
        {
            string key;
            ItemScan.Clear();
            var inv = InventoryBlocks[ibPtr]?.GetInventory();
            if (inv == null)
            {
                InventoryBlocks.RemoveAtFast(ibPtr);
                return;
            }  
            if (inv.ItemCount == 0)
                return;
            inv.GetItems(ItemScan);
            for (int j = 0; j < ItemScan.Count; j++)
            {
                key = ItemScan[j].Type.TypeId.Substring(16) + '!' + ItemScan[j].Type.SubtypeId;
                if (Items.ContainsKey(key))
                    Items[key].AddAmount(inv.GetItemAmount(Items[key].Type).ToIntSafe());
            }
        }

        public static bool TryGetUseRate(ref UseRate r, ref ItemInfo item, out double rate)
        {
            rate = r.Rate(item.Data);
            if (rate <= 0) return false;
            return true;
        }

        void AddItemGroup(long id, string key)
        {
            using (var p = new iniWrap())
            {
                MyIniParseResult result;
                if (p.CustomData(GCM.Me, out result))
                    if (p.HasKey(Section, key))
                    {
                        var s = p.String(Section, key).Split('\n');
                        if (s.Length > 0)
                        {
                            var k = new string[s.Length];
                            var t = new string[s.Length];
                            for (int i = 0; i < s.Length; i++)
                            {
                                var a = s[i].Split(cmd);
                                if (!Items.ContainsKey(a[1] + cmd + a[2]))
                                    try
                                    {
                                        InventoryItem item = null;
                                        if (a.Length == 4)
                                            item = new InventoryItem(a[1], a[2], a[3]);
                                        else if (a.Length == 3)
                                            item = new InventoryItem(a[1], a[2]);
                                        else
                                        {
                                            t[i] = "";
                                            continue;
                                        }
                                        Items.Add(item.ID, item);
                                    }
                                    catch (Exception) // keen dict problem
                                    { continue; }
                                k[i] = a[1] + cmd + a[2];
                                if (a.Length != 3 && a.Length != 4)
                                    t[i] = "";
                                else
                                    t[i] = a[0] + " ";
                            }
                            if (!itemKeys.ContainsKey(id))
                            {
                                itemKeys.Add(id, k);
                                itemTags.Add(id, t);
                            }
                        }
                    }
                    else
                    {
                        if (key != J) throw new Exception($"key {key} for command !itemslist not found in PB custom data.");
                    }
                else throw new Exception(result.Error);
            }

        }

        void UpdateItemString(long id, ref SpriteData d)
        {
            if (!itemKeys.ContainsKey(id))
                return;
            var itm = Items[itemKeys[id][0]];
            d.SetData(itm.Data, itemTags[id][0], itm.Format);
            //if (itemKeys[id].Length == 1)
            //    return;
            for (int i = 1; i < itemKeys[id].Length; i++)
                d.Data += $"\n{itemTags[id][i]}{Items[itemKeys[id][i]].ToString(true)}";
        }

        #region UtilityBase

        public override void Reset(GraphicsManager manager, MyGridProgram program)
        {
            using (var p = new iniWrap())
            if (p.CustomData(manager.Me))
            {
                vanilla = p.Bool(Section, "nilla", false);
                ignoreTanks = p.Bool(Section, "ignoreTanks", true);
                ignoreSMConnectors = p.Bool(Section, "ignoreSMC", true);
                ignoreGuns = vanilla & p.Bool(Section, "nogunz", true);
                updateStep = p.Int(Section, "invStep", 17);
            }
            base.Reset(manager, program);
        }

        public override void GetBlocks()
        {
            InventoryBlocks.Clear();
            TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                if (b.BlockDefinition.SubtypeId == "LargeInteriorTurret")
                    return false;
                if (ignoreTanks && b is IMyGasTank)
                    return false;
                if (ignoreGuns && b is IMyUserControllableGun)
                    return false;
                if (ignoreSMConnectors && b is IMyShipConnector && b.WorldVolume.Radius <= 0.5)
                    return false;
                if (b.HasInventory && b.IsSameConstructAs(GCM.Me))
                    InventoryBlocks.Add(b);
                return true;
            });
        }

        void JIT()
        {
            // JIT
            var d = 0d;
            var l = new List<IMyTerminalBlock>();
            var jitItem = new InventoryItem("Ingot", J);
            jitItem.AddAmount(0);
            jitItem.Clear();
            var jitinfo = new ItemInfo(jitItem);
            var jitR = new UseRate(J);
            AddItemGroup(jitSprite.uID, J);
            UpdateItemString(jitSprite.uID, ref jitSprite);
            TryGetUseRate(ref jitR, ref jitinfo, out d);
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            JIT();

            itemKeys.Clear();
            itemTags.Clear();
            Items.Clear();
            
            commands["!item"] = b =>
            {
                if (GCM.justStarted && b.Data != invalid)
                    try
                    {
                        if (b.Data.Contains(cmd))
                        {
                            if (!Items.ContainsKey(b.Data))
                            {
                                b.Data = b.Data.Trim();
                                var s = b.Data.Split(cmd);
                                if (s.Length == 2)
                                    Items.Add(b.Data, new InventoryItem(s[0], s[1]));                                                                 
                            }
                            if (!itemKeys.ContainsKey(b.uID))
                            {
                                itemKeys.Add(b.uID, new string[] { b.Data });
                                b.Data = invalid;
                            }
                        }
                    }
                    catch (Exception)
                    { throw new Exception($"\nError in data for sprite {b.Name} - invalid item key: {b.Data}."); }
                b.SetData(Items[itemKeys[b.uID][0]].Data);
            };

            commands["!itemslist"] = b =>
            {
                if (GCM.justStarted && b.Data != "" && b.Data != invalid)
                {
                    AddItemGroup(b.uID, b.Data);
                    b.Data = invalid;
                    return;
                }
                UpdateItemString(b.uID, ref b);
            };

            commands["!invdebug"] = b =>
            {
                if (!GCM.justStarted)
                    return;
                b.Data = $"INVENTORIES = {InventoryBlocks.Count}\n";
                b.Data += ignoreGuns ? "WEAPONS NOT COUNTED" : "CHECKING WEAPONS";
            };
        }

        public override void Update()
        {
            if (Items.Count == 0)
            {
                needsUpdate = false;
                return;
            }
            if (ibPtr == 0)
                foreach (var itm in Items.Values)
                    itm.Clear();

            int m = Math.Min(InventoryBlocks.Count, ibPtr + updateStep);
            for (; ibPtr < m; ibPtr+= 1)
                ScanInventories();
            if (m == InventoryBlocks.Count)
            {
                ibPtr = 0;
                needsUpdate = false;
            }
        }

        #endregion
    }

    public class BlockInfo<T>// : IInfo
        where T : IMyFunctionalBlock
    {

    }


    public class BlockUtilities : UtilityBase
    {
        readonly string tag;
        List<IMyTerminalBlock> AllBlocks = new List<IMyTerminalBlock>();
        List<IMyBlockGroup> AllGroups = new List<IMyBlockGroup>();
        Dictionary<long, long[]> BlockKeys = new Dictionary<long, long[]>();
        // TODO: make a new class or something (if i dont kill myself this week)
        Dictionary<long, IMyFunctionalBlock> BlockData = new Dictionary<long, IMyFunctionalBlock>(); //temp

        public BlockUtilities(string s)
        {
            name = "Blocks";
            tag = s;
        }

        #region UtilityBase

        public override void GetBlocks()
        {
            AllBlocks.Clear();
            BlockKeys.Clear();
            TerminalSystem.GetBlocksOfType(AllBlocks);
            TerminalSystem.GetBlockGroups(AllGroups);
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!blkstatus", b =>
            {
                if (GCM.justStarted && !BlockKeys.ContainsKey(b.uID))
                {
                    var blk = AllBlocks.Find(c => c.CustomName == b.Data);
                    if (blk != null && blk is IMyFunctionalBlock)
                    {
                        BlockData.Add(blk.EntityId, (IMyFunctionalBlock)blk);
                        BlockKeys.Add(b.uID, new long[] { blk.EntityId });
                    }
                }
                if (BlockKeys.ContainsKey(b.uID))
                    b.Data = BlockData[BlockKeys[b.uID][0]].IsFunctional ? "ON" : "OFF";
            });
        }
        #endregion
    }

    public class FlightUtilities : UtilityBase
    {
        List<IMyJumpDrive> JumpDrives = new List<IMyJumpDrive>();
        double lastDist, maxDist, lastAccel, maxAccel;
        const double dev = 0.01;
        readonly string tag, ctrl, fmat;
        string std;
        DateTime stopTS, accelTS; // fuck you
        Vector3D VZed = Vector3D.Zero, lastVel, grav;
        Info jump;

        public FlightUtilities(string s, string f = "flightFMAT")
        {
            name = "Flight";
            tag = s;
            fmat = f;
        }

        #region UtilityBase

        public override void GetBlocks()
        {
            using (var p = new iniWrap())
            {
                p.CustomData(GCM.Me);
                std = p.String(tag, fmat, "0000");
                TerminalSystem.GetBlocksOfType(JumpDrives, b => b.IsSameConstructAs(Program.Me));
            }
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {

            jump = new Info("jd%", JumpCharge);
            // JIT
            jump.Update();
            GetHorizonAngle();
            Accel();
            GetAlt(MyPlanetElevation.Sealevel);
            StoppingDist();

            commands.Add("!horiz", b =>
                 Validate(GetHorizonAngle(), ref b, "-#0.##; +#0.##" + "°"));

            commands.Add("!c-alt", b =>
                Validate(GetAlt(MyPlanetElevation.Sealevel), ref b, std));

            commands.Add("!s-alt", b =>
                Validate(GetAlt(MyPlanetElevation.Surface), ref b, std));

            commands.Add("!stop", b =>
                Validate(StoppingDist(), ref b, std));

            commands.Add("!accel", b => Validate(Accel(), ref b, std));

            commands.Add("!damp", b =>
            { // this should not be a problem (famous last words)
                b.Data = GCM.Controller.DampenersOverride ? "ON" : "OFF";
            });

            commands.Add("!jcharge%", b => b.SetData(jump.Data, "#0.#%"));

            commands.Add("!jchargeb", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, jump.Data);
            });

        }

        public override void Update()
        {

            jump.Update();
        }

        #endregion

        bool GravCheck(out Vector3D grav) //wanted something nice and neat
        {
            grav = GCM.Controller.GetNaturalGravity();
            if (grav == VZed)
                return false;
            return true;
        }

        double GetHorizonAngle()
        {
            if (!GravCheck(out grav))
                return bad;
            if (grav == VZed)
                return bad;
            grav.Normalize();
            return Math.Asin(MathHelper.Clamp(GCM.Controller.WorldMatrix.Forward.Dot(grav), -1, 1));
        }

        double GetAlt(MyPlanetElevation elevation)
        {
            if (!GravCheck(out grav))
                return bad;
            var alt = 0d;
            if (GCM.Controller.TryGetPlanetElevation(elevation, out alt))
                return alt;
            return bad;
        }

        double Accel()
        {
            var ret = 0d;
            var ts = DateTime.Now;
            if (GCM.justStarted)
                return bad;
            var current = GCM.Controller.GetShipVelocities().LinearVelocity;

            if (current.Length() < 0.037)
            {
                lastAccel = 0;
                return bad;
            }
            var mag = (current - lastVel).Length();
            if (mag > dev)
                ret += mag / (ts - accelTS).TotalSeconds;

            if (ret > maxAccel) maxAccel = ret;
            lastAccel = ret;
            accelTS = ts;
            return ret <= dev ? maxAccel : ret;
        }

        double StoppingDist()
        {
            double
                ret = lastDist,
                a = 0;
            var ts = DateTime.Now;

            var current = GCM.Controller.GetShipVelocities().LinearVelocity;
            if (!GCM.justStarted)
            {
                if (!GCM.Controller.DampenersOverride || current.Length() < 0.037) return bad;
                var mag = (lastVel - current).Length();
                if (mag > dev)
                {
                    a = mag / (ts - stopTS).TotalSeconds;
                    ret = current.Length() * current.Length() / (2 * a);
                    lastDist = ret;
                }
            }
            if (ret > maxDist) maxDist = ret;
            lastVel = current;
            stopTS = ts;
            return a <= dev ? maxDist : ret;
        }

        double JumpCharge()
        {
            if (JumpDrives.Count == 0) return 0f;
            float charge, max = 0f;
            charge = max;
            for (int i = 0; i < JumpDrives.Count; i++)
            {
                //if (JumpDrives[i] == null) continue;
                charge += JumpDrives[i].CurrentStoredPower;
                max += JumpDrives[i].MaxStoredPower;
            }
            return charge / max;
        }
    }

    public class ThrustUtilities : UtilityBase
    {
        readonly string tag;
        const string
            F = "fwd",
            B = "bwd",
            L = "lft",
            R = "rgt",
            U = "upw",
            D = "dwn";
        List<IMyThrust> AllThrusters = new List<IMyThrust>();
        SortedList<Base6Directions.Direction, Info> groups = new SortedList<Base6Directions.Direction, Info>();
        Dictionary<Base6Directions.Direction, IMyThrust[]> dirThrust = new Dictionary<Base6Directions.Direction, IMyThrust[]>();
        Dictionary<Base6Directions.Direction, int> maxThrust = new Dictionary<Base6Directions.Direction, int>();
        Dictionary<long, Base6Directions.Direction> idToDir = new Dictionary<long, Base6Directions.Direction>();
        Dictionary<long, IMyThrust> singles = new Dictionary<long, IMyThrust>();

        public ThrustUtilities(string t)
        {
            name = "Thrust";
            tag = t;
        }

        #region UtilityBase

        public override void GetBlocks()
        {
            groups.Clear();
            AllThrusters.Clear();
            maxThrust.Clear();
            dirThrust.Clear();
            TerminalSystem.GetBlocksOfType(AllThrusters, (thrust) => thrust.IsSameConstructAs(GCM.Me));

            if (GCM.Controller != null)
            {
                IMyShipController temp = null;
                bool main = GCM.Controller.IsMainCockpit;
                if (!main)
                {
                    TerminalSystem.GetBlocksOfType<IMyShipController>(null, b =>
                    {
                        if (b.IsMainCockpit)
                            temp = b;
                        return false;
                    });
                    GCM.Controller.IsMainCockpit = true;
                }
                var list = new List<IMyThrust>();
                createGroup(ref list, getDir(F));
                createGroup(ref list, getDir(B));
                createGroup(ref list, getDir(L));
                createGroup(ref list, getDir(R));
                createGroup(ref list, getDir(U));
                createGroup(ref list, getDir(D));

                foreach (var dir in dirThrust.Keys)
                    groups.Add(dir, new Info("thr" + dir, () => getThrust(dir)));
                if (!main)
                {
                    if (temp != null)
                        temp.IsMainCockpit = true;
                    GCM.Controller.IsMainCockpit = false;
                }
            }
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            // JIT
            getThrust(getDir(F));

            commands.Add("!cirthr%", b =>
            {
                bool k = singles.ContainsKey(b.uID);
                if (GCM.justStarted && !k)
                    TerminalSystem.GetBlocksOfType<IMyThrust>(null, t =>
                    {
                        if (t.CustomName.Contains(b.Name))
                        {
                            singles[b.uID] = t;
                            Lib.GraphStorage[b.uID] = MyTuple.Create(false, b.sX);
                        }
                        return false;
                    });
                b.sX = b.sY = (.625f + singles[b.uID].CurrentThrust / singles[b.uID].MaxThrust) * Lib.GraphStorage[b.uID].Item2;
            });
            commands.Add("!curthr%", b =>
            {
                bool k = singles.ContainsKey(b.uID);
                if (GCM.justStarted && !k)
                    TerminalSystem.GetBlocksOfType<IMyThrust>(null, t =>
                    {
                        if (t.CustomName.Contains(b.Data))
                            singles[b.uID] = t;
                        return false;
                    });
                b.Data = singles[b.uID].CurrentThrustPercentage.ToString("#0.#%");
            });
            commands.Add("!movethr", b => curThrust(ref b));
            commands.Add("!movethr%", b => curThrust(ref b, true));
            commands.Add("!a", b => curThrust(ref b, a: true));
        }

        public override void Update()
        {
            foreach (var g in groups.Values)
                g.Update();
        }

        #endregion
        Base6Directions.Direction getDir(string s)
        {
            switch (s)
            {
                case F:
                default:
                    return Base6Directions.Direction.Forward;
                case B:
                    return Base6Directions.Direction.Backward;
                case L:
                    return Base6Directions.Direction.Left;
                case R:
                    return Base6Directions.Direction.Right;
                case U:
                    return Base6Directions.Direction.Up;
                case D:
                    return Base6Directions.Direction.Down;
            }
        }

        void createGroup(ref List<IMyThrust> l, Base6Directions.Direction dir)
        {
            l.Clear();
            var v = Base6Directions.GetIntVector(dir);
            foreach (var t in AllThrusters)
                if (t.GridThrustDirection == v)
                    l.Add(t);

            // you never know...
            if (l.Count > 0)
            {
                var a = l.ToArray();
                int sum = 0, i = 0;
                for (; i < a.Length; i++)
                    sum += Convert.ToInt32(a[i].MaxThrust);
                dirThrust.Add(dir, a);
                maxThrust.Add(dir, sum);
            }
        }
        double getThrust(Base6Directions.Direction dir)
        {
            var ret = 0d;
            if (!dirThrust.ContainsKey(dir))
                return ret;
            var a = dirThrust[dir];
            for (int i = 0; i < a.Length; i++)
                ret += a[i].CurrentThrust;
            return ret;
        }
        void curThrust(ref SpriteData b, bool pct = false, bool a = false)
        {
            var d = -GCM.Controller?.MoveIndicator;
            if (d == Vector3.Zero || d == null)
            {
                b.Data = invalid;
                return;
            }
            var dir = Base6Directions.GetClosestDirection(d.Value);
            var cur = groups[dir].Data;
            if (pct)
                b.SetData(cur / maxThrust[dir], "#0.#%");
            else if (a)
                b.SetData(cur / GCM.Controller.CalculateShipMass().TotalMass, "#0.#");
            else
                b.SetData(groups[dir].Data);
        }

    }



    public class PowerUtilities : UtilityBase
    {
        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
        List<IMyReactor> Reactors = new List<IMyReactor>();
        List<IMyPowerProducer>
            Engines = new List<IMyPowerProducer>(),
            AllPower = new List<IMyPowerProducer>();
        UseRate U;
        ItemInfo Fuel;
        double pmax;
        Info batt, pwr;
        const string I = "ON", O = "OFF", ur = "Ingot!Uranium";
        public PowerUtilities()
        {
            name = "Power";
            U = new UseRate(ur);
        }
        #region UtilityBase
        public override void Reset(GraphicsManager m, MyGridProgram p)
        {
            base.Reset(m, p);
            var a = ur.Split(cmd);
            Fuel = new ItemInfo(a[0], a[1], Reactors.ToList<IMyTerminalBlock>());
        }

        public override void GetBlocks()
        {
            Batteries.Clear();
            Reactors.Clear();
            Engines.Clear();
            AllPower.Clear();
            TerminalSystem.GetBlocksOfType(Batteries, (battery) => battery.IsSameConstructAs(GCM.Me));
            TerminalSystem.GetBlocksOfType(Reactors, (reactor) => reactor.IsSameConstructAs(GCM.Me));
            TerminalSystem.GetBlocksOfType(Engines, (generator) => generator.IsSameConstructAs(GCM.Me) && generator.CustomName.Contains("Engine"));
            TerminalSystem.GetBlocksOfType(AllPower, (power) => power.IsSameConstructAs(GCM.Me));
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            batt = new Info("bc%", BatteryCharge);
            pwr = new Info("pwr", Output);

            commands.Add("!bcharge%", (b) =>
                Validate(batt.Data, ref b, "#0.#%"));

            commands.Add("!bchargeb", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, batt.Data);
            });
            commands.Add("!gridgen%", b => b.SetData(pwr.Data / pmax, "#0.#%"));
            commands.Add("!fuel", b => b.SetData(Fuel.Data, Fuel.Format));
            commands.Add("!fission", b =>
            {
                var rate = 0d;
                if (GCM.justStarted)
                    b.Data = InventoryUtilities.TryGetUseRate(ref U, ref Fuel, out rate) ? $"{rate:000.0} KG/S" : "0 KG/S";
            });
            commands.Add("!gridcap", b => b.SetData(1 - (pwr.Data / pmax), "#0.#%"));
        }

        public override void Update()
        {
            Fuel.Update();
            batt.Update();
            pwr.Update();
        }

        #endregion
        double BatteryCharge()
        {
            if (Batteries.Count == 0)
                return bad;
            var charge = 0d;
            var total = charge;
            for (int i = 0; i < Batteries.Count; i++)
            {
                //if (battery == null) continue;
                charge += Batteries[i].CurrentStoredPower;
                total += Batteries[i].MaxStoredPower;
            }
            return (charge / total);
        }

        double Output()
        {
            var sum = 0f;
            pmax = 0;
            for (int i = 0; i < AllPower.Count; i++)
            {
                if (!AllPower[i].IsFunctional || !AllPower[i].Enabled)
                    continue;
                sum += AllPower[i].CurrentOutput;
                pmax += AllPower[i].MaxOutput;
            }
            return sum;

        }
    }

    public class WeaponUtilities// : UtilityBase
    {
        Dictionary<long, string[]> wpnTags = new Dictionary<long, string[]>();
        Dictionary<long, Info> wpnData = new Dictionary<long, Info>(); // sprite uid to cached gun stats
        Dictionary<long, IMyUserControllableGun> wpns = new Dictionary<long, IMyUserControllableGun>(); // eid to gun
    }

    // TODO: THIS SYSTEM IS ASS
    public class CoreWeaponUtilities : UtilityBase
    {
        Dictionary<long, MyTuple<string, IMyTerminalBlock[]>> WeaponGroups = new Dictionary<long, MyTuple<string, IMyTerminalBlock[]>>();
        Dictionary<long, string> tagStorage = new Dictionary<long, string>();
        List<IMyTerminalBlock> wcWeapons = new List<IMyTerminalBlock>();
        WCPBAPI api = null;
        bool man = false;

        public CoreWeaponUtilities()
        {
            name = "Weapons";
        }

        #region UtilityBase
        public override void Reset(GraphicsManager m, MyGridProgram p)
        {
            base.Reset(m, p);
            WeaponGroups.Clear();
            man = true;
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!wpnrdy", (b) =>
            {
                if (WCPBAPI.Activate(Program.Me, ref api))
                {
                    if (!WeaponGroups.ContainsKey(b.uID)) AddWeaponGroup(b);
                    else UpdateWeaponReady(ref b);
                }
            });
            commands.Add("!tgt", (b) =>
            {
                if (WCPBAPI.Activate(Program.Me, ref api))
                {
                    var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
                    b.Data = focus.HasValue ? focus.Value.Name : "NO TARGET";
                }
            });

            commands.Add("!tgtdist", (b) =>
            {
                if (WCPBAPI.Activate(Program.Me, ref api))
                {
                    var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
                    b.Data = focus.HasValue ? (focus.Value.Position - Program.Me.CubeGrid.GetPosition()).Length().ToString("4:####") : "NO TARGET";
                }
            });

            commands.Add("!heats%", (b) =>
            {
                if (WCPBAPI.Activate(Program.Me, ref api))
                {
                    // todo
                }
            });
        }

        public override void GetBlocks()
        {
            if (!WCPBAPI.Activate(Program.Me, ref api))
                return;
            wcWeapons.Clear();
            foreach (var t in tagStorage.Values)
                TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
                {
                    if (b.IsSameConstructAs(Program.Me) && b.CustomName.Contains(t))
                        wcWeapons.Add(b);
                    return true;
                });
        }

        public override void Update() { }

        #endregion
        void AddWeaponGroup(SpriteData d)
        {
            var list = new List<IMyTerminalBlock>();
            string[] dat = d.Data.Split(cmd);
            if (!tagStorage.ContainsKey(d.uID))
                tagStorage.Add(d.uID, dat[0]);
            TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                if (b.IsSameConstructAs(Program.Me) && b.CustomName.Contains(dat[0]))
                    list.Add(b);
                return true;
            });
            if (list.Count > 0) WeaponGroups.Add(d.uID, new MyTuple<string, IMyTerminalBlock[]>(dat[1], list.ToArray()));
        }

        void UpdateWeaponReady(ref SpriteData d)
        {
            if (api == null) return;
            int count = 0;
            foreach (var wpn in WeaponGroups[d.uID].Item2)
                if (api.IsWeaponReadyToFire(wpn)) count++;
            d.Data = $"{WeaponGroups[d.uID].Item1} {count}/{WeaponGroups[d.uID].Item2.Length} RDY";
        }

    }
}
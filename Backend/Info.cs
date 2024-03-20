using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRageMath;
using VRageRender;

namespace IngameScript
{

    public class Info
    {
        public virtual double Data { get { Active = true; return data; } }
        private double data;
        private bool Active = false;
        private Func<double> update;
        public Info(Func<double> u)
        {
            update = u;
        }
        public virtual void Update()
        {
            if (!Active) return;
            data = update.Invoke();
        }
    }

    public abstract class InfoUtility
    {
        #region fields

        public static bool justStarted;
        public static Dictionary<long, MyTuple<bool, float>> GraphStorage = new Dictionary<long, MyTuple<bool, float>>();
        protected MyGridProgram Program;
        protected IMyGridTerminalSystem TerminalSystem;
        protected const char
            cmd = '!',
            space = ' ',
            bar = 'b',
            pct = '%';
        protected string invalid = "••", name;
        public string Name { get { return name.ToUpper(); } }
        protected StringBuilder Builder;
        protected double bad = double.NaN;
        public static void ApplyBuilder(SpriteData d)
        {
            StringBuilder builder = new StringBuilder(d.Data);
            if (d.Prepend != null)
                builder.Insert(0, d.Prepend);
            if (d.Append != null)
                builder.Append(d.Append);
            d.Data = builder.ToString();
        }
        #endregion

        public InfoUtility()
        {
            Builder = new StringBuilder();
        }
        public virtual void Reset(MyGridProgram program)
        {
            GraphStorage.Clear();
            Program = program;
            TerminalSystem = program.GridTerminalSystem;
            justStarted = true;
        }

        public virtual void Update()
        {
        }

        public abstract void Setup(ref Dictionary<string, Action<SpriteData>> commands);


    }

    // SO...command formatting. Depends on the general command, but here's the idea
    // this is all for the K_DATA field of the sprite.
    // <required param 1>$<required param 2>$...$<required param n>

    public class GasUtilities : InfoUtility
    {
        InventoryUtilities Inventory;
        public List<IMyGasTank>
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
        UseRate Ice, H2;
        Dictionary<long, int> hTime = new Dictionary<long, int>();
        Queue<double> savedIce = new Queue<double>(10);
        Info
            h2, o2;

        public GasUtilities(ref InventoryUtilities inv)
        {
            name = "Gas";
            Ice = new UseRate("Ice");
            Inventory = inv;
        }

        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            HydrogenTanks.Clear();
            OxygenTanks.Clear();
            TerminalSystem.GetBlocksOfType(HydrogenTanks, (b) => b.BlockDefinition.SubtypeId.Contains("Hyd"));
            TerminalSystem.GetBlocksOfType(OxygenTanks, (b) => !HydrogenTanks.Contains(b));
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {           //s fucking hate this, this sucks ass
            h2 = new Info(HydrogenStatus);
            o2 = new Info(OxygenStatus);

            commands.Add("!h2%", (b) =>
            b.Data = h2.Data.ToString("#0.#%"));

            commands.Add("!o2%", (b) =>
                b.Data = o2.Data.ToString("#0.#%"));
            commands.Add("!h2b", (b) =>
            {
                if (justStarted) Util.CreateBarGraph(ref b);
                Util.UpdateBarGraph(ref b, h2.Data);
            });
            commands.Add("!o2b", (b) =>
            {
                if (justStarted) Util.CreateBarGraph(ref b);
                Util.UpdateBarGraph(ref b, o2.Data);
            });
            commands.Add("!h2t", (b) =>
            {
                if (justStarted)
                {
                    if (b.Data == "sec")
                        hTime.Add(b.uID, 1);
                    else if (b.Data == "min")
                        hTime.Add(b.uID, 2);
                    else hTime.Add(b.uID, 0);
                }
                if (hTime.ContainsKey(b.uID))
                    b.Data = HydrogenTime(hTime[b.uID]);
            });

            commands.Add("!ice", (b) =>
            {
                var rate = 0d;
                if (justStarted && !Inventory.Items.ContainsKey(Ice.iID))
                    Inventory.Items.Add(Ice.iID, new ItemInfo(new InventoryItem("Ore", Ice.iID), Inventory));
                b.Data = Inventory.TryGetUseRate<IMyTerminalBlock>(Ice, ref savedIce, out rate) ? $"{rate:000.0} kg/s" : "0 kg/s";
            });
        }

        public override void Update()
        {
            h2.Update();
            o2.Update();
        }

        #endregion
        double HydrogenStatus()
        {
            var amt = 0d;
            var total = amt;
            foreach (var tank in HydrogenTanks)
            {
                amt += tank.FilledRatio * tank.Capacity;
                total += tank.Capacity;
            }
            return amt / total;
        }

        double OxygenStatus()
        {
            var amt = 0d;
            var total = amt;
            foreach (var tank in OxygenTanks)
            {
                amt += tank.FilledRatio * tank.Capacity;
                total += tank.Capacity;
            }
            return amt / total;
        }

        string HydrogenTime(int ts)
        {
            var rate = H2.Rate(MathHelperD.Clamp(h2.Data, 1E-50, double.MaxValue));
            var value = h2.Data / rate;
            if (rate < 1E-15 || double.IsNaN(value) || double.IsInfinity(value))
                return invalid;
            var time = TimeSpan.FromSeconds(value);
            var r = string.Format("{0,2:D2}h {1,2:D2}m {2,2:D2}s", (long)time.TotalHours, (long)time.TotalMinutes, (long)time.Seconds);
            if (ts == 0)
                return r;
            else if (ts == 1) { r = time.TotalSeconds.ToString(); return r; }
            else if (ts == 2) { r = time.TotalMinutes.ToString(); return r; }
            else return invalid;
        }
    }
    public class InventoryItem
    {
        public string Tag = "";
        public MyItemType Type;
        public int Quantity = 0;
        public long last = 0;
        public InventoryItem(string[] line)
        {
            if (line.Length != 3) return;
            Tag = line[0];
            Type = new MyItemType(InventoryUtilities.myObjectBuilder + '_' + line[1], line[2]);

        }
        public InventoryItem(string t, string st)
        {
            Type = new MyItemType(InventoryUtilities.myObjectBuilder + '_' + t, st);
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Tag)) return $"{Tag} {Quantity}";
            else return Quantity.ToString();
        }
    }

    public class UseRate
    {
        double lastQ = 0;
        long lastTS;
        public readonly string iID;
        public UseRate(string i)
        { iID = i; }
        public double Rate(double q)
        {
            var t = DateTime.Now.Ticks;
            double r = (q - lastQ) / (t - lastTS);
            lastQ = q;
            lastTS = t;
            return r;
        }
    }

    public class ItemInfo : Info
    {
        public override double Data { get { return item.Quantity; } }
        public InventoryItem Item { get { return item; } }
        public string ID => item.Type.SubtypeId;
        private InventoryItem item;
        private readonly InventoryUtilities Base;

        public ItemInfo(InventoryItem i, InventoryUtilities iu) : base(null)
        {
            item = i;
            Base = iu;
            Update();
        }
        public override void Update()
        {
            Base.TryGetItem(ref item);
        }
        public override string ToString()
        {
            return item.ToString();
        }
    }

    public class InventoryUtilities : InfoUtility
    {

        public IMyProgrammableBlock Reference;
        public static string myObjectBuilder = "MyObjectBuilder";
        public string Section;
        int maxItems = 5, iiPtr;
        public bool updated;
        public List<IMyTerminalBlock> InventoryBlocks = new List<IMyTerminalBlock>();
        Dictionary<long, string[]> itemKeys = new Dictionary<long, string[]>();
        // you know i had to do it to em
        public SortedList<string, ItemInfo> Items = new SortedList<string, ItemInfo>();
        public int Pointer { get { return iiPtr; } }

        public InventoryUtilities(MyGridProgram program, string s)
        {
            Section = s;
            Reference = program.Me;
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

        private int ItemQuantity<T>(ref List<T> blocks, InventoryItem item)
            where T : IMyTerminalBlock
        {
            int amt = 0;
            foreach (var block in blocks)
                if (block.HasInventory && block.IsSameConstructAs(Reference))
                {
                    var i = block.GetInventory();
                    if (i.CurrentVolume.RawValue == 0) continue;
                    if (!i.ContainItems(1, item.Type))
                        continue;
                    amt += i.GetItemAmount(item.Type).ToIntSafe();
                }
            return amt;
        }

        public void TryGetItem<T>(ref List<T> blocks, ref InventoryItem item)
            where T : IMyTerminalBlock
        {
            item.Quantity = ItemQuantity(ref blocks, item);
        }
        public void TryGetItem(ref InventoryItem item)
        {
            item.Quantity = ItemQuantity(ref InventoryBlocks, item);
        }

        public bool TryGetUseRate<T>(UseRate r, ref Queue<double> storage, out double rate, List<T> invs = null)
            where T : IMyTerminalBlock
        {
            if (storage.Count == 10) // whatever
                storage.Dequeue();
            rate = 0d;
            if (storage.Count < 10)
                return false;
            int q = invs == null ? ItemQuantity(ref InventoryBlocks, Items[r.iID].Item) : ItemQuantity(ref invs, Items[r.iID].Item);
            rate = r.Rate(q);
            storage.Enqueue(rate);
            if (rate < 0) return false;
            return true;
        }

        void AddItemGroup(long id, string key)
        {
            Parser p = new Parser();
            MyIniParseResult result;
            if (p.CustomData(Reference, out result))
                if (p.hasKey(Section, key))
                {
                    var s = p.String(Section, key).Split('\n');
                    if (s.Length > 0)
                    {
                        var k = new string[s.Length];
                        for (int i = 0; i < s.Length; i++)
                        {
                            var a = s[i].Split(cmd);
                            if (!Items.ContainsKey(a[2]))
                                Items.Add(a[2], new ItemInfo(new InventoryItem(s[i].Split(cmd)), this));
                            k[i] = a[2];
                        }
                        itemKeys.Add(id, k);
                    }
                    return;
                }
                else throw new Exception($"key {key} for command !{key} not found in PB custom data.");
            else throw new Exception(result.Error);
        }
        int count = 0;
        void UpdateItemSprite(long id, ref string data)
        {
            var v = "";
            ItemInfo info;
            if (!itemKeys.ContainsKey(id)) return;
            data = Items[itemKeys[id][0]].Item.ToString();
            if (itemKeys[id].Length == 1) return;

            for (int i = 1; i < itemKeys[id].Length; i++)
                data += $"\n{Items[itemKeys[id][i]].Item}";

        }

        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            InventoryBlocks.Clear();
            Items.Clear();
            itemKeys.Clear();
            //var p = new Parser();
            //MyIniParseResult result;
            TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                var i = b.HasInventory;
                if (i) InventoryBlocks.Add(b);
                return i;
            });
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!item", (b) =>
            {
                if (justStarted)
                    if (b.Data.Contains(cmd))
                    {
                        b.Data = b.Data.Trim();
                        var s = b.Data.Split(cmd);
                        var item = new ItemInfo(new InventoryItem(s[0], s[1]), this);
                        itemKeys.Add(b.uID, new string[] { item.ID });
                        Items.Add(item.ID, item);
                    }
                    else throw new Exception("LOLE");
                UpdateItemSprite(b.uID, ref b.Data);
            });
            commands.Add("!ores", (b) =>
            {
                if (justStarted)
                { AddItemGroup(b.uID, "ores"); return; }
                UpdateItemSprite(b.uID, ref b.Data);
            });

            commands.Add("!ingots", (b) =>
            {
                if (justStarted)
                    AddItemGroup(b.uID, "ingots");
                UpdateItemSprite(b.uID, ref b.Data);
            });
            commands.Add("!invdebug", (b) =>
            {
                if (!justStarted) return;
                string debug = "";
                //foreach (var kvp in ItemStorage)
                //{
                //    var s = kvp.Value[0];
                //    var s  = kvp.Key.ToString();
                //    debug += s[0]+ "..." + s.Substring(10) + " " + s.Type.SubtypeId.ToUpper() + ", " + TryGetItem(ref InventoryBlocks, ref s) + '\n';
                //}
                var c = 0;
                foreach (var item in Items)
                    debug += item.ToString().ToUpper() + '\n';
                b.Data = debug;
            });

            commands.Add("!components", (b) =>
            {
                if (justStarted)
                { AddItemGroup(b.uID, "components"); return; }
                UpdateItemSprite(b.uID, ref b.Data);
            });

            commands.Add("!ammos", (b) =>
            {
                if (justStarted)
                { AddItemGroup(b.uID, "ammos"); return; }
                UpdateItemSprite(b.uID, ref b.Data);
            });
        }

        int Next()
        {
            int m = Items.Count - 1;
            if (iiPtr == m)
            {
                updated = true;
                iiPtr = 0;
                return maxItems;
            }
            if (iiPtr > m)
                iiPtr = m;
            else
                iiPtr += maxItems;
            return iiPtr;
        }

        public override void Update()
        {
            int i = iiPtr,
                m = Next();
            for (; i < m; i++)
                Items.Values[i].Update();
        }

        #endregion
    }

    //public class BlockUtilities : InfoUtility
    //{

    //}

    public class FlightUtilities : InfoUtility
    {
        //IMyCubeGrid Ship; //fuvckoff
        IMyShipController Controller;
        List<IMyJumpDrive> JumpDrives = new List<IMyJumpDrive>();
        double lastDist, maxDist;
        string std;
        DateTime stopTS; // fuck you
        Vector3D VZed = Vector3D.Zero, lastVel, grav;
        Info jump;

        public FlightUtilities(string f)
        {
            name = "Flight";
            std = f;
        }

        #region InfoUtility

        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            //Ship = program.Me.CubeGrid;
            TerminalSystem.GetBlocksOfType<IMyShipController>(null, (b) =>
            {
                if ((b.CustomName.Contains("[I]") || b.IsMainCockpit) && b.IsSameConstructAs(Program.Me))
                    Controller = b;
                return true;
            });
            TerminalSystem.GetBlocksOfType(JumpDrives, (b) => b.IsSameConstructAs(Program.Me));
        }
        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            jump = new Info(JumpCharge);
            commands.Add("!horiz", (b) =>
                b.Data = Validate(GetHorizonAngle(), "-#0.##; +#0.##") + "°");

            commands.Add("!c-alt", (b) =>
                b.Data = Validate(GetAlt(MyPlanetElevation.Sealevel), std));

            commands.Add("!s-alt", (b) =>            
                b.Data = Validate(GetAlt(MyPlanetElevation.Surface), std));

            commands.Add("!stop", (b) =>
                b.Data = Validate(StoppingDist(), std, std));

            commands.Add("!damp", (b) =>
            { // this should not be a problem (famous lastQ words)
                b.Data = Controller.DampenersOverride ? "ON" : "OFF";
            });

            commands.Add("!jcharge%", (b) => b.Data = jump.Data.ToString("#0.#%"));

            commands.Add("!jchargeb", (b) =>
            {
                if (justStarted) Util.CreateBarGraph(ref b);
                Util.UpdateBarGraph(ref b, jump.Data);
            });

        }

        public override void Update()
        {
            jump.Update();
        }

        #endregion

        string Validate(double v, string f, string o = "••")
        {
            return !double.IsNaN(v) ? v.ToString(f) : o; 
        }

        bool GravCheck(out Vector3D grav) //wanted something nice and neat
        {
            grav = VZed;
            if (Controller == null)
                return false;

            grav = Controller.GetNaturalGravity();
            if (grav == VZed)
                return false;
            return true;
        }

        double GetHorizonAngle()
        {
            var grav = VZed; // Vector3D.Zero         
            if (!GravCheck(out grav))
                return bad;
            grav.Normalize();
            return Math.Asin(MathHelper.Clamp(Controller.WorldMatrix.Forward.Dot(grav), -1, 1));
        }

        double GetAlt(MyPlanetElevation elevation)
        {
            var grav = VZed; // Vector3D.Zero         
            if (!GravCheck(out grav))
                return bad;
            var alt = 0d;
            if (Controller.TryGetPlanetElevation(elevation, out alt))
                return alt;
            return bad;
        }

        double StoppingDist()
        {
            double
                ret = lastDist,
                a = 0,
                dev = 0.01;
            var ts = DateTime.Now;

            var current = Controller.GetShipVelocities().LinearVelocity;
            if (!justStarted)
            {
                if (!Controller.DampenersOverride || current.Length() < 0.037) return bad;
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
            float charge, max = 0f;
            charge = max;
            if (JumpDrives.Count == 0) return 0f;
            foreach (var jd in JumpDrives)
            {
                charge += jd.CurrentStoredPower;
                max += jd.MaxStoredPower;
            }
            return charge / max;
        }
    }

    public class PowerUtilities : InfoUtility
    {
        InventoryUtilities Inventory;
        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
        List<IMyReactor> Reactors = new List<IMyReactor>();
        List<IMyPowerProducer> Engines = new List<IMyPowerProducer>();

        UseRate U;
        int lastUQ;
        long lastUTS;
        Info batt;
        Queue<double> savedUranium = new Queue<double>(10);
        string I = "ON", O = "OFF";
        public PowerUtilities(InventoryUtilities inventory)
        {
            Inventory = inventory;
            name = "Power";
            U = new UseRate("Uranium");
        }
        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            Batteries.Clear();
            Reactors.Clear();
            Engines.Clear();
            TerminalSystem.GetBlocksOfType(Batteries, (battery) => battery.IsSameConstructAs(program.Me));
            TerminalSystem.GetBlocksOfType(Reactors, (reactor) => reactor.IsSameConstructAs(program.Me));
            TerminalSystem.GetBlocksOfType(Engines, (generator) => generator.IsSameConstructAs(program.Me) && generator.CustomName.Contains("Engine"));
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            batt = new Info(BatteryCharge);

            commands.Add("!bcharge%", (b) =>
            {
                b.Data = !double.IsNaN(batt.Data) ? batt.Data.ToString("#0.#%") : invalid;
            });

            commands.Add("!bchargeb", (b) =>
            {
                if (justStarted) Util.CreateBarGraph(ref b);
                Util.UpdateBarGraph(ref b, batt.Data);
            });

            commands.Add("!fission", (b) =>
            {
                var rate = 0d;
                if (justStarted)
                    if (!Inventory.Items.ContainsKey(U.iID))
                        Inventory.Items.Add(U.iID, new ItemInfo(new InventoryItem("Ingot", U.iID), Inventory));
                b.Data = Inventory.TryGetUseRate(U, ref savedUranium, out rate, Reactors) ? $"{rate:000.0} KG/S" : "0 KG/S";
            });

            commands.Add("!reactorstat", (b) =>
            {
                int c = 0;

                foreach (var reactor in Reactors)
                    if (reactor.Enabled) c++;
                b.Data = Reactors.Count > 1 ? $"{c}/{Reactors.Count} " + I : (c == 0 ? O : I);
            });

            commands.Add("enginestat", (b) =>
            {
                int c = 0;
                foreach (var reactor in Engines)
                    if (reactor.Enabled) c++;
                b.Data = Engines.Count > 1 ? $"{c}/{Engines.Count} " + I : (c == 0 ? O : I);
            });
        }

        public override void Update()
        {
            batt.Update();
        }

        #endregion
        double BatteryCharge()
        {
            if (Batteries.Count == 0)
                return bad;
            var charge = 0d;
            var total = charge;
            foreach (var battery in Batteries)
            {
                charge += battery.CurrentStoredPower;
                total += battery.MaxStoredPower;
            }
            return (charge / total);
        }

    }

    // TODO: THIS SYSTEM IS ASS
    public class WeaponUtilities : InfoUtility
    {
        Dictionary<long, MyTuple<string, IMyTerminalBlock[]>> WeaponGroups = new Dictionary<long, MyTuple<string, IMyTerminalBlock[]>>();
        WCPBAPI api = null;
        string tag;
        int runs = 0, ok = 79;
        bool hasWC = true;
        bool wait
        {
            get
            {
                WCPBAPI.Activate(Program.Me, ref api);
                runs++;
                return runs <= ok;
            }
        }

        public WeaponUtilities(string t)
        {
            tag = t;
            name = "Weapons";
        }

        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            WCPBAPI.Activate(Program.Me, ref api);
            WeaponGroups.Clear();
        }
        bool noWC(ref SpriteData b)
        {
            var r = !hasWC || api == null;
            if (r) b.Data = "ERROR";
            return r;
        }
        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!wpnrdy", (b) =>
            {
                if (wait) return;
                if (noWC(ref b)) { hasWC = false; return; }
                if (runs >= ok && !WeaponGroups.ContainsKey(b.uID)) AddWeaponGroup(b);
                else if (WeaponGroups.ContainsKey(b.uID))
                    UpdateWeaponReady(ref b);
            });
            commands.Add("!tgt", (b) =>
            {
                if (wait) return;
                if (noWC(ref b)) { hasWC = false; return; }
                else
                {
                    var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
                    b.Data = focus.HasValue ? focus.Value.Name : "NO TARGET";
                }
            });

            commands.Add("!tgtdist", (b) =>
            {
                if (wait) return;
                if (noWC(ref b)) { hasWC = false; return; }
                else
                {
                    var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
                    b.Data = focus.HasValue ? (focus.Value.Position - Program.Me.CubeGrid.GetPosition()).Length().ToString("4:####") : "NO TARGET";
                }
            });

        }

        public override void Update()
        {

        }

        #endregion

        void AddWeaponGroup(SpriteData d)
        {
            var list = new List<IMyTerminalBlock>();
            string[] dat = d.Data.Split(cmd);
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
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Gui;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Policy;
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
    public interface IInfo
    {
        double Data { get; }
        void Update();
    }


    public class Info : IInfo
    {
        public double Data => data;
        private double data;
        private Func<double> update;
        public Info(Func<double> u)
        {
            update = u;
        }
        public void Update() => data = update.Invoke();
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
        #endregion

        public virtual void Reset(GraphicsManager manager, MyGridProgram program)
        {
            GCM = manager;
            Program = program;
            TerminalSystem = program.GridTerminalSystem;
            jitSprite.uID = long.MinValue;
        }

        public virtual void GetBlocks() {}
        public abstract void Update();
        public abstract void Setup(ref Dictionary<string, Action<SpriteData>> commands);


    }

    // SO...command formatting. Depends on the general command, but here's the idea
    // this is all for the K_DATA field of the sprite.
    // <required param 1>$<required param 2>$...$<required param n>

    public class GasUtilities : UtilityBase
    {
        InventoryUtilities Inventory;
        public List<IMyGasTank>
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
        UseRate Ice;
        const int keen = 5;
        DateTime tsH2;
        double lastH2; // sigh
        Dictionary<long, int> hTime = new Dictionary<long, int>();
        Info h2, o2;

        public GasUtilities(ref InventoryUtilities inv)
        {
            name = "Gas";
            Ice = new UseRate("Ore!Ice");
            Inventory = inv;
        }

        #region InfoUtility
        public override void Reset(GraphicsManager m, MyGridProgram p)
        {
            base.Reset(m, p);
            hTime.Clear();
            GetBlocks();
            HydrogenStatus();
            OxygenStatus();
            HydrogenTime(0);
        }

        public override void GetBlocks()
        {
            HydrogenTanks.Clear();
            OxygenTanks.Clear();
            TerminalSystem.GetBlocksOfType(HydrogenTanks, (b) => b.BlockDefinition.SubtypeId.Contains("Hyd"));
            TerminalSystem.GetBlocksOfType(OxygenTanks, (b) => !HydrogenTanks.Contains(b));
            base.GetBlocks();
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
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, h2.Data);
            });
            commands.Add("!o2b", (b) =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, o2.Data);
            });
            commands.Add("!h2t", (b) =>
            {
                if (GCM.justStarted)
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
                if (GCM.justStarted && !Inventory.Items.ContainsKey(Ice.iID))
                {
                    var i = Ice.iID.Split(cmd);
                    Inventory.Items.Add(Ice.iID, new ItemInfo(i[0], i[1], Inventory));
                }
                b.Data = Inventory.TryGetUseRate<IMyTerminalBlock>(Ice, out rate) ? (rate / keen).ToString("G4") : invalid;
            });
        }

        public override void Update()
        {
            h2.Update();
            o2.Update();
        }

        #endregion
        double HydrogenStatus() => TankStatus(ref HydrogenTanks);
        double OxygenStatus() => TankStatus(ref  OxygenTanks);

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


        string HydrogenTime(int sw)
        {
            var ts = DateTime.Now;
            var rate = MathHelperD.Clamp(lastH2 - h2.Data, 1E-50, double.MaxValue) / (ts - tsH2).TotalSeconds;
            var value = h2.Data / rate;
            lastH2 = h2.Data;
            tsH2 = ts;
            if (rate < 1E-15 || double.IsNaN(value) || value > 1E5)
                return invalid;
            var time = TimeSpan.FromSeconds(value);
            // stolen
            if (sw == 0)
                if (time.TotalHours >= 1)
                    return string.Format("{0,2}h {1,2}m", (long)time.TotalHours, (long)time.Minutes);
                else
                    return string.Format("{0,2}m {1,2}s", (long)time.TotalMinutes, (long)time.Seconds);
            else if (sw == 1) return time.TotalSeconds.ToString();
            else if (sw == 2) return time.TotalMinutes.ToString();
            else return invalid;
        }
    }
    public class InventoryItem
    {
        public string Tag = "";
        public MyItemType Type;
        public int Quantity = 0;
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
        //{
        //    if (!string.IsNullOrEmpty(Tag)) 
        //        return $"{Tag} {Quantity}";
        //    else 
        //        return 
        //}
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

    public class ItemInfo : IInfo
    {
        public double Data => quantity;
        private double quantity;
        public readonly MyItemType Type;

        public string ID => Tag + '!' + Type.SubtypeId;
        public readonly string Tag;
        private readonly InventoryUtilities Inventory;

        public ItemInfo(string t, string st, InventoryUtilities iu)
        {
            Inventory = iu;
            Tag = t;
            Type = new MyItemType(InventoryUtilities.myObjectBuilder + '_' + t, st);
            Update();
        }

        public void Update() => quantity = Inventory.ItemQuantity(ref Inventory.InventoryBlocks, this);

        public override string ToString() => quantity.ToString();
    }

    public class InventoryUtilities : UtilityBase
    {

        public IMyProgrammableBlock Reference;
        public static string myObjectBuilder = "MyObjectBuilder";
        public string Section, J = "JIT";
        int updateStep = 5, iiPtr;
        bool ignoreTanks;
        public bool needsUpdate;
        public List<IMyTerminalBlock> InventoryBlocks = new List<IMyTerminalBlock>();
        private Dictionary<long, string[]>
            itemKeys = new Dictionary<long, string[]>(),
            itemTags = new Dictionary<long, string[]>();
        // you know i had to do it to em
        public SortedList<string, ItemInfo> Items = new SortedList<string, ItemInfo>();
        public int Pointer => iiPtr + 1;

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

        public int ItemQuantity<T>(ref List<T> blocks, ItemInfo item)
            where T : IMyTerminalBlock
        {
            int amt = 0, i = 0;
            for(; i < blocks.Count; i++)
            {
                var inv = blocks[i]?.GetInventory();
                if (inv == null || !inv.ContainItems(1, item.Type))
                    continue;
                amt += inv.GetItemAmount(item.Type).ToIntSafe();
            }
            return amt;
        }

        public bool TryGetUseRate<T>(UseRate r, out double rate, List<T> invs = null)
            where T : IMyTerminalBlock
        {
            rate = 0;
            if (r.iID == J) return false;
            int q = invs == null ? ItemQuantity(ref InventoryBlocks, Items[r.iID]) : ItemQuantity(ref invs, Items[r.iID]);
            rate = r.Rate(q);
            if (rate <= 0) return false;
            return true;
        }

        void AddItemGroup(long id, string key)
        {
            if (key == J)
                return;
            var p = new iniWrap();
            MyIniParseResult result;
            if (p.CustomData(Reference, out result))
                if (p.hasKey(Section, key))
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
                                    var item = new ItemInfo(a[1], a[2], this);
                                    Items.Add(item.ID, item);
                                }
                                catch (Exception e)
                                { continue; }
                            k[i] = a[1] + cmd + a[2];
                            if (a.Length != 3)
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
                    return;
                }
                else throw new Exception($"key {key} for command !{key} not found in PB custom data.");
            else throw new Exception(result.Error);
        }

        void UpdateItemString(long id, ref string data)
        {
            if (!itemKeys.ContainsKey(id))
                return;
            //string s = "";
            //foreach (var key in Items.Keys) s += $"\n{key}";
            //throw new Exception(s);
            data = itemTags[id][0] + Items[itemKeys[id][0]].ToString();
            if (itemKeys[id].Length == 1)
                return;
            for (int i = 1; i < itemKeys[id].Length; i++)
                data += $"\n{itemTags[id][i]}{Items[itemKeys[id][i]]}";
        }

        #region InfoUtility

        public override void Reset(GraphicsManager m, MyGridProgram p)
        {
            base.Reset(m, p);
            GetBlocks();
            var jitItem = new ItemInfo("Ingot", J, this);
            var temp = new Queue<double>();
            var d = 0d;
            AddItemGroup(jitSprite.uID, J);
            UpdateItemString(jitSprite.uID, ref jitSprite.Data);
            TryGetUseRate<IMyTerminalBlock>(new UseRate(J), out d);
            Items.Remove(J);
            itemKeys.Remove(jitSprite.uID);
            itemTags.Remove(jitSprite.uID);
        }

        public override void GetBlocks()
        {
            InventoryBlocks.Clear();
            TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                var i = b.HasInventory && b.IsSameConstructAs(Reference);
                if (ignoreTanks)
                    if (b is IMyGasTank)
                        return false;
                if (i) InventoryBlocks.Add(b);
                return i;
            });
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            Items.Clear();
            var p = new iniWrap();
            if (p.CustomData(Reference))
                ignoreTanks = p.Bool(Section, "ignoreTanks", true);
            commands.Add("!item", (b) =>
            {
                if (GCM.justStarted)
                    if (b.Data.Contains(cmd))
                    {
                        b.Data = b.Data.Trim();
                        var s = b.Data.Split(cmd);
                        if (!itemKeys.ContainsKey(b.uID))
                        {
                            itemKeys.Add(b.uID, new string[] { b.Data });
                            itemTags.Add(b.uID, new string[] { "" });
                        }
                        if (!Items.ContainsKey(b.Data))
                            Items.Add(b.Data, new ItemInfo(s[0], s[1], this));
                    }
                    else return;
                UpdateItemString(b.uID, ref b.Data);
            });

            commands.Add("!itemslist", (b) =>
            {
                if (GCM.justStarted && b.Data != "")
                {
                    AddItemGroup(b.uID, b.Data);
                    return;
                }
                UpdateItemString(b.uID, ref b.Data);
            });

            commands.Add("!invdebug", (b) =>
            {
                if (!GCM.justStarted)
                    return;
                string debug = $"INVENTORIES - {InventoryBlocks.Count}";

                foreach (var item in Items.Keys)
                    debug += $"\n{item.ToUpper()}";
                b.Data = debug;
            });
        }

        public override void Update()
        {
            if (Items.Count == 0)
            {
                needsUpdate = false;
                return;
            }
            int n = Math.Min(Items.Count, iiPtr + updateStep);
            for (; iiPtr < n; iiPtr++)
                Items.Values[iiPtr].Update();
            if (n == Items.Count)
            {
                iiPtr = 0;
                needsUpdate = false;
            }
        }

        #endregion
    }

    //public class BlockUtilities : UtilityBase
    //{

    //}

    public class FlightUtilities : UtilityBase
    {
        //IMyCubeGrid Ship; //fuvckoff
        IMyShipController Controller;
        List<IMyJumpDrive> JumpDrives = new List<IMyJumpDrive>();
        double lastDist, maxDist;
        readonly string tag, ctrl, fmat;
        string std, ctrlName;
        DateTime stopTS; // fuck you
        Vector3D VZed = Vector3D.Zero, lastVel, grav;
        Info jump;

        public FlightUtilities(string s, string f = "flightFMAT", string c = "shipCTRL")
        {
            name = "Flight";
            tag = s;
            fmat = f;
            ctrl = c;
        }

        #region InfoUtility

        public override void Reset(GraphicsManager m, MyGridProgram p)
        {
            base.Reset(m, p);
            var par = new iniWrap();
            par.CustomData(m.Me);
            std = par.String(tag, fmat, "0000");
            ctrlName = par.String(tag, ctrl, "[I]");
            GetBlocks();
        }

        public override void GetBlocks()
        {
            TerminalSystem.GetBlocksOfType<IMyShipController>(null, (b) =>
            {
                if ((b.CustomName.Contains(ctrlName) || b.IsMainCockpit) && b.IsSameConstructAs(Program.Me))
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
            { // this should not be a problem (famous last words)
                b.Data = Controller.DampenersOverride ? "ON" : "OFF";
            });

            commands.Add("!jcharge%", (b) => b.Data = jump.Data.ToString("#0.#%"));

            commands.Add("!jchargeb", (b) =>
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

        string Validate(double v, string f, string o = "••") => !double.IsNaN(v) ? v.ToString(f) : o;

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
            if (!GCM.justStarted)
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
            for (int i = 0; i < JumpDrives.Count; i++)
            {
                //if (JumpDrives[i] == null) continue;
                charge += JumpDrives[i].CurrentStoredPower;
                max += JumpDrives[i].MaxStoredPower;
            }
            return charge / max;
        }
    }

    public class PowerUtilities : UtilityBase
    {
        InventoryUtilities Inventory;
        IMyTerminalBlock gridRef;
        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
        List<IMyReactor> Reactors = new List<IMyReactor>();
        List<IMyPowerProducer> Engines = new List<IMyPowerProducer>();

        UseRate U;
        Info batt;
        readonly string I = "ON", O = "OFF";
        public PowerUtilities(ref InventoryUtilities inventory)
        {
            Inventory = inventory;
            name = "Power";
            U = new UseRate("Ingot!Uranium");
        }
        #region InfoUtility
        public override void Reset(GraphicsManager m, MyGridProgram p)
        {
            gridRef = p.Me;
            base.Reset(m, p);
            GetBlocks();
        }

        public override void GetBlocks()
        {
            Batteries.Clear();
            Reactors.Clear();
            Engines.Clear();
            TerminalSystem.GetBlocksOfType(Batteries, (battery) => battery.IsSameConstructAs(gridRef));
            TerminalSystem.GetBlocksOfType(Reactors, (reactor) => reactor.IsSameConstructAs(gridRef));
            TerminalSystem.GetBlocksOfType(Engines, (generator) => generator.IsSameConstructAs(gridRef) && generator.CustomName.Contains("Engine"));
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
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, batt.Data);
            });

            commands.Add("!fission", (b) =>
            {
                var rate = 0d;
                if (GCM.justStarted)
                    if (!Inventory.Items.ContainsKey(U.iID))
                        Inventory.Items.Add(U.iID, new ItemInfo("Ingot", U.iID, Inventory));
                b.Data = Inventory.TryGetUseRate(U, out rate, Reactors) ? $"{rate:000.0} KG/S" : "0 KG/S";
            });

            commands.Add("!reactorstat", (b) =>
            {
                int c = 0, i = 0;

                for(; i < Reactors.Count; i++)
                {

                    if (Reactors[i].Enabled) c++;
                }
                b.Data = Reactors.Count > 1 ? $"{c}/{Reactors.Count} " + I : (c == 0 ? O : I);
            });

            commands.Add("enginestat", (b) =>
            {
                int c = 0, i = 0;
                for (; i < Engines.Count; i++)
                    if (Engines[i].Enabled) c++;
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
            for (int i = 0; i < Batteries.Count; i++)
            {
                //if (battery == null) continue;
                charge += Batteries[i].CurrentStoredPower;
                total += Batteries[i].MaxStoredPower;
            }
            return (charge / total);
        }

    }

    // TODO: THIS SYSTEM IS ASS
    public class WeaponUtilities : UtilityBase
    {
        Dictionary<long, MyTuple<string, IMyTerminalBlock[]>> WeaponGroups = new Dictionary<long, MyTuple<string, IMyTerminalBlock[]>>();
        Dictionary <long, string> tagStorage = new Dictionary<long, string>();
        List<IMyTerminalBlock> wcWeapons = new List<IMyTerminalBlock>();
        WCPBAPI api = null;
        bool man = false;

        public WeaponUtilities()
        {
            name = "Weapons";
        }

        #region InfoUtility
        public override void Reset(GraphicsManager m, MyGridProgram p)
        {
            base.Reset(m, p);
            if (WCPBAPI.Activate(Program.Me, ref api))
                GetBlocks();
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
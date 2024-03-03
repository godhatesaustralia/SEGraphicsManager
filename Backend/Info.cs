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
    public class InfoUtility
    {
        #region fields

        public static bool justStarted;
        public static Dictionary<long, MyTuple<bool, float>> GraphStorage = new Dictionary<long, MyTuple<bool, float>>();
        protected MyGridProgram Program;
        protected IMyGridTerminalSystem TerminalSystem;
        protected const char
            commandSplit = '!',
            space = ' ';
        protected string invalid = "••";
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
        protected TimeSpan DeltaT
        {
            get
            {
                return Program.Runtime.TimeSinceLastRun;
            }
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
        public virtual void RegisterCommands(ref Dictionary <string, Action<SpriteData>> commands)
        {
            Program.Echo("YOU SHOULD KILL YOURSELF...NOW!!!!!!");
        }
    }

    // SO...command formatting. Depends on the general command, but here's the idea
    // this is all for the K_DATA field of the sprite.
    // <required param 1>$<required param 2>$...$<required param n>

    public class GasUtilities : InfoUtility
    {
        InventoryUtilities Inventory;
        public static List<IMyGasTank> 
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
        public static InventoryItem Ice;
        double lastHydrogen = 0;
        Dictionary<long, int> hTime = new Dictionary<long, int>();
        Queue<double> savedIce = new Queue<double>(10);

        public GasUtilities(InventoryUtilities i)
        {
            Inventory = i;
            Ice = new InventoryItem("Ore", "Ice");
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

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands) 
        {           //i fucking hate this, this sucks ass
            commands.Add("!h2%", (b) =>
            b.Data = HydrogenStatus().ToString("#0.#%"));

            commands.Add("!o2%", (b) =>
                b.Data = OxygenStatus().ToString("#0.#%"));

            commands.Add("!h2b", (b) =>
            {
                if (justStarted) Utilities.CreateBarGraph(ref b);
                Utilities.UpdateBarGraph(ref b, HydrogenStatus());
            });

            commands.Add("!o2b", (b) =>
            {
                if (justStarted) Utilities.CreateBarGraph(ref b);
                Utilities.UpdateBarGraph(ref b, OxygenStatus());
            });

            commands.Add("!h2t", (b) => 
            {
                if (justStarted)
                    if (b.Data == "sec")
                        hTime.Add(b.uID, 1);
                    else if (b.Data == "min")
                        hTime.Add(b.uID, 2);

                if (hTime.ContainsKey(b.uID))
                    b.Data = HydrogenTime(hTime[b.uID]);
                else
                b.Data = HydrogenTime();
            });
                
            commands.Add("!ice", (b) =>
            {// ONLY USE WITH UPDATE100
                var rate = 0d;
                b.Data = Inventory.TryGetUseRate(ref Ice, ref savedIce, ref InventoryUtilities.InventoryBlocks, out rate) ? $"{rate:000.0} kg/s" : "0 kg/s";
            });
             
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

        string HydrogenTime(int ts = 0)
        {
            var pct = HydrogenStatus();
            //program.Me.CustomData += $"PCT {pct}\n";
            var rate = MathHelperD.Clamp(lastHydrogen - pct, 1E-50, double.MaxValue) / DeltaT.TotalSeconds;
            //program.Me.CustomData += $"RATE {rate}\n";
            var value = pct / rate;
            lastHydrogen = HydrogenStatus();
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

    public class InventoryUtilities : InfoUtility
    {
        public IMyProgrammableBlock Reference;
        public static string myObjectBuilder = "MyObjectBuilder";
        public string Section;
        public static List<IMyTerminalBlock>InventoryBlocks = new List<IMyTerminalBlock>();
        public Dictionary<long, InventoryItem[]> ItemStorage = new Dictionary<long, InventoryItem[]>();

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

        public bool TryGetItem<T>(ref List<T> blocks, ref InventoryItem item)
            where T : IMyTerminalBlock
        {
            int amount = 0;
            foreach (var block in blocks)
                if (block.HasInventory && block.IsSameConstructAs(Reference))
                {
                    var i = block.GetInventory();
                    if (!i.ContainItems(1, item.Type))
                        continue;
                    amount += i.GetItemAmount(item.Type).ToIntSafe();
                }
            if (amount == 0) return true;
            item.Quantity = amount; 
            return true;
        }

        public bool TryGetUseRate<T>(ref InventoryItem item, ref Queue<double> storage, ref List<T> blocks, out double rate)
            where T : IMyTerminalBlock
        {       
            if (storage.Count == 10) // whatever
                storage.Dequeue();
            rate = 0d;
            TryGetItem(ref blocks, ref item);
            storage.Enqueue(item.Quantity);
            if (storage.Count < 10)
                return false;
            var t = DateTime.Now.Ticks;
            rate = (storage.First() - storage.Last()) / (t - item.last);
            item.last = t;
            if (rate < 0) return false;
            return true;
        }

        void AddItemGroup(long id, string key)
        {
            Parser parser = new Parser();
            MyIniParseResult result;
            if (parser.CustomData(Reference, out result))
                if (parser.hasKey(Section, key))
                {
                    var s = parser.String(Section, key).Split('\n');
                    if (s.Length > 0)
                    {
                        var array = new InventoryItem[s.Length];
                        for (int i = 0; i < s.Length; i++)
                            array[i] = new InventoryItem(s[i].Split(commandSplit));
                        ItemStorage.Add(id, array);
                    }
                }
        }

        void UpdateItemGroup(long id, ref string data)
        {
            data = ItemStorage[id][0].ToString();
            if (ItemStorage[id].Length == 1)
            {
                if (TryGetItem(ref InventoryBlocks, ref ItemStorage[id][0]))
                {
                    data = ItemStorage[id][0].ToString();
                    return;
                }
            }
            else if (ItemStorage[id].Length > 1)
            {
                if (TryGetItem(ref InventoryBlocks, ref ItemStorage[id][0]))
                {
                    data = $"{ItemStorage[id][0]}";
                    for (int i = 1; i < ItemStorage[id].Length; i++)
                    {
                        TryGetItem(ref InventoryBlocks, ref ItemStorage[id][i]);
                        data += '\n' + $"{ItemStorage[id][i]}";
                    }
                    return;
                }
            }
        }

        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            InventoryBlocks.Clear();
            ItemStorage.Clear();
            TerminalSystem.GetBlocksOfType(InventoryBlocks, (b) => b.HasInventory);    
        }

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!item", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.uID))
                    if (b.Data.Contains(commandSplit))
                    {
                        b.Data = b.Data.Trim();
                        var stringParts = b.Data.Split(commandSplit);
                        var item = new InventoryItem[] { new InventoryItem(stringParts[0], stringParts[1]) };
                        ItemStorage.Add(b.uID, item);
                    }
                    else throw new Exception("LOLE");
                UpdateItemGroup(b.uID, ref b.Data);
            });
            commands.Add("!ores", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.uID))
                    AddItemGroup(b.uID, "ores");
                UpdateItemGroup(b.uID, ref b.Data);
            });

            commands.Add("!ingots", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.uID))
                    AddItemGroup(b.uID, "ingots");
                UpdateItemGroup(b.uID, ref b.Data);
            });
            commands.Add("debug", (b) =>
            {
                string debug = string.Empty;
                foreach (var kvp in ItemStorage)
                {
                    var item = kvp.Value[0];
                    var s  = kvp.Key.ToString();
                    debug += s[0]+ "..." + s.Substring(10) + " " + item.Type.SubtypeId.ToUpper() + ", " + TryGetItem(ref InventoryBlocks, ref item) + '\n';
                } b.Data = debug;
            });

            commands.Add("!components", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.uID))
                    AddItemGroup(b.uID, "components");
                UpdateItemGroup(b.uID, ref b.Data);
            });

            commands.Add("!ammos", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.uID))
                    AddItemGroup(b.uID, "ammos");
                UpdateItemGroup(b.uID, ref b.Data);
            });
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
        Vector3D VZed = Vector3D.Zero, last;

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

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!horiz", (b) =>
            {
                var aoa = GetHorizonAngle();
                b.Data = aoa != bad ? MathHelper.ToDegrees(aoa).ToString("-#0.##; +#0.##") + "°" : invalid;
            });

            commands.Add("!c-alt", (b) => 
            {
                var alt = GetAlt(MyPlanetElevation.Sealevel);
                b.Data = alt != bad ? $"{alt:0000} m" : invalid;
            });

            commands.Add("!s-alt", (b) => 
            {
                var alt = GetAlt(MyPlanetElevation.Surface);
                b.Data = alt != bad ? $"{alt:0000} m" : invalid;
            });

            commands.Add("!stop", (b) =>
            {
                var dist = StoppingDist();
                b.Data = $"{dist:0000}"; 
            });

            commands.Add("!damp", (b) =>
            {
                b.Data = Controller.DampenersOverride ? "ON" : "OFF";
            });

            commands.Add("!jcharge%", (b) => b.Data = JumpCharge().ToString("#0.#%"));

            commands.Add("!jchargeb", (b) =>
            {
                if (justStarted) Utilities.CreateBarGraph(ref b);
                Utilities.UpdateBarGraph(ref b, JumpCharge());
            });

        }
        #endregion

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
            var ret = lastDist;
            var a = 0d;
            var current = Controller.GetShipVelocities().LinearVelocity;
            if (!justStarted && current != VZed)
            {
                var mag = (last - current).Length();
                if (mag > 0.01)
                {
                    a = (last - current).Length() / DeltaT.TotalSeconds;
                    ret = current.Length() * current.Length() / (2 * a);
                    lastDist = ret;
                }
            }
            if (ret > maxDist) maxDist = ret;
            last = current;
            return a <= 0.01 ? maxDist : ret;
        }

        float JumpCharge()
        {
            float charge, max = 0f;
            charge = max;
            if (JumpDrives.Count == 0) return 0f;
            foreach (var jd in JumpDrives)
            {
                charge += jd.CurrentStoredPower;
                max += jd.MaxStoredPower;
            }
            return charge/max;
        }
    }

    public class PowerUtilities : InfoUtility
    {
        InventoryUtilities Inventory;
        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
        List<IMyReactor> Reactors = new List<IMyReactor>();
        List<IMyPowerProducer> Engines = new List<IMyPowerProducer>();
        InventoryItem uraniumIngot = new InventoryItem("Ingot", "Uranium");
        Queue<double> savedUranium = new Queue<double>(10);
        string I = "ON", O = "OFF";
        public PowerUtilities(InventoryUtilities inventory)
        {
            Inventory = inventory;
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

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!bcharge%", (b) =>
            {
                var batt = BatteryCharge();
                b.Data = batt != bad ? batt.ToString("#0.#%") : invalid;
            });

            commands.Add("!bchargeb", (b) =>
            {
                if (justStarted) Utilities.CreateBarGraph(ref b);
                Utilities.UpdateBarGraph(ref b, BatteryCharge());
            });

            commands.Add("!fission", (b) =>
            {
                var rate = 0d;
                b.Data = Inventory.TryGetUseRate(ref uraniumIngot, ref savedUranium, ref Reactors, out rate) ? $"{rate:000.0} KG/S" : "0 KG/S";
            });

            commands.Add("!reactorstat", (b) =>
            {
                int c = 0;

                foreach (var reactor in Reactors)
                    if (reactor.Enabled) c++;
                b.Data = Reactors.Count > 1  ? $"{c}/{Reactors.Count} " + I :(c == 0 ? O : I);
            });

            commands.Add("enginestat", (b) =>
            {
                int c = 0;
                foreach (var reactor in Engines)
                    if (reactor.Enabled) c++;
                b.Data = Engines.Count > 1 ? $"{c}/{Engines.Count} " + I : (c == 0 ? O : I);
            });

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

        public WeaponUtilities(string t)
        {
            tag = t;
        }

        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            WCPBAPI.Activate(Program.Me, ref api);
            WeaponGroups.Clear();
        }

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!wpnrdy", (b) =>
            {
                if (justStarted) AddWeaponGroup(b);
                else if (WeaponGroups.ContainsKey(b.uID))
                    UpdateWeaponCharge(ref b);
            });
            commands.Add("!tgt", (b) =>
            {
                if (api == null) { b.Data = "ERROR"; return; }
                else
                {
                    var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
                    b.Data = focus.HasValue ? focus.Value.Name : "NO TARGET";
                }
            });

            commands.Add("!tgtdist", (b) =>
            {
                if (api == null) { b.Data = "ERROR"; return; }
                else
                {
                    var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
                    b.Data = focus.HasValue ? (focus.Value.Position - Program.Me.CubeGrid.GetPosition()).Length().ToString("4:####") : "NO TARGET";
                }
            });

        }

        #endregion

        void AddWeaponGroup(SpriteData d)
        {
            var list = new List<IMyTerminalBlock>();
            string[] dat = d.Data.Split(commandSplit);
            TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                if (b.IsSameConstructAs(Program.Me) && b.CustomName.Contains(dat[0]))
                    list.Add(b);
                return true;
            });
            if (list.Count > 0) WeaponGroups.Add(d.uID, new MyTuple<string, IMyTerminalBlock[]>(dat[1], list.ToArray()));
        }

        void UpdateWeaponCharge(ref SpriteData d)
        {
            if (api == null) return;
            int count = 0;
            foreach (var wpn in WeaponGroups[d.uID].Item2)
                if (api.IsWeaponReadyToFire(wpn)) count++;
            d.Data = $"{WeaponGroups[d.uID].Item1} {count}/{WeaponGroups[d.uID].Item2.Length} RDY";
        }

    }
}
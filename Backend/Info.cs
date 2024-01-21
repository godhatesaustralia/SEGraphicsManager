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
            if (d.BuilderPrepend != null)
                builder.Insert(0, d.BuilderPrepend);
            if (d.BuilderAppend != null)
                builder.Append(d.BuilderAppend);
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
        public static InventoryItem Ice = new InventoryItem(new MyItemType("MyObjectBuilder_Ore", "Ice"));
        double lastHydrogen = 0;
        Queue<double> savedIce = new Queue<double>(10);

        public GasUtilities(InventoryUtilities inventory)
        {
            Inventory = inventory;
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
                SharedUtilities.UpdateBarGraph(ref b, HydrogenStatus()));

            commands.Add("!o2b", (b) =>
                SharedUtilities.UpdateBarGraph(ref b, OxygenStatus()));

            commands.Add("!h2t", (b) =>
                b.Data = HydrogenTime());
                
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

        string HydrogenTime()
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
        
            return string.Format("{0,2:D2}h {1,2:D2}m {2,2:D2}s", (long)time.TotalHours, (long)time.TotalMinutes, (long)time.Seconds); 
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
            Type = new MyItemType(InventoryUtilities.myObjectBuilderString + '_' + line[1], line[2]);
        }
        public InventoryItem(MyItemType type)
        {
            Type = type;
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
        public static string myObjectBuilderString = "MyObjectBuilder";
        public string Section;
        public static List<IMyTerminalBlock>InventoryBlocks = new List<IMyTerminalBlock>();
        public Dictionary<long, InventoryItem[]> ItemStorage = new Dictionary<long, InventoryItem[]>();

        public InventoryUtilities(MyGridProgram program, string sect)
        {
            Section = sect;
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
                    var inventory = block.GetInventory();
                    if (!inventory.ContainItems(1, item.Type))
                        continue;
                    amount += inventory.GetItemAmount(item.Type).ToIntSafe();
                }
            if (amount == 0) return false;
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
            rate = (storage.First() - storage.Last()) / 2;
            if (rate < 0) return false;
            return true;
        }

        void AddItemGroup(long id, string key)
        {
            Parser parser = new Parser();
            MyIniParseResult result;
            if (parser.TryParseCustomData(Reference, out result))
                if (parser.ContainsKey(Section, key))
                {
                    var s = parser.ParseString(Section, key).Split('\n');
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
                    data = ItemStorage[id][0].ToString();
                    for (int i = 1; i < ItemStorage[id].Length; i++)
                    {
                        TryGetItem(ref InventoryBlocks, ref ItemStorage[id][i]);
                        data += '\n' + ItemStorage[id][i].ToString();
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
                MyItemType itemType;
                if (justStarted && !ItemStorage.ContainsKey(b.UniqueID))
                    if (b.Data.Contains(commandSplit))
                    {
                        b.Data = b.Data.Trim();
                        var stringParts = b.Data.Split(commandSplit);
                        itemType = new MyItemType($"{myObjectBuilderString}_{stringParts[0]}", stringParts[1]);
                        var item = new InventoryItem[] { new InventoryItem(itemType) };
                        ItemStorage.Add(b.UniqueID, item);
                    }
                    else throw new Exception("LOLE");
                UpdateItemGroup(b.UniqueID, ref b.Data);
            });
            commands.Add("!ores", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.UniqueID))
                    AddItemGroup(b.UniqueID, "ores");
                UpdateItemGroup(b.UniqueID, ref b.Data);
            });

            commands.Add("!ingots", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.UniqueID))
                    AddItemGroup(b.UniqueID, "ingots");
                UpdateItemGroup(b.UniqueID, ref b.Data);
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
                if (justStarted && !ItemStorage.ContainsKey(b.UniqueID))
                    AddItemGroup(b.UniqueID, "components");
                UpdateItemGroup(b.UniqueID, ref b.Data);
            });

            commands.Add("!ammos", (b) =>
            {
                if (justStarted && !ItemStorage.ContainsKey(b.UniqueID))
                    AddItemGroup(b.UniqueID, "ammos");
                UpdateItemGroup(b.UniqueID, ref b.Data);
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
        double lastDist;
        Vector3D VZed = Vector3D.Zero, last;

        #region InfoUtility

        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            //Ship = program.Me.CubeGrid;
            TerminalSystem.GetBlocksOfType<IMyShipController>(null, (b) =>
            {
                if (b.CustomName.Contains("[I]") || b.IsMainCockpit)
                    Controller = b;
                return true;
            });
        }

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!horiz", (b) =>
            {
                var aoa = GetHorizonAngle();
                b.Data = aoa != bad ? MathHelper.ToDegrees(aoa).ToString("-#0.##; +#0.##") + "°" : invalid;
            });

            commands.Add("!seaalt", (b) => 
            {
                var alt = GetAlt(MyPlanetElevation.Sealevel);
                b.Data = alt != bad ? $"{alt:0000} m" : invalid;
            });

            commands.Add("!suralt", (b) => 
            {
                var alt = GetAlt(MyPlanetElevation.Surface);
                b.Data = alt != bad ? $"{alt:0000} m" : invalid;
            });

            commands.Add("!stopdist", (b) =>
            {
                var dist = StoppingDist();
                b.Data = $"{dist:0000}"; 
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
            var current = Controller.GetShipVelocities().LinearVelocity;
            if (!justStarted && current != VZed)
            {
                var mag = (last - current).Length();
                if (mag > 0.01)
                {
                    var accel = (last - current).Length() / DeltaT.TotalSeconds;
                    ret = current.Length() * current.Length() / (2 * accel);
                    lastDist = ret;
                }
            }
            last = current;
            return ret;
        }
    }

    public class PowerUtilities : InfoUtility
    {
        InventoryUtilities Inventory;
        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
        List<IMyReactor> Reactors = new List<IMyReactor>();
        List<IMyPowerProducer> Generators = new List<IMyPowerProducer>();
        InventoryItem uraniumIngot = new InventoryItem(new MyItemType($"{InventoryUtilities.myObjectBuilderString}_Ingot", "Uranium"));
        Queue<double> savedUranium = new Queue<double>(10);
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
            Generators.Clear();
            TerminalSystem.GetBlocksOfType(Batteries, (battery) => battery.IsSameConstructAs(program.Me));
            TerminalSystem.GetBlocksOfType(Reactors, (reactor) => reactor.IsSameConstructAs(program.Me));
            TerminalSystem.GetBlocksOfType(Generators, (generator) => generator.IsSameConstructAs(program.Me));
        }

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!battcharge", (b) =>
            {
                var batt = BatteryCharge();
                b.Data = batt != bad ? batt.ToString("#0.##%") : invalid;
            });
            commands.Add("!fissionrate", (b) =>
            {
                var rate = 0d;
                b.Data = Inventory.TryGetUseRate(ref uraniumIngot, ref savedUranium, ref Reactors, out rate) ? $"{rate:000.0} kg/s" : "0 kg/s";
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
        Dictionary<long, IMyTerminalBlock[]> WeaponGroups = new Dictionary<long, IMyTerminalBlock[]>();
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
            commands.Add("!weaponcharge%", (b) =>
            {
                if (justStarted) AddWeaponGroup(b);
                if (WeaponGroups.ContainsKey(b.UniqueID))
                    UpdateWeaponCharge(ref b);
            });

        }

        #endregion

        void AddWeaponGroup(SpriteData d)
        {
            var list = new List<IMyTerminalBlock>();
            TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                if (b.IsSameConstructAs(Program.Me) && b.CustomName.Contains(d.Data))
                    list.Add(b);
                return true;
            });
            if (list.Count > 0) WeaponGroups.Add(d.UniqueID, list.ToArray());
        }

        void UpdateWeaponCharge(ref SpriteData d)
        {
            d.Data = WeaponGroups[d.UniqueID][0].CustomName.ToUpper().TrimStart(tag.ToCharArray()) + (!api.IsWeaponReadyToFire(WeaponGroups[d.UniqueID][0]) ? " CYCLE" : " RDY");

            if (WeaponGroups[d.UniqueID].Length > 1)
            {
                for (int i = 1; i < WeaponGroups[d.UniqueID].Length; i++)
                    d.Data += '\n' + WeaponGroups[d.UniqueID][i].CustomName.ToUpper().TrimStart(tag.ToCharArray()) + (!api.IsWeaponReadyToFire(WeaponGroups[d.UniqueID][0]) ? " CYCLE" : " RDY");
                }
            }

    }
}
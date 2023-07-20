using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
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
        internal MyGridProgram Program;
        internal IMyGridTerminalSystem TerminalSystem;
        internal const char
            commandSplit = '$',
            space = ' ';
        internal string invalid = "[NULL]";
        internal StringBuilder Builder;
        internal TimeSpan DeltaT
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
        public static List<IMyGasTank> 
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
        public static MyItemType Ice = new MyItemType("MyObjectBuilder_Ore", "Ice");
        public double lastHydrogen = 0, lastIce = 0;
        public TimeSpan lastTime = TimeSpan.Zero;

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
            b.Data = HydrogenStatus().ToString("#0.##%"));

            commands.Add("!o2%", (b) =>
                b.Data = OxygenStatus().ToString("#0.##%"));

            commands.Add("!h2t", (b) =>
                b.Data = HydrogenTime());

            commands.Add("!ice", (b) =>
                b.Data = IceRate(InventoryUtilities.InventoryBlocks));
        }

        #endregion
        public double HydrogenStatus()
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

        public double OxygenStatus()
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

        public string HydrogenTime()
        {
            if (lastTime == TimeSpan.Zero)
            {
                lastTime = DeltaT;
                return invalid;
            }  
            var current = lastTime + DeltaT;
            //program.Me.CustomData += $"LAST {lastTime} CURRENT {current}\n";
            var pct = HydrogenStatus();
            //program.Me.CustomData += $"PCT {pct}\n";
            var rate = MathHelperD.Clamp(lastHydrogen - pct, 1E-50, double.MaxValue) / (current - lastTime).TotalSeconds;
            //program.Me.CustomData += $"RATE {rate}\n";
            var value = pct / rate;
            lastHydrogen = HydrogenStatus();
            if (rate < 1E-15 || double.IsNaN(value) || double.IsInfinity(value))
                return invalid;
            var time = TimeSpan.FromSeconds(value);
            return string.Format("{0,2:D2}h {1,2:D2}m {2,2:D2}s", (long)time.TotalHours, (long)time.TotalMinutes, (long)time.Seconds); 
        }
        public void LastTimeUpdate()
        {
        lastTime += DeltaT;
        }
        public string IceRate(List<IMyTerminalBlock> blocks)
        {
            var current = lastTime + DeltaT;
            var amt = 0;
            foreach (var block in blocks)
                InventoryUtilities.TryGetItem(block, ref Ice, ref amt);
            var rate = Math.Abs((lastIce - amt) / (current - lastTime).TotalSeconds);
            lastIce = amt;
            if (rate < 0) return invalid;
            return rate.ToString("0,4") + " kg/s";
        }
    }

    public class InventoryUtilities : InfoUtility
    {
        public static string myObjectBuilderString = "MyObjectBuilder";
        public static List<IMyTerminalBlock>InventoryBlocks = new List<IMyTerminalBlock>();
        public Dictionary<long, MyItemType[]> ItemStorage = new Dictionary<long, MyItemType[]>();

        string[] ammoNames = new string[]
        {
            "[ACN",
            "[GAT",
            "[RKT",
            "[ASL",
            "[ART",
            "[SRG",
            "[LRG"
        };

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

        public static bool TryGetItem<T>(T block, ref MyItemType itemType, ref int total)
            where T : IMyEntity
        {
            var initial = total;
            if (block.HasInventory)
            {
                var inventory = block.GetInventory();
                if (!inventory.ContainItems(1, itemType))
                    return false;
                total += inventory.GetItemAmount(itemType).ToIntSafe();
            }
            if (initial == total)
                return false;
            return true;
        }
        public static bool TryGetItem<T>(T block, ref MyItemType[] items, ref int total)
            where T : IMyEntity
        {
            var initial = total;
            if (block.HasInventory)
            {
                var inventory = block.GetInventory();
            foreach (var item in items)
                {
                    if (!inventory.ContainItems(1, item))
                        return false;
                    total += inventory.GetItemAmount(item).ToIntSafe();
                }
            }
            if (initial == total)
                return false;
            return true;
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
            commands.Add("!ammo", (b) =>
            {
                var amt = 0;

                if (justStarted)
                {
                    b.Data = b.Data.Trim();
                    var ammos = new string[]
                    {
                    "AutocannonClip",
                    "NATO_25x184mm",
                    "Missile200mm",
                    "MediumCalibreAmmo",
                    "LargeCalibreAmmo",
                    "SmallRailgunAmmo",
                    "LargeRailgunAmmo"
                    };
                    foreach (var ammo in ammos)
                        if (b.Data == ammo)
                            ItemStorage.Add(b.UniqueID, new MyItemType[] { new MyItemType($"{myObjectBuilderString}_AmmoMagazine", ammo) });
                    //throw new Exception(" DIE");
                    //if (ammoType == null) ammoType = MyItemType.MakeAmmo(ammos[1]); this seem return null....

                }
                foreach (var block in InventoryBlocks)
                    TryGetItem(block, ref ItemStorage[b.UniqueID][0], ref amt);
                b.Data = amt.ToString();

                if (!b.UseStringBuilder)
                    return;

                if (b.BuilderPrepend.Length > 0)
                    b.Data = $"{b.BuilderPrepend} {b.Data}";

                if (b.BuilderAppend.Length > 0)
                    b.Data = $"{b.Data} {b.BuilderAppend}";
            });

            commands.Add("!ammos", (b) =>
            {
                if (!b.UseStringBuilder)
                    return;

                if (justStarted)
                {
                    var ammos = new string[]
                    {
                    "AutocannonClip",
                    "NATO_25x184mm",
                    "Missile200mm",
                    "MediumCalibreAmmo",
                    "LargeCalibreAmmo",
                    "SmallRailgunAmmo",
                    "LargeRailgunAmmo"
                    };
                    ItemStorage.Add(b.UniqueID, new MyItemType[7]);
                    for (int i = 0; i < 6; ++i) //it's okay!! becausawe im drunk
                        ItemStorage[b.UniqueID][i] = new MyItemType($"{myObjectBuilderString}_AmmoMagazine", ammos[i]);
                }
                for (int i = 0; i < 6; ++i)
                {
                    var amt = 0;
                    foreach (var block in InventoryBlocks)
                    {
                        TryGetItem(block, ref ItemStorage[b.UniqueID][i], ref amt);
                    }

                    if (amt > 0)
                    {
                        var data = amt.ToString();
                        Builder.AppendLine($"{ammoNames[i]} {data}{b.BuilderAppend}");
                    }
                }

                b.Data = Builder.ToString();
                Builder.Clear();
            });

            commands.Add("!item", (b) =>
            {
                var amt = 0;
                MyItemType itemType = new MyItemType();
                if (justStarted)
                    if (b.Data.Contains(commandSplit))
                    {
                        b.Data = b.Data.Trim();
                        var stringParts = b.Data.Split(commandSplit);
                        itemType = new MyItemType($"{myObjectBuilderString}_{stringParts[0]}", stringParts[1]);
                    }
                foreach (var block in InventoryBlocks)
                    TryGetItem(block, ref itemType, ref amt);
                b.Data = amt.ToString();
            });
        }
        #endregion
    }
    public class FlightUtilities : InfoUtility
    {
        //IMyCubeGrid Ship; //fuvckoff
        IMyShipController Controller;
        Vector3D VZed = Vector3D.Zero;

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
            commands.Add("!aoa", (b) => b.Data = GetAoA());
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

        public string GetAoA()
        {
            var grav = VZed;
            if (!GravCheck(out grav))
                return invalid;
            var gridMatrix = Controller.WorldMatrix;
            var fwd = Controller.WorldMatrix.Forward;
            fwd.Normalize();
            grav.Normalize();
            var aoa = Math.Acos(MathHelper.Clamp(fwd.Dot(fwd.Dot(grav) * grav), -1, 1)); //i did it on paper. dont ask = )
            MathHelper.ToDegrees(aoa);
            return aoa.ToString("{0,4}:F2") + "°";

        }
    }
}
using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.AI.Logic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRageMath;
using VRageRender;

namespace IngameScript
{

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
    public static class SharedUtilities
    {
        static public SpriteType defaultType = SpriteType.TEXT;
        static public UpdateFrequency defaultUpdate = UpdateFrequency.None;
        static public Color defaultColor = Color.HotPink;

        public static UpdateFrequency UpdateConverter(UpdateType source)
        {
            var updateFrequency = UpdateFrequency.None; //0000
            if ((source & UpdateType.Update1) != 0) updateFrequency |= UpdateFrequency.Update1; //0001
            if ((source & UpdateType.Update10) != 0) updateFrequency |= UpdateFrequency.Update10; //0010
            if ((source & UpdateType.Update100) != 0) updateFrequency |= UpdateFrequency.Update100;//0100
            return updateFrequency;
        }
        public static bool TryGetItem<T>(T block, MyItemType itemType, ref int total)
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
        public static void SetupBarGraph(ref SpriteData sprite, float pctData)
        {
            if (sprite.SpriteSizeX > sprite.SpriteSizeY)
                sprite.SpriteSizeX *= pctData;
            else if (sprite.SpriteSizeY > sprite.SpriteSizeX)
                sprite.SpriteSizeY *= pctData;
        }
    }

    public class DisplayIniKeys //avoid allocating new memory for every display (i hope). just seems less retarded.
    {
        public string
            ScreenSection,
            SpriteSection,
            ListKey,
            TypeKey,
            DataKey,
            SizeKey,
            AlignKey,
            PositionKey,
            RotationScaleKey,
            ColorKey,
            FontKey,
            CommandKey,
            UpdateKey,
            BuilderKey,
            PrependKey,
            AppendKey;
        public char
            l_coord = '(',
            r_coord = ')',
            new_line = '\n',
            new_entry = '>';
        public void ResetKeys() //yes...this is questionable...
        {
            ScreenSection = "SECT_SCREEN";
            SpriteSection = "SECT_SPRITE";
            ListKey = "K_LIST";
            TypeKey = "K_TYPE";
            DataKey = "K_DATA";
            SizeKey = "K_SIZE";
            AlignKey = "K_ALIGN";
            PositionKey = "K_COORD";
            RotationScaleKey = "K_ROTSCAL";
            ColorKey = "K_COLOR";
            FontKey = "K_FONT";
            CommandKey = "K_CMD";
            UpdateKey = "K_UPDT";
            BuilderKey = "K_BUILD";
            PrependKey = "K_PREP";
            AppendKey = "K_APP";
        }

    }
}
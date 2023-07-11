using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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
    public class InventoryProvider
    {
        #region ItemTypesList

        // TypeIDs:
        // MyObjectBuilder_Ingot
        // MyObjectBuilder_AmmoMagazine
        // MyObjectBuilder_Component
        //
        // SubtypeIDs:
        // ------------AMMOMAGAZINES------------
        // NATO_25x184mm [gat]
        // Missile200mm
        // AutocannonClip
        // MediumCalibreAmmo [assault]
        // LargeCalibreAmmo [shartillery]
        // SmallRailgunAmmo
        // LargeRailgunAmmo

        // NATO_5p56x45mm [depricated]
        // SemiAutoPistolMagazine
        // FullAutoPistolMagazine
        // ElitePistolMagazine
        // AutomaticRifleGun_Mag_20rd
        // RapidFireAutomaticRifleGun_Mag_50rd <-THE INTERIOR TURRET ONE
        // PreciseAutomaticRifleGun_Mag_5rd
        // UltimateAutomaticRifleGun_Mag_30rd

        //------------COMPONENTS  ------------
        // Construction
        // MetalGrid
        // InteriorPlate
        // SteelPlate
        // Girder
        // SmallTube
        // LargeTube
        // Display
        // BulletproofGlass
        // Superconductor
        // Computer
        // Reactor
        // Thrust
        // GravityGenerator
        // Medical
        // RadioCommunication
        // Detector
        // Explosives
        // SolarCell
        // PowerCell
        // Canvas
        // Motor

        // Northwind
        // R75ammo          - Railgun 75mm)
        // R150ammo         - Railgun 150mm)
        // R250ammo         - Railgun 250mm)
        // H203Ammo         - 203mm HE)
        // H203AmmoAP       - 203mm AP)
        // C30Ammo          - 30mm Standard)
        // C30DUammo        - 30mm Dep. Uranium)
        // CRAM30mmAmmo     - C-RAM (CIWS?))
        // C100mmAmmo       - 100mm HE)
        // C300AmmoAP       - 300mm AP)
        // C300AmmoHE       - 300mm HE)
        // C300AmmoG        - 300mm Guided)
        // C400AmmoAP       - 400mm AP)
        // C400AmmoHE       - 400mm HE)
        // C400AmmoCluster  - 400mm Cluster)

        // MWI Homing 
        // TorpedoMk1           - M-1 Launcher
        // DestroyerMissileX    - M-8 Launcher

        // Kharak [OUTDATED]
        //
        // MyObjectBuilder_AmmoMagazine
        //
        // NATO_25x184mm
        // NATO_5p56x45mm
        // Ballistics_Flak
        // Ballistics_Cannon
        // Ballistics_Railgun
        // Ballistics_MAC
        // Missile200mm
        // Missiles_Missile
        // Missiles_Torpedo

        #endregion


    }
}
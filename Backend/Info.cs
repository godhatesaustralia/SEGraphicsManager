using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    public class DataGroup : IEquatable<DataGroup>
    {
        public string Name;
        public string[] Tags, Formats;
        public long[] Keys;
        public bool Equals(DataGroup g) => Name == g.Name;
    }

    public interface IInfo
    {
        double Data {get;}
        bool Changed {get;}
        void Update();
    }

    public class Info : IInfo
    {
        public double Data {get; protected set;}
        public readonly long EID;
        public readonly double Capacity;
        public bool Changed => Data != _lastData;
        protected double _lastData;
        Func<double> update;
        public Info(Func<double> u = null, double c = -1)
        {
            update = u;
            Capacity = c;
        }
        public void Update()
        {
            _lastData = Data;
            Data = update();
        }
    }

    public partial class Program : MyGridProgram
    {
        string[] _dataKeys;
        Dictionary<long, IInfo> _data = new Dictionary<long, IInfo>();
        Dictionary<int, DataGroup> _mappedGroups = new Dictionary<int, DataGroup>();
        Dictionary<string, DataGroup> _groups = new Dictionary<string, DataGroup>();
        Dictionary<string, IMyTerminalBlock> _nameToBlock = new Dictionary<string, IMyTerminalBlock>();
        DataGroup _tmp = new DataGroup();

        List<IMyGasGenerator> _o2h2 = new List<IMyGasGenerator>();
        ItemInfo IceOre, Fuel;
        const int KEEN = 5;
        const double TOL = 0.01, MAX_JD_PWR = 1 / 3E6, BAD = double.NaN;
        DateTime _h2TS, _stopTS, _accelTS; // fuck you
        double
            _lastDist, _maxDist, _lastAccel, _maxAccel, _totalCharge,
            _lastH2, // sigh
            _pmax;
        Vector3D VZed = Vector3D.Zero, lastVel, grav;
        UseRate U, Ice = new UseRate("Ore!Ice");

        const string
            _B = "_bar", _P = "_pct", _T = "_time",
            H2 = "h2", O2 = "o2",
            HZ = "horiz", A = "accel",  AF = "alt_surf", AC = "alt_sea", ST = "stop",
            J = "jdc";

        void UpdateData(SpriteData d)
        {
            DataGroup g;
            if (!_mappedGroups.TryGetValue(d.uID, out g))
            {
                d.Data = Lib.IVD;
                return;
            }

            for (int i = g.Keys.Length - 1; i >= 0; i--)
            {

            }

        }

        DataGroup CreateGroup<T>(string n, string d, ref Dictionary<string, T> blocks)
        where T : IMyTerminalBlock
        {
            if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(d))
                return null;
            var a = d.Split('\n');
            int i = 0, c = a.Length;

            if (a == null || c == 0)
                return null;
            string[][] a2 = new string[c][];

            var g = new DataGroup { Name = n, Keys = new long[c], Tags = new string[c] };
            for (; i < c; i++)
            {
                a2[i] = a[i].Split('\n');
                g.Tags[i] = a2[i][0];
                g.Keys[i] = blocks[a2[i][1]].EntityId;
                g.Formats[i] = a2[i][2];
            }
            return g;
        }

        bool Collect<T>(T b, Func<double> d)
        where T : IMyTerminalBlock
        {
            if (b.IsSameConstructAs(Me))
            {
                _nameToBlock[b.CustomName] = b;
                _data[b.EntityId] = new Info(d);
            }
            return false;
        }

        void GetList<T>(ref List<IMyTerminalBlock> l)
        where T : class // this is retardedv thnkas keen
        {
            l.Clear();
            GTS.GetBlocksOfType<T>(l, b => b.IsSameConstructAs(_g.Me));
        }

        public void GetBlocks()
        {
            var tempBlocks = new List<IMyTerminalBlock>();
            GTS.GetBlocksOfType<IMyGasGenerator>(tempBlocks, b => b.IsSameConstructAs(_g.Me));
            GTS.GetBlocksOfType<IMyGasTank>(null, b => Collect(b, () => b.FilledRatio));


            
            IceOre = new ItemInfo("Ore", "Ice", _o2h2.ToList<IMyTerminalBlock>());

            GTS.GetBlocksOfType<IMyJumpDrive>(null, b => Collect(b, () => b.CurrentStoredPower));



        }

        public void Setup(ref Dictionary<string, Action<SpriteData>> cmd)
        {
            cmd[H2] = b =>
                b.SetData(_data[H2].Data);
            cmd[H2 + _P] = b =>
                b.SetData(_data[H2].Data, "#0.#%");
            cmd[H2 + _B] = b =>
            {
                if (_g.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, _data[H2].Data);
            };

            cmd[O2] = b =>
                b.SetData(_data[O2].Data);
            cmd[O2 + _P] = b =>
                b.SetData(_data[O2].Data, "#0.#%");
            cmd[O2 + _B] = b =>
            {
                if (_g.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, _data[O2].Data);
            };

            cmd[H2 + _T] = b =>
            {
                var t = HydrogenTime();
                if (t == TimeSpan.Zero)
                    b.Data = Lib.IVD;
                else if (t.TotalHours >= 1)
                    b.Data = string.Format("{0,2}h {1,2}m", (long)t.TotalHours, (long)t.Minutes);
                else
                    b.Data = string.Format("{0,2}m {1,2}s", (long)t.TotalMinutes, (long)t.Seconds);
            };

            GetHorizonAngle();
            Accel();
            GetAlt(MyPlanetElevation.Sealevel);
            StoppingDist();

            cmd[HZ] = b =>
                 Validate(GetHorizonAngle(), ref b, "-#0.##; +#0.##" + "°");

            cmd[AC] = b =>
                Validate(GetAlt(MyPlanetElevation.Sealevel), ref b, Lib.IVD);

            cmd[AF] = b =>
                Validate(GetAlt(MyPlanetElevation.Surface), ref b, Lib.IVD);

            cmd[ST] = b =>
                Validate(StoppingDist(), ref b, Lib.IVD);

            cmd[A] = b => Validate(Accel(), ref b, Lib.IVD);

            cmd[J] = b =>
            {
                if (Startup && !_mappedGroups.ContainsKey(b.uID))
                {
                    _tmp.Name = b.Data;
                    if (b.Data.Contains(Lib.CMD))
                    {
                        var a = b.Data.Split(Lib.CMD);
                        _mappedGroups.Add(b.uID, new DataGroup { Name = b.Name, Tags = new string[] { a[0] }, Keys = new int[] { a[1].GetHashCode() }, Formats = new string[] { b.Format } });
                    }
                    else if (_groups.Contains(_tmp))


                }
            };
            cmd.Add("!jdc", b => { if (_jdDict.ContainsKey(b.Key)) b.SetData(_jdDict[b.Key].Data); });
            cmd["!jdc%"] = b => { if (_jdDict.ContainsKey(b.Key)) b.SetData(_jdDict[b.Key].Data * MAX_JD_PWR); });
            commands.Add("!jdcb", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, _jdDict[b.Key].Data * MAX_JD_PWR);
            });

            commands.Add("!jcharge%", b => b.SetData(totalCharge * MAX_JD_PWR, "#0.#%"));
            commands.Add("!jchargeb", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, totalCharge * MAX_JD_PWR);
            });
        }

        void Validate(double v, ref SpriteData d, string def = "")
        {
            if (double.IsNaN(v)) d.Data = Lib.IVD;
            else d.SetData(v, def);
        }

        #region gas

        double TankStatus(ref List<IMyGasTank> tanks, bool pct)
        {
            var amt = 0d;
            var total = amt;
            for (int i = 0; i < tanks.Count; i++)
            {
                amt += tanks[i].FilledRatio * tanks[i].Capacity;
                total += tanks[i].Capacity;
            }
            return pct ? amt / total : amt;
        }

        TimeSpan HydrogenTime()
        {
            var ts = DateTime.Now;
            double
                d = _data[H2].Data,
                r = MathHelperD.Clamp(_lastH2 - d, 1E-50, double.MaxValue) / (ts - _h2TS).TotalSeconds,
                v = d / r;

            _lastH2 = d;
            _h2TS = ts;

            if (r < 1E-15 || double.IsNaN(v) || v > 1E5)
                return TimeSpan.Zero;
            return TimeSpan.FromSeconds(v);
        }
        #endregion

        #region power
        double Output()
        {
            var sum = 0f;
            _pmax = 0;
            for (int i = 0; i < AllPower.Count; i++)
            {
                if (!AllPower[i].IsFunctional || !AllPower[i].Enabled)
                    continue;

                sum += AllPower[i].CurrentOutput;
                _pmax += AllPower[i].MaxOutput;
            }
            return sum;

        }
        #endregion

        #region flight
        bool GravCheck(out Vector3D grav) //wanted something nice and neat
        {
            grav = Controller.GetNaturalGravity();
            if (grav == VZed)
                return false;
            return true;
        }

        double GetHorizonAngle()
        {
            if (!GravCheck(out grav))
                return BAD;
            if (grav == VZed)
                return BAD;
            grav.Normalize();
            return Math.Asin(MathHelper.Clamp(Controller.WorldMatrix.Forward.Dot(grav), -1, 1));
        }

        double GetAlt(MyPlanetElevation elevation)
        {
            if (!GravCheck(out grav))
                return BAD;
            var alt = 0d;
            if (Controller.TryGetPlanetElevation(elevation, out alt))
                return alt;
            return BAD;
        }

        double Accel()
        {
            var ret = 0d;
            var ts = DateTime.Now;
            if (Startup)
                return BAD;

            var current = Controller.GetShipVelocities().LinearVelocity;
            if (current.Length() < 0.037)
            {
                _lastAccel = 0;
                return BAD;
            }

            var mag = (current - lastVel).Length();
            if (mag > TOL)
                ret += mag / (ts - _accelTS).TotalSeconds;

            if (ret > _maxAccel) _maxAccel = ret;

            _lastAccel = ret;
            _accelTS = ts;

            return ret <= TOL ? _maxAccel : ret;
        }

        double StoppingDist()
        {
            double
                ret = _lastDist,
                a = 0;
            var ts = DateTime.Now;

            var current = Controller.GetShipVelocities().LinearVelocity;
            if (!Startup)
            {
                if (!Controller.DampenersOverride || current.Length() < 0.037) return BAD;
                var mag = (lastVel - current).Length();
                if (mag > TOL)
                {
                    a = mag / (ts - _stopTS).TotalSeconds;
                    ret = current.Length() * current.Length() / (2 * a);
                    _lastDist = ret;
                }
            }

            if (ret > _maxDist) _maxDist = ret;

            lastVel = current;
            _stopTS = ts;

            return a <= TOL ? _maxDist : ret;
        }
        #endregion

    }


    // SO...command formatting. Depends on the general command, but here's the idea
    // this is all for the DATA field of the sprite.
    // <required param 1>$<required param 2>$...$<required param n>

    // NEW COMMAND FORMATTING
    // !cmd format unit sp_data1 sp_data2
    // CAN WE EVEN DO IT?!?!?!?!?!?

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
            GCM.Terminal.GetBlocksOfType(HydrogenTanks, (b) => b.IsSameConstructAs(GCM.Me) && b.BlockDefinition.SubtypeId.Contains("Hyd"));
            GCM.Terminal.GetBlocksOfType(OxygenTanks, (b) => !HydrogenTanks.Contains(b) && b.IsSameConstructAs(GCM.Me));
            GCM.Terminal.GetBlocksOfType(Gens, (b) => b.IsSameConstructAs(GCM.Me));
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
                if (t.TotalHours >= 1)
                    b.Data = string.Format("{0,2}h {1,2}m", (long)t.TotalHours, (long)t.Minutes);
                else
                    b.Data = string.Format("{0,2}m {1,2}s", (long)t.TotalMinutes, (long)t.Seconds);
                //else
                //    b.SetData(t.TotalSeconds);
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
        double HydrogenStatus() => TankStatus(ref HydrogenTanks, true);
        double OxygenStatus() => TankStatus(ref OxygenTanks, true);

        double TankStatus(ref List<IMyGasTank> tanks, bool pct)
        {
            var amt = 0d;
            var total = amt;
            for (int i = 0; i < tanks.Count; i++)
            {
                amt += tanks[i].FilledRatio * tanks[i].Capacity;
                total += tanks[i].Capacity;
            }
            return pct ? amt / total : amt;
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

    public class InventoryItem
    {
        public double Data{get; protected set;}
        public bool Changed => Data != _lastData;
        protected double _lastData = 0;
        public readonly MyItemType Type;
        public readonly int ID;
        public readonly string Tag, Format;
        public InventoryItem(string t, string st, string f = "")
        {
            Tag = t;
            Format = f;
            if (st == InventoryUtilities.J)
                return;
            Type = new MyItemType(InventoryUtilities.objBldr + '_' + Tag, st);
            ID = Type.GetHashCode();
        }
        protected InventoryItem(InventoryItem d)
        {
            Tag = d.Tag;
            Format = d.Format;
            Type = d.Type;
            ID = d.ID;
        }
        public void AddAmount(int i) => Data += i;
        public void Clear()
        {
            _lastData = Data;
            Data = 0;
        }
        public string ToString(bool f = false) => f ? Data.ToString(Format) : Data.ToString();
    }

    public class ItemInfo : InventoryItem, IInfo
    {
        List<IMyTerminalBlock> _invBlocks = null;
        public ItemInfo(string t, string st, List<IMyTerminalBlock> i = null) : base(new InventoryItem(t, st))
        {
            if (i != null)
                _invBlocks = i.ToList();
        }
        public ItemInfo(InventoryItem d, List<IMyTerminalBlock> i = null) : base(d)
        {
            if (i != null)
                _invBlocks = i.ToList();
        }
        public void Update()
        {
            int i = _invBlocks.Count - 1, q;
            Data = 0;
            for (; i >= 0; i--)
            {
                q = 0;
                if (_invBlocks[i].Closed)
                    _invBlocks.RemoveAtFast(i);
                var inv = _invBlocks[i]?.GetInventory();
                if (inv == null)
                    continue;
                q = inv.GetItemAmount(Type).ToIntSafe();
                Data += q;
            }
        }
    }

    public class InventoryUtilities
    {
        public const string objBldr = "MyObjectBuilder", J = "JIT";
        
        public string Section, DebugString;
        int updateStep = 5, ibPtr;
        bool ignoreTanks, ignoreGuns, ignoreSMConnectors, vanilla;
        public bool needsUpdate;
        List<IMyTerminalBlock> InventoryBlocks = new List<IMyTerminalBlock>();
        public int Count => InventoryBlocks.Count;
        Dictionary<int, string[]>
            itemKeys = new Dictionary<int, string[]>(),
            itemTags = new Dictionary<int, string[]>();
        Program _p;
        DataGroup[] _itemGroups;
        List<MyInventoryItem> ItemScan = new List<MyInventoryItem>();
        // you know i had to do it to em
        public Dictionary<int, InventoryItem> Items = new Dictionary<int, InventoryItem>();
        Dictionary<int, DataGroup> _itemGroupMap = new Dictionary<int, DataGroup>();

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
            int key;
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
                key = ItemScan[j].Type.GetHashCode();
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

        void BindItemGroup(int id, string key)
        {
            if (_itemGroupMap.ContainsKey(id))
                return;
            for (int i = 0; i < _itemGroups.Length; i++)
            {
                var igrp = _itemGroups[i];
                if (igrp.Name == key)
                    _itemGroupMap.Add(id, igrp);
            }
        }

        void UpdateItemString(int id, ref SpriteData d)
        {
            if (!_itemGroupMap.ContainsKey(id))
                return;
            d.Data = "";
            var g = _itemGroupMap[id];
            d.Data = $"{g.Tags[0]}{Items[g.Keys[0]].ToString(true)}";
            for (int i = 1; i < g.Keys.Length; i++)
                d.Data += $"\n{g.Tags[i]}{Items[g.Keys[i]].ToString(true)}";
        }

        #region UtilityBase

        public void Reset(Program program)
        {
            _p = program;
            InventoryBlocks.Clear();
            _p.GTS.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                if (b.BlockDefinition.SubtypeId == "LargeInteriorTurret")
                    return false;
                if (ignoreTanks && b is IMyGasTank)
                    return false;
                if (ignoreGuns && b is IMyUserControllableGun)
                    return false;
                if (ignoreSMConnectors && b is IMyShipConnector && b.WorldVolume.Radius <= 0.5)
                    return false;
                if (b.HasInventory && b.IsSameConstructAs(_p.Me))
                    InventoryBlocks.Add(b);
                return true;
            });

            using (var p = new IniWrap())
                if (!p.CustomData(_p.Me))
                {
                    vanilla = p.Bool(Section, "nilla", false);
                    ignoreTanks = p.Bool(Section, "ignoreTanks", true);
                    ignoreSMConnectors = p.Bool(Section, "ignoreSMC", true);
                    ignoreGuns = vanilla & p.Bool(Section, "nogunz", true);
                    updateStep = p.Int(Section, "invStep", 17);
                    var grps = p.String(Section, _p.isCringe ? "itemGroups_v" : "itemGroups");
                    if (!string.IsNullOrEmpty(grps))
                    {
                        var a = grps.Split('\n');
                        _itemGroups = new DataGroup[a.Length];

                        int i = 0, j = 0;
                        for (; i < a.Length; i++)
                        {
                            var s = p.String(Section, a[i]).Split('\n');
                            if (s.Length > 0)
                            {
                                var k = new long[s.Length];
                                var t = new string[s.Length];

                                for (; j < s.Length; i++)
                                {
                                    var istr = s[j].Split(Lib.CMD);
                                    InventoryItem item = null;
                                    try
                                    {
                                        if (istr.Length == 4)
                                            item = new InventoryItem(istr[1], istr[2], istr[3]);
                                        else if (a.Length == 3)
                                            item = new InventoryItem(istr[1], istr[2]);
                                        else
                                        {
                                            t[i] = "";
                                            continue;
                                        }
                                        Items.Add(item.ID, item);
                                    }
                                    catch (Exception) // keen dict problem
                                    { continue; }

                                    k[i] = item?.ID ?? 0;
                                    t[i] = a.Length != 3 && a.Length != 4 ? "" : istr[0] + " ";
                                }

                                _itemGroups[i] = new DataGroup
                                {
                                    Name = a[i],
                                    Keys = k,
                                    Tags = t
                                };
                            }
                        }
                    }
                }
        }

        public void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
            // JIT
            var d = 0d;
            var l = new List<IMyTerminalBlock>();
            var jitItem = new InventoryItem("Ingot", J);
            jitItem.AddAmount(0);
            jitItem.Clear();
            var jitinfo = new ItemInfo(jitItem);
            var jitR = new UseRate(J);
            BindItemGroup(Lib.SJIT.uID, J);
            UpdateItemString(Lib.SJIT.uID, ref Lib.SJIT);
            TryGetUseRate(ref jitR, ref jitinfo, out d);

            itemKeys.Clear();
            itemTags.Clear();
            Items.Clear();

            commands["!item"] = b =>
            {
                if (_p.Startup && b.Data != Lib.IVD)
                    try
                    {
                        if (b.Data.Contains(Lib.CMD) && !_itemGroupMap.ContainsKey(b.uID))
                        {
                            b.Data = b.Data.Trim();
                            var s = b.Data.Split(Lib.CMD);
                            int c = s[0].GetHashCode() + s[1].GetHashCode();
                            if (!Items.ContainsKey(c))
                                Items.Add(c, new InventoryItem(s[0], s[1], s.Length == 3 ? s[2] : ""));

                            _itemGroupMap.Add(b.uID, new DataGroup { Name = b.Name, Keys = new int[] { c } });
                            b.Data = Lib.IVD;
                        }
                    }
                    catch (Exception)
                    { throw new Exception($"\nError in data for sprite {b.Name} - invalid item key: {b.Data}."); }
                b.SetData(Items[_itemGroupMap[b.uID].Keys[0]].Data);
            };

            commands["!itemslist"] = b =>
            {
                if (_p.Startup && b.Data != "" && b.Data != Lib.IVD)
                {
                    BindItemGroup(b.uID, b.Data);
                    b.Data = Lib.IVD;
                    return;
                }
                UpdateItemString(b.uID, ref b);
            };

            commands["!invdebug"] = b =>
            {
                if (!_p.Startup)
                    return;
                b.Data = $"INVENTORIES = {InventoryBlocks.Count}\n";
                b.Data += ignoreGuns ? "WEAPONS NOT COUNTED" : "CHECKING WEAPONS";
            };
        }

        public void Update()
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
            for (; ibPtr < m; ibPtr += 1)
                ScanInventories();
            if (m == InventoryBlocks.Count)
            {
                ibPtr = 0;
                needsUpdate = false;
            }
        }

        #endregion
    }


    public class FlightUtilities : UtilityBase
    {
        List<IMyJumpDrive> JumpDrives = new List<IMyJumpDrive>();
        double lastDist, maxDist, lastAccel, maxAccel, totalCharge;

        const double TOL = 0.01, MAX_JD_PWR = 1 / 3E6;
        readonly string tag, ctrl, fmat;
        string std;
        DateTime stopTS, accelTS; // fuck you
        Vector3D VZed = Vector3D.Zero, lastVel, grav;
        Dictionary<int, Info> _jdVals = new Dictionary<int, Info>();
        Dictionary<int, DataGroup> _jdGroupMap = new Dictionary<int, DataGroup>();
        public FlightUtilities(string s, string f = "flightFMAT")
        {
            name = "Flight";
            tag = s;
            fmat = f;
        }

        #region UtilityBase


        public override void GetBlocks()
        {
            using (var p = new IniWrap())
            {
                p.CustomData(GCM.Me);
                std = p.String(tag, fmat, "0000");
                GCM.Terminal.GetBlocksOfType(JumpDrives, b => b.IsSameConstructAs(Program.Me));
                foreach (var j in JumpDrives)
                    _jdVals.Add(j.CustomName.GetHashCode(), new Info(j.CustomName, () => j.CurrentStoredPower));
            }
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {
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
            commands.Add("!jdc", b =>
            {
                if (GCM.justStarted && !_jdGroupMap.ContainsKey(b.uID))
                {
                    if (b.Data.Contains(cmd))
                    {
                        var a = b.Data.Split('\n');
                        var ints = new List<int>();
                        foreach (var ln in a)
                        {
                            ln.Trim('|');
                            foreach (var kvp in _jdVals)
                                if (kvp.Value.Name == ln)
                                    ints.
                        }
                    }

                }
            });
            commands.Add("!jdc", b => { if (_jdDict.ContainsKey(b.Key)) b.SetData(_jdDict[b.Key].Data); });
            commands.Add("!jdc%", b => { if (_jdDict.ContainsKey(b.Key)) b.SetData(_jdDict[b.Key].Data * MAX_JD_PWR); });
            commands.Add("!jdcb", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, _jdDict[b.Key].Data * MAX_JD_PWR);
            });

            commands.Add("!jcharge%", b => b.SetData(totalCharge * MAX_JD_PWR, "#0.#%"));
            commands.Add("!jchargeb", b =>
            {
                if (GCM.justStarted)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, totalCharge * MAX_JD_PWR);
            });

        }

        public override void Update()
        {
            for (int i = JumpDrives.Count - 1; i >= 0; i--)
            {
                var n = JumpDrives[i].CustomName;
                if (JumpDrives[i].Closed)
                {
                    _jdDict.Remove(n);
                    JumpDrives.RemoveAtFast(i);
                }
                _jdDict[n].Update();
                totalCharge += _jdDict[n].Data;
            }
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
            if (mag > TOL)
                ret += mag / (ts - accelTS).TotalSeconds;

            if (ret > maxAccel) maxAccel = ret;

            lastAccel = ret;
            accelTS = ts;

            return ret <= TOL ? maxAccel : ret;
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
                if (mag > TOL)
                {
                    a = mag / (ts - stopTS).TotalSeconds;
                    ret = current.Length() * current.Length() / (2 * a);
                    lastDist = ret;
                }
            }

            if (ret > maxDist) maxDist = ret;

            lastVel = current;
            stopTS = ts;

            return a <= TOL ? maxDist : ret;
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
            GCM.Terminal.GetBlocksOfType(Batteries, (battery) => battery.IsSameConstructAs(GCM.Me));
            GCM.Terminal.GetBlocksOfType(Reactors, (reactor) => reactor.IsSameConstructAs(GCM.Me));
            GCM.Terminal.GetBlocksOfType(Engines, (generator) => generator.IsSameConstructAs(GCM.Me) && generator.CustomName.Contains("Engine"));
            GCM.Terminal.GetBlocksOfType(AllPower, (power) => power.IsSameConstructAs(GCM.Me));
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
            return charge / total;
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

    public class WeaponUtilities : UtilityBase
    {
        MyDefinitionId _pwr = new MyDefinitionId(typeof(VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Electricity");
        Dictionary<long, string[]> _wpnTags = new Dictionary<long, string[]>();
        Dictionary<long, Info> wpnData = new Dictionary<long, Info>(); // sprite uid to cached gun stats
        Dictionary<long, IMyUserControllableGun> wpns = new Dictionary<long, IMyUserControllableGun>(); // eid to gun
        public WeaponUtilities()
        {
            GCM.Terminal.GetBlocksOfType<IMyUserControllableGun>(null, b =>
            {
                var c = b.Components.Get<MyResourceSinkComponent>();
                return false;
            });
        }

        public override void GetBlocks()
        {

        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {

        }

        public override void Update()
        {

        }

    }
}
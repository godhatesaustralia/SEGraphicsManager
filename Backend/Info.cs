using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    public class Info
    {
        public readonly string Name;
        public double Data => value;
        protected double value, lastValue;
        protected bool dataChanged => value != lastValue;

        public Func<bool> IsValid = () => true;
        protected Func<double> update;
        public Info(string n = "", Func<double> u = null, Func<bool> v = null)
        {
            Name = n;
            update = u;
            IsValid = v ?? IsValid;
        }
        public virtual void Update() => value = update.Invoke();
    }

    public abstract class UtilityBase
    {
        #region fields
        protected Program _p;
        protected const char
            cmd = '!',
            bar = 'b',
            pct = '%';
        protected string invalid = "••", name;
        public string Name => name.ToUpper();
        protected List<string> _closed = new List<string>();
        protected SpriteData jitSprite = new SpriteData();
        protected const double BAD = double.NaN;
        #endregion

        public virtual void Reset(Program p)
        {
            _p = p;
            jitSprite.uID = int.MinValue;
            GetBlocks();
        }

        protected void Validate(double v, ref SpriteData d, string def = "")
        {
            if (double.IsNaN(v)) d.Data = invalid;
            else d.SetData(v, def);
        }
        public abstract void Update();

        public abstract void GetBlocks();

        public abstract void Setup(ref Dictionary<string, Action<SpriteData>> commands);


    }

    // SO...command formatting. Depends on the general command, but here's the idea
    // this is all for the DATA field of the sprite.
    // <required param 1>$<required param 2>$...$<required param n>

    // NEW COMMAND FORMATTING
    // !cmd format unit sp_data1 sp_data2
    // CAN WE EVEN DO IT?!?!?!?!?!?

    public class GasUtilities : UtilityBase
    {
        Dictionary<string, Info>
            H2Tanks = new Dictionary<string, Info>(),
            O2Tanks = new Dictionary<string, Info>();

        // UseRate Ice;
        // ItemInfo IceOre;
        // const int keen = 5;
        DateTime tsH2;
        int _totalH2, _totalO2;
        double _h2, _o2, _lastH2; // sigh

        public GasUtilities()
        {
            name = "Gas";
            // Ice = new UseRate("Ore!Ice");
        }

        #region UtilityBase

        public override void GetBlocks()
        {
            H2Tanks.Clear();
            O2Tanks.Clear();

            _p.GTS.GetBlocksOfType<IMyGasTank>(null, b =>
            {
                var n = b.CustomName;
                if (b.CubeGrid == _p.Me.CubeGrid)
                {
                    if (b.BlockDefinition.SubtypeId.Contains("Hyd") && !H2Tanks.ContainsKey(n))
                        H2Tanks[n] = new Info(n, () => b.FilledRatio, () => !b.Closed && b.IsFunctional);
                    else if (!b.BlockDefinition.SubtypeId.Contains("Hyd") && !O2Tanks.ContainsKey(n))
                        O2Tanks[n] = new Info(n, () => b.FilledRatio, () => !b.Closed && b.IsFunctional);
                }
                return false;
            });

            _totalH2 = H2Tanks.Count;
            _totalO2 = O2Tanks.Count;
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> c)
        {
            // JIT
            HydrogenTime();

            c["!h2tank%"] = b =>
            {
                if (H2Tanks.ContainsKey(b.Key)) b.SetData(H2Tanks[b.Key].Data);
            };

            c["!h2allk"] = b =>
            {
                var s = "";
                if (!_p.SetupComplete && b.Trim == 0)
                    int.TryParse(b.Format, out b.Trim);
                else foreach (var t in H2Tanks.Keys)
                        s += t.Remove(0, b.Trim) + "\n";
                
                if (s != b.Data) b.Data = s;
            };

            c["!h2ct"] = b => b.Data = $"{H2Tanks.Count}/{_totalH2}";

            c["h2all%"] = b =>
            {
                var s = "";
                if (b.Format != "") foreach (var t in H2Tanks.Values)
                        s += $"{t.Data:b.Format}\n";
                else foreach (var t in H2Tanks.Values)
                        s += $"{t.Data * 100:000}%\n";

                 if (s != b.Data) b.Data = s;
            };

            c["!h2tankb"] = b =>
            {
                if (!H2Tanks.ContainsKey(b.Key)) Lib.UpdateBarGraph(ref b, 0);
                else
                {
                    if (!_p.SetupComplete) Lib.CreateBarGraph(ref b);
                    Lib.UpdateBarGraph(ref b, H2Tanks[b.Key].Data);
                }
            };

            c["!o2tank%"] = b =>
            {
                if (O2Tanks.ContainsKey(b.Key)) b.SetData(O2Tanks[b.Key].Data);
            };

            c["!o2allk"] = b =>
            {
                var s = "";
                if (!_p.SetupComplete && b.Trim == 0)
                    int.TryParse(b.Format, out b.Trim);
                else foreach (var t in O2Tanks.Keys)
                        b.Data += t.Remove(0, b.Trim) + "\n";

                if (s != b.Data) b.Data = s;
            };

            c["!o2ct"] = b => b.Data = $"{O2Tanks.Count}/{_totalO2}";

            c["o2all%"] = b =>
            {
                var s = "";

                if (b.Format != "") foreach (var t in O2Tanks.Values)
                       s += $"{t.Data:b.Format}\n";
                else foreach (var t in O2Tanks.Values)
                        s += $"{t.Data * 100:000}%\n";
                
                if (s != b.Data) b.Data = s;
            };

            c["!o2tankb"] = b =>
            {
                if (!O2Tanks.ContainsKey(b.Key)) Lib.UpdateBarGraph(ref b, 0);
                else
                {
                    if (!_p.SetupComplete) Lib.CreateBarGraph(ref b);
                    Lib.UpdateBarGraph(ref b, O2Tanks[b.Key].Data);
                }
            };

            c["!h2%"] = b => b.SetData(_h2, "#0.#%");

            c["!o2%"] = b => b.SetData(_o2, "#0.#%");

            c["!h2b"] = b =>
            {
                if (!_p.SetupComplete)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, _h2);
            };

            c["!o2b"] = b =>
            {
                if (!_p.SetupComplete)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, _o2);
            };

            c["!h2t"] = b =>
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
            };

            // c["!ice"] = b =>
            // {
            //     var rate = 0d;
            //     b.Data = InventoryUtilities.TryGetUseRate(ref Ice, ref IceOre, out rate) ? (rate / keen).ToString("G4") : invalid;
            // };
        }

        public override void Update()
        {
            _h2 = _o2 = 0;
            foreach (var t in H2Tanks.Values)
                if (!t.IsValid()) _closed.Add(t.Name);
                else
                {
                    t.Update();
                    _h2 += t.Data;
                }

            int i = _closed.Count, c;
            for (; --i > 0;)
            {
                H2Tanks.Remove(_closed[i]);
                _closed.RemoveAt(i);
            }

            _closed.Clear();

            c = H2Tanks.Count;
            if (c > 0) _h2 /= c;

            foreach (var t in O2Tanks.Values)
                if (!t.IsValid()) _closed.Add(t.Name);
                else
                {
                    t.Update();
                    _o2 += t.Data;
                }

            i = _closed.Count;
            for (; --i > 0;)
            {
                O2Tanks.Remove(_closed[i]);
                _closed.RemoveAt(i);
            }

            _closed.Clear();

            c = O2Tanks.Count;
            if (c > 0) _o2 /= c;
        }
        #endregion

        TimeSpan HydrogenTime()
        {
            var ts = DateTime.Now;
            var rate = MathHelperD.Clamp(_lastH2 - _h2, 1E-50, double.MaxValue) / (ts - tsH2).TotalSeconds;
            var value = _h2 / rate;
            _lastH2 = _h2;
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
        IMyTerminalBlock Container;
        public ItemInfo(string t, string st, IMyTerminalBlock i = null) : base(new InventoryItem(t, st))
        {
            Container = i;
            if (i == null) return;

            IsValid = () => i.Closed;
        }
        public ItemInfo(InventoryItem d, IMyTerminalBlock i = null) : base(d)
        {
            Container = i;
            if (i == null) return;

            IsValid = () => i.Closed;
        }
        public override void Update()
        {
            //DebugString = "";
            if (Container == null || !Container.HasInventory) return;
            value = Container.GetInventory().GetItemAmount(Type).ToIntSafe();
        }
    }

    public class InventoryUtilities : UtilityBase
    {
        public static string myObjectBuilder = "MyObjectBuilder", J = "JIT";
        public string Section, DebugString;
        int updateStep = 5, ibPtr;
        bool ignoreTanks, ignoreGuns, ignoreSMConnectors, vanilla;
        string _dat;
        public bool needsUpdate;
        List<IMyTerminalBlock> InventoryBlocks = new List<IMyTerminalBlock>();
        public int Count => InventoryBlocks.Count;
        Dictionary<int, string[]>
            itemKeys = new Dictionary<int, string[]>(),
            itemTags = new Dictionary<int, string[]>();
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

        public static bool TryGetUseRate(ref UseRate r, double d, out double rate)
        {
            rate = r.Rate(d);
            if (rate <= 0) return false;
            return true;
        }

        void AddItemGroup(int id, string key)
        {
            using (var p = new IniWrap())
            {
                MyIniParseResult result;
                if (p.CustomData(_p.Me, out result))
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
                    else if (key != J)
                        throw new Exception($"key {key} for command !itemslist not found in PB custom data.");
            }

        }

        void UpdateItemString(int id, ref SpriteData d)
        {
            if (!itemKeys.ContainsKey(id))
                return;
            _dat = "";
            var itm = Items[itemKeys[id][0]];
            _dat = $"{itemTags[id][0]}{Items[itemKeys[id][0]].ToString(true)}";
            for (int i = 1; i < itemKeys[id].Length; i++)
                _dat += $"\n{itemTags[id][i]}{Items[itemKeys[id][i]].ToString(true)}";
            d.Data = _dat;
        }

        #region UtilityBase

        public override void Reset(Program m)
        {
            using (var p = new IniWrap())
                if (p.CustomData(m.Me))
                {
                    vanilla = p.Bool(Section, "nilla", false);
                    ignoreTanks = p.Bool(Section, "ignoreTanks", true);
                    ignoreSMConnectors = p.Bool(Section, "ignoreSMC", true);
                    ignoreGuns = vanilla & p.Bool(Section, "nogunz", true);
                    updateStep = p.Int(Section, "invStep", 17);
                }
            base.Reset(m);
        }

        public override void GetBlocks()
        {
            InventoryBlocks.Clear();
            _p.GTS.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                bool r;
                r = b.BlockDefinition.SubtypeId == "LargeInteriorTurret";

                r |= ignoreTanks && b is IMyGasTank;

                r |= ignoreGuns && b is IMyUserControllableGun;

                r |= ignoreSMConnectors && b is IMyShipConnector && b.WorldVolume.Radius <= 0.5;

                if (!r && b.HasInventory && b.IsSameConstructAs(_p.Me))
                    InventoryBlocks.Add(b);

                return false;
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
            TryGetUseRate(ref jitR, 0, out d);
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> c)
        {
            JIT();

            itemKeys.Clear();
            itemTags.Clear();
            Items.Clear();

            c["!item"] = b =>
            {
                if (!_p.SetupComplete && b.Data != invalid)
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
                                else if (s.Length == 3)
                                    Items.Add(b.Data, new InventoryItem(s[0], s[1], s[2]));
                            }
                            if (!itemKeys.ContainsKey(b.uID))
                            {
                                itemKeys[b.uID] = new string[] { b.Data };
                                b.Data = invalid;
                            }
                        }
                    }
                    catch (Exception)
                    { throw new Exception($"\nError in data for sprite {b.Name} - invalid item key: {b.Data}."); }
                b.SetData(Items[itemKeys[b.uID][0]].Data);
            };

            c["!itemslist"] = b =>
            {
                if (!_p.SetupComplete && b.Data != "" && b.Data != invalid)
                {
                    AddItemGroup(b.uID, b.Data);
                    b.Data = invalid;
                    return;
                }
                UpdateItemString(b.uID, ref b);
            };

            c["!invdebug"] = b =>
            {
                if (!!_p.SetupComplete)
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
        Dictionary<string, Info> JumpDrives = new Dictionary<string, Info>();
        double lastDist, maxDist, lastAccel, maxAccel;
        const double dev = 0.01;
        readonly string tag, ctrl, fmat;
        string std;
        DateTime stopTS, accelTS; // fuck you
        Vector3D VZed = Vector3D.Zero, lastVel, grav;
        double _jdTotal;

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
                p.CustomData(_p.Me);
                std = p.String(tag, fmat, "0000");
                _p.GTS.GetBlocksOfType<IMyJumpDrive>(null, b =>
                {
                    var n = b.CustomName;
                    if (b.IsSameConstructAs(_p.Me) && !JumpDrives.ContainsKey(n))
                        JumpDrives[n] = new Info(n, () => b.CurrentStoredPower / b.MaxStoredPower, () => !b.Closed && b.IsFunctional);

                    return false;
                });
            }
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> c)
        {

            GetHorizonAngle();
            Accel();
            GetAlt(MyPlanetElevation.Sealevel);
            StoppingDist();

            c["!hrz"] = b => Validate(GetHorizonAngle(), ref b, "-00; +00" + "°");

            c["!horiz"] = b => Validate(GetHorizonAngle(), ref b, "-#0.##; +#0.##" + "°");

            c["!c-alt"] = b => Validate(GetAlt(MyPlanetElevation.Sealevel), ref b, std);

            c["!s-alt"] = b => Validate(GetAlt(MyPlanetElevation.Surface), ref b, std);

            c["!stop"] = b => Validate(StoppingDist(), ref b, std);

            c["!accel"] = b => Validate(Accel(), ref b, std);

            c["!damp"] = b => b.Data = _p.Controller.DampenersOverride ? "ON" : "OFF";

            c["!jd%"] = b =>
            {
                if (JumpDrives.ContainsKey(b.Key))
                    if (b.Format != "")
                        b.SetData(JumpDrives[b.Key].Data);
                    else b.Data = $"{JumpDrives[b.Key].Data * 100:000}%";
            };

            c["!jdb"] = b =>
            {
                if (!JumpDrives.ContainsKey(b.Key)) Lib.UpdateBarGraph(ref b, 0);
                else
                {
                    if (!_p.SetupComplete) Lib.CreateBarGraph(ref b);
                    Lib.UpdateBarGraph(ref b, JumpDrives[b.Key].Data);
                }
            };

            c["!jdall%"] = b =>
            {
                var s = "";

                if (b.Format != "")
                    foreach (var j in JumpDrives.Values)
                        s += $"\n{j.Data.ToString(b.Format)}";
                else foreach (var j in JumpDrives.Values)
                        s += $"\n{j.Data * 100:000}";

                b.Data = s;
            };

            c["!jdtotal%"] = b => b.SetData(_jdTotal, "#0.#%");

            c["!jdtotalb"] = b =>
            {
                if (!_p.SetupComplete) Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, _jdTotal);
            };

        }

        public override void Update()
        {
            _jdTotal = 0;

            foreach (var jd in JumpDrives.Values)
                if (!jd.IsValid()) continue;
                else
                {
                    jd.Update();
                    _jdTotal += jd.Data;
                }

            int c = JumpDrives.Count;
            if (c > 0) _jdTotal /= c;

        }

        #endregion

        bool GravCheck(out Vector3D grav) //wanted something nice and neat
        {
            grav = _p.Controller.GetNaturalGravity();
            return grav == VZed;
        }

        double GetHorizonAngle()
        {
            if (!GravCheck(out grav)) return BAD;

            grav.Normalize();
            return Math.Asin(MathHelper.Clamp(_p.Controller.WorldMatrix.Forward.Dot(grav), -1, 1));
        }

        double GetAlt(MyPlanetElevation elevation)
        {
            if (!GravCheck(out grav)) return BAD;

            var alt = 0d;
            if (_p.Controller.TryGetPlanetElevation(elevation, out alt))
                return alt;

            return BAD;
        }

        double Accel()
        {
            var ret = 0d;
            var ts = DateTime.Now;

            if (!_p.SetupComplete) return BAD;

            var current = _p.Controller.GetShipVelocities().LinearVelocity;
            if (current.Length() < 0.037)
            {
                lastAccel = 0;
                return BAD;
            }

            var mag = (current - lastVel).Length();

            if (mag > dev) ret += mag / (ts - accelTS).TotalSeconds;

            if (ret > maxAccel) maxAccel = ret;

            lastAccel = ret;
            accelTS = ts;

            return ret <= dev ? maxAccel : ret;
        }

        double StoppingDist()
        {
            double ret = lastDist, a = 0;
            var ts = DateTime.Now;

            var current = _p.Controller.GetShipVelocities().LinearVelocity;
            if (!!_p.SetupComplete)
            {
                if (!_p.Controller.DampenersOverride || current.Length() < 0.037) return BAD;

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
    }

    public class PowerUtilities : UtilityBase
    {
        Dictionary<string, ReactorData> Reactors = new Dictionary<string, ReactorData>();
        class ReactorData
        {
            public double Max;
            public Info Current;
            public ItemInfo CurrentFuel;
            public Func<bool> IsValid;
        }

        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
        List<IMyPowerProducer>
            Engines = new List<IMyPowerProducer>(),
            AllPower = new List<IMyPowerProducer>();
        UseRate U;
        double _rpwr, _bpwr, _pmax, _fuelTotal;
        Info batt, pwr;
        const string I = "ON", O = "OFF", ur = "Ingot!Uranium";
        public PowerUtilities()
        {
            name = "Power";
            U = new UseRate(ur);
        }
        #region UtilityBase
        public override void Reset(Program p)
        {
            base.Reset(p);
            var a = ur.Split(cmd);
            _fuelTotal = 0;
        }

        public override void GetBlocks()
        {
            Batteries.Clear();
            Engines.Clear();
            AllPower.Clear();
            _p.GTS.GetBlocksOfType(Batteries, battery => battery.IsSameConstructAs(_p.Me));
            _p.GTS.GetBlocksOfType(Engines, generator => generator.IsSameConstructAs(_p.Me) && generator.CustomName.Contains("Engine"));
            _p.GTS.GetBlocksOfType(AllPower, power => power.IsSameConstructAs(_p.Me));

            Reactors.Clear();
            _p.GTS.GetBlocksOfType<IMyReactor>(null, b =>
            {
                var n = b.CustomName;
                if (b.IsSameConstructAs(_p.Me) && !Reactors.ContainsKey(n))
                    Reactors[n] = new ReactorData
                    {
                        Max = b.MaxOutput,
                        Current = new Info(n, () => b.CurrentOutput),
                        CurrentFuel = new ItemInfo("Ingot", "Uranium", b),
                        IsValid = () => !b.Closed && b.IsFunctional
                    };

                return false;
            });
        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> c)
        {
            batt = new Info("bc%", BatteryCharge);
            pwr = new Info("pwr", Output);

            c["!bcharge%"] = b => Validate(batt.Data, ref b, "#0.#%");

            c["!bchargeb"] = b =>
            {
                if (!_p.SetupComplete)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, batt.Data);
            };

            c["!rpwrMWt"] = b =>b.SetData(_rpwr);

            c["!bpwrMWt"] = b => b.SetData(_bpwr);

            c["!apwrMWt"] = b => b.SetData(pwr.Data);

            c["!gridgen%"] = b => b.SetData(pwr.Data / _pmax, "#0.#%");

            c["!gridgenb"] = b =>
            {
                if (!_p.SetupComplete)
                    Lib.CreateBarGraph(ref b);
                Lib.UpdateBarGraph(ref b, pwr.Data / _pmax);
            };

            c["!rfuel"] = b =>
            {
                if (Reactors.ContainsKey(b.Key)) b.SetData(Reactors[b.Key].CurrentFuel.Data);
            };

            c["!rpwrMW"] = b =>
            {
                ReactorData r;
                if (!Reactors.TryGetValue(b.Key, out r)) return;

                if (b.Format == "") b.Data = $"{Reactors[b.Key].Current.Data / 1E6:000}";
                else b.SetData(Reactors[b.Key].Current.Data);
            };

            c["!rpwr%"] = b =>
            {
                ReactorData r;
                if (!Reactors.TryGetValue(b.Key, out r)) return;

                if (b.Format == "") b.Data = $"{Reactors[b.Key].Current.Data / r.Max:000}%";
                else b.SetData(Reactors[b.Key].Current.Data / r.Max);
            };

            c["!fuel"] = b => b.SetData(_fuelTotal);

            c["!fission"] = b =>
            {
                var rate = 0d;
                if (!_p.SetupComplete)
                    b.Data = InventoryUtilities.TryGetUseRate(ref U, _fuelTotal, out rate) ? $"{rate:000} KG/S" : "0 KG/S";
            };

            c["!gridcap"] = b => b.SetData(1 - (pwr.Data / _pmax), "#0.#%");
        }

        public override void Update()
        {
            _fuelTotal = _rpwr = 0;
            foreach (var r in Reactors.Values)
                if (!r.IsValid()) _closed.Add(r.Current.Name);
                else
                {
                    r.Current.Update();
                    r.CurrentFuel.Update();
                    _fuelTotal += r.CurrentFuel.Data;
                    _rpwr += r.Current.Data;
                }

            for (int i = _closed.Count; --i > 0;)
            {
                Reactors.Remove(_closed[i]);
                _closed.RemoveAt(i);
            }

            batt.Update();
            pwr.Update();
        }

        #endregion
        double BatteryCharge()
        {
            if (Batteries.Count == 0) return BAD;

            double charge = _bpwr = 0, total = charge;
            for (int i = 0; i < Batteries.Count; i++)
            {
                //if (battery == null) continue;
                charge += Batteries[i].CurrentStoredPower;
                total += Batteries[i].MaxStoredPower;

                _bpwr += Batteries[i].CurrentOutput;
            }
            return charge / total;
        }

        double Output()
        {
            var sum = _pmax = 0f;

            for (int i = 0; i < AllPower.Count; i++)
            {
                if (!AllPower[i].IsFunctional || !AllPower[i].Enabled)
                    continue;

                sum += AllPower[i].CurrentOutput;
                _pmax += AllPower[i].MaxOutput;
            }
            return sum;

        }
    }

    //public class WeaponUtilities : UtilityBase
    //{
    //   // MyDefinitionId _nrg = new MyDefinitionId()
    //    Dictionary<long, string[]> wpnTags = new Dictionary<long, string[]>();
    //    Dictionary<long, Info> wpnData = new Dictionary<long, Info>(); // sprite uid to cached gun stats
    //    Dictionary<long, IMyUserControllableGun> wpns = new Dictionary<long, IMyUserControllableGun>(); // eid to gun
    //    public WeaponUtilities()
    //    {
    //        name = "Weapons";
    //        _p.GTS.GetBlocksOfType<IMyUserControllableGun>(null, b =>
    //        {
    //            var c = b.Components.Get<MyResourceSinkComponent>();
    //            return false;
    //        });
    //    }
    //}
}
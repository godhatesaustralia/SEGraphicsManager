using System.Collections.Generic;
using System;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRageMath;
using VRage;

namespace IngameScript
{
    // adrn

    public class AviationUtilities : UtilityBase // replacing most of the other shit with this since it's specialized
    {
        IMyShipController Reference => GCM.Controller;


        public AviationUtilities(string s)
        {
            name = "Avionics";
        }

        #region UtilityBase

        public override void GetBlocks()
        {

        }

        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        {

        }

        public override void Update()
        {

        }


        #endregion
        public class BlockInfo<T>// : IInfo
            where T : IMyFunctionalBlock
        {

        }
    }

    public class EngineUtilities : UtilityBase
    {
        readonly string tag;
        double totalFuelCap, lastFuel;
        DateTime fuelTS;
        Info Fuel;
        List<IMyGasTank> FuelTanks = new List<IMyGasTank>();
        Dictionary<long, string> EngineMappings = new Dictionary<long, string>();
        Dictionary<string, IMyThrust> Engines = new Dictionary<string, IMyThrust>(); // name, (0) (1) (2)
        Vector2 _circl = new Vector2();

        public EngineUtilities(string t)
        {
            name = "Thrust";
            tag = t;
        }

        #region UtilityBase

        public override void GetBlocks()
        {

            FuelTanks.Clear();
            GCM.Terminal.GetBlocksOfType(FuelTanks, b => b.IsSameConstructAs(GCM.Me));

            if (GCM.Controller != null)
            {
                IMyShipController temp = null;
                bool main = GCM.Controller.IsMainCockpit;
                if (!main)
                {
                    GCM.Terminal.GetBlocksOfType<IMyShipController>(null, b =>
                    {
                        if (b.IsMainCockpit)
                            temp = b;
                        return false;
                    });
                    GCM.Controller.IsMainCockpit = true;
                }
                if (!main)
                {
                    if (temp != null)
                        temp.IsMainCockpit = true;
                    GCM.Controller.IsMainCockpit = false;
                }
            }
        }
        public override void Setup(ref Dictionary<string, Action<SpriteData>> commands)
        // JIT+
        {
            Fuel = new Info("Fuel", () => TankStatus(false));
            commands.Add("!f%", b => TankStatus(true));
            commands.Add("!f", b => TankStatus(false));
            commands.Add("!ftime", b =>
            {
                var t = FuelBurnTime();
                if (t == TimeSpan.Zero)
                    b.Data = invalid;
                if (t.TotalHours >= 1)
                    b.Data = string.Format("{0,2}h {1,2}m", (long)t.TotalHours, (long)t.Minutes);
                else
                    b.Data = string.Format("{0,2}m {1,2}s", (long)t.TotalMinutes, (long)t.Seconds);
            });
            commands.Add("!eng", b =>
            {
                if (GCM.justStarted && !EngineMappings.ContainsKey(b.uID))
                    AddThrust(ref b);
                b.SetData(GetThrust(b.uID, false));
            }); 
            commands.Add("!eng%", b =>
            {
                if (GCM.justStarted && !EngineMappings.ContainsKey(b.uID))
                    AddThrust(ref b);
                b.SetData(GetThrust(b.uID, true));
            });
            commands.Add("!engb", b =>
            {
                if (GCM.justStarted && !EngineMappings.ContainsKey(b.uID))
                {
                    AddThrust(ref b);
                    Lib.CreateBarGraph(ref b);
                }
                var dat = GetThrust(b.uID, true);
                if (b is SpriteConditional)
                    b.SetData(dat);
                Lib.UpdateBarGraph(ref b, dat);
            });
        }

        public override void Update()
        {
            Fuel.Update();
        }

        #endregion

        void AddThrust(ref SpriteData b)
        {
                var a = GCM.Terminal.GetBlockWithName(b.Data) as IMyThrust;
                if (a != null)
                {
                    if (!Engines.ContainsKey(a.CustomName))
                        Engines[a.CustomName] =  a;
                    if (!EngineMappings.ContainsKey(b.uID))
                        EngineMappings[b.uID] = a.CustomName;
                }
        }

        double GetThrust(long uid, bool pct)
        {
            var r = 0d;
            if (EngineMappings.ContainsKey(uid))
                if (Engines[EngineMappings[uid]].Closed)
                    Engines.Remove(EngineMappings[uid]);
                else
                    r = pct ? Engines[EngineMappings[uid]].CurrentThrustPercentage : Engines[EngineMappings[uid]].CurrentThrust;
            return r;
        }
        double TankStatus(bool pct)
        {
            var amt = 0d;
            var total = amt;
            for (int i = 0; i < FuelTanks.Count; i++)
            {
                amt += FuelTanks[i].FilledRatio * FuelTanks[i].Capacity;
                total += FuelTanks[i].Capacity;
            }
            return pct ? amt / total : amt;
        }

        TimeSpan FuelBurnTime()
        {
            var ts = DateTime.Now;
            var rate = MathHelperD.Clamp(lastFuel - Fuel.Data, 1E-50, double.MaxValue) / (ts - fuelTS).TotalSeconds;
            var value = Fuel.Data / rate;
            lastFuel = Fuel.Data;
            fuelTS = ts;
            if (rate < 1E-15 || double.IsNaN(value) || value > 1E5)
                return TimeSpan.Zero;
            return TimeSpan.FromSeconds(value);
        }

    }


    //TODO: THIS SYSTEM IS ASS
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
            if (!WCPBAPI.Activate(GCM.Me, ref api))
                return;
            wcWeapons.Clear();
            foreach (var t in tagStorage.Values)
                GCM.Terminal.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
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
            GCM.Terminal.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
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
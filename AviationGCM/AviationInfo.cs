using System.Collections.Generic;
using System;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    // adrn
    public class ThrustUtilities : UtilityBase
    {
        readonly string tag;
        double totalFuelCap, lastFuel;
        DateTime fuelTS;
        Info Fuel;
        List<IMyGasTank> FuelTanks = new List<IMyGasTank>();
        Dictionary<string, long[]> EngineMappings = new Dictionary<string, long[]>(); // name, (0) (1) (2)
        Dictionary<long, IMyAirVent> Intakes = new Dictionary<long, IMyAirVent>(); // (0) engine intake
        Dictionary<long, IMyPowerProducer> Generators = new Dictionary<long, IMyPowerProducer>(); // (1) main "engine" piece
        Dictionary<long, IMyThrust> Engines = new Dictionary<long, IMyThrust>(); // (2) thruster piece
        Vector2 _circl = new Vector2();

        public ThrustUtilities(string t)
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
                var list = new List<IMyThrust>();

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
            //Fuel = new Info("Fuel", () => TankStatus(false));
            //commands.Add("!movethr", b => curThrust(ref b));
            //commands.Add("!movethr%", b => curThrust(ref b, true));
            //commands.Add("!a", b => curThrust(ref b, a: true));
        }

        public override void Update()
        {

            Fuel.Update();
        }

        #endregion

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

        void createGroup(ref List<IMyThrust> l, Base6Directions.Direction dir)
        {
            l.Clear();
            var v = Base6Directions.GetIntVector(dir);
            foreach (var t in AllThrusters)
                if (t.GridThrustDirection == v)
                    l.Add(t);

            // you never know...
            if (l.Count > 0)
            {
                var a = l.ToArray();
                int sum = 0, i = 0;
                for (; i < a.Length; i++)
                    sum += Convert.ToInt32(a[i].MaxThrust);
                dirThrust.Add(dir, a);
                maxThrust.Add(dir, sum);
            }
        }
        double getThrust(Base6Directions.Direction dir)
        {
            var ret = 0d;
            if (!dirThrust.ContainsKey(dir))
                return ret;
            var a = dirThrust[dir];
            for (int i = 0; i < a.Length; i++)
                ret += a[i].CurrentThrust;
            return ret;
        }
        void curThrust(ref SpriteData b, bool pct = false, bool a = false)
        {
            var d = -GCM.Controller?.MoveIndicator;
            if (d == Vector3.Zero || d == null)
            {
                b.Data = invalid;
                return;
            }
            var dir = Base6Directions.GetClosestDirection(d.Value);
            var cur = groups[dir].Data;
            if (pct)
                b.SetData(cur / maxThrust[dir], "#0.#%");
            else if (a)
                b.SetData(cur / GCM.Controller.CalculateShipMass().TotalMass, "#0.#");
            else
                b.SetData(groups[dir].Data);
        }

    }


    // TODO: THIS SYSTEM IS ASS
    //public class CoreWeaponUtilities : UtilityBase
    //{
    //    Dictionary<long, MyTuple<string, IMyTerminalBlock[]>> WeaponGroups = new Dictionary<long, MyTuple<string, IMyTerminalBlock[]>>();
    //    Dictionary<long, string> tagStorage = new Dictionary<long, string>();
    //    List<IMyTerminalBlock> wcWeapons = new List<IMyTerminalBlock>();
    //    WCPBAPI api = null;
    //    bool man = false;

    //    public CoreWeaponUtilities()
    //    {
    //        name = "Weapons";
    //    }

    //    #region UtilityBase
    //    public override void Reset(GraphicsManager m, MyGridProgram p)
    //    {
    //        base.Reset(m, p);
    //        WeaponGroups.Clear();
    //        man = true;
    //    }

    //    public override void Bind(ref Dictionary<string, Action<SpriteData>> commands)
    //    {
    //        commands.Add("!wpnrdy", (b) =>
    //        {
    //            if (WCPBAPI.Activate(Program.Me, ref api))
    //            {
    //                if (!WeaponGroups.ContainsKey(b.uID)) AddWeaponGroup(b);
    //                else UpdateWeaponReady(ref b);
    //            }
    //        });
    //        commands.Add("!tgt", (b) =>
    //        {
    //            if (WCPBAPI.Activate(Program.Me, ref api))
    //            {
    //                var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
    //                b.Data = focus.HasValue ? focus.Value.Name : "NO TARGET";
    //            }
    //        });

    //        commands.Add("!tgtdist", (b) =>
    //        {
    //            if (WCPBAPI.Activate(Program.Me, ref api))
    //            {
    //                var focus = api.GetAiFocus(Program.Me.CubeGrid.EntityId);
    //                b.Data = focus.HasValue ? (focus.Value.Position - Program.Me.CubeGrid.GetPosition()).Length().ToString("4:####") : "NO TARGET";
    //            }
    //        });

    //        commands.Add("!heats%", (b) =>
    //        {
    //            if (WCPBAPI.Activate(Program.Me, ref api))
    //            {
    //                // todo
    //            }
    //        });
    //    }

    //    public override void GetBlocks()
    //    {
    //        if (!WCPBAPI.Activate(Program.Me, ref api))
    //            return;
    //        wcWeapons.Clear();
    //        foreach (var t in tagStorage.Values)
    //            TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
    //            {
    //                if (b.IsSameConstructAs(Program.Me) && b.CustomName.Contains(t))
    //                    wcWeapons.Add(b);
    //                return true;
    //            });
    //    }

    //    public override void CheckUpdate() { }

    //    #endregion
    //    void AddWeaponGroup(SpriteData d)
    //    {
    //        var list = new List<IMyTerminalBlock>();
    //        string[] dat = d.Data.Split(cmd);
    //        if (!tagStorage.ContainsKey(d.uID))
    //            tagStorage.Add(d.uID, dat[0]);
    //        TerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
    //        {
    //            if (b.IsSameConstructAs(Program.Me) && b.CustomName.Contains(dat[0]))
    //                list.Add(b);
    //            return true;
    //        });
    //        if (list.Count > 0) WeaponGroups.Add(d.uID, new MyTuple<string, IMyTerminalBlock[]>(dat[1], list.ToArray()));
    //    }

    //    void UpdateWeaponReady(ref SpriteData d)
    //    {
    //        if (api == null) return;
    //        int count = 0;
    //        foreach (var wpn in WeaponGroups[d.uID].Item2)
    //            if (api.IsWeaponReadyToFire(wpn)) count++;
    //        d.Data = $"{WeaponGroups[d.uID].Item1} {count}/{WeaponGroups[d.uID].Item2.Length} RDY";
    //    }

    //}
}
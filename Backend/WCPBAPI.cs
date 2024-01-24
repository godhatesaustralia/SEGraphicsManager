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
using VRageMath;

namespace IngameScript
{
    public class WCPBAPI
    {
        private Action<ICollection<MyDefinitionId>> _getCoreWeapons;
        private Action<ICollection<MyDefinitionId>> _getCoreStaticLaunchers;
        private Action<ICollection<MyDefinitionId>> _getCoreTurrets;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
        private Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<Sandbox.ModAPI.Ingame.MyDetectedEntityInfo>> _getObstructions;
        private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, float> _getMaxWeaponRange;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int, bool> _canShootTarget;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int, Vector3D?> _getPredictedTargetPos;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float> _getHeatLevel;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float> _currentPowerConsumption;
        private Func<MyDefinitionId, float> _getMaxPower;
        private Func<long, bool> _hasGridAi;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool> _hasCoreWeapon;
        private Func<long, float> _getOptimalDps;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, string> _getActiveAmmo;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long> _getPlayerController;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Matrix> _getWeaponAzimuthMatrix;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Matrix> _getWeaponElevationMatrix;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, bool, bool, bool> _isTargetValid;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>> _getWeaponScope;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, MyTuple<bool, bool>> _isInRange;
        static public void Activate(IMyTerminalBlock pbBlock, ref WCPBAPI apiHandle)
        {
            if (apiHandle != null)
                return;

            var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null)
                return;

            apiHandle = new WCPBAPI();
            apiHandle.ApiAssign(dict);
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;

            AssignMethod(delegates, "GetCoreWeapons", ref _getCoreWeapons);
            AssignMethod(delegates, "GetCoreStaticLaunchers", ref _getCoreStaticLaunchers);
            AssignMethod(delegates, "GetCoreTurrets", ref _getCoreTurrets);
            AssignMethod(delegates, "GetBlockWeaponMap", ref _getBlockWeaponMap);
            AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
            AssignMethod(delegates, "GetObstructions", ref _getObstructions);
            AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
            AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
            AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
            AssignMethod(delegates, "GetMaxWeaponRange", ref _getMaxWeaponRange);
            AssignMethod(delegates, "CanShootTarget", ref _canShootTarget);
            AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
            AssignMethod(delegates, "GetHeatLevel", ref _getHeatLevel);
            AssignMethod(delegates, "GetCurrentPower", ref _currentPowerConsumption);
            AssignMethod(delegates, "GetMaxPower", ref _getMaxPower);
            AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
            AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
            AssignMethod(delegates, "GetActiveAmmo", ref _getActiveAmmo);
            AssignMethod(delegates, "GetPlayerController", ref _getPlayerController);
            AssignMethod(delegates, "GetWeaponAzimuthMatrix", ref _getWeaponAzimuthMatrix);
            AssignMethod(delegates, "GetWeaponElevationMatrix", ref _getWeaponElevationMatrix);
            AssignMethod(delegates, "IsTargetValid", ref _isTargetValid);
            AssignMethod(delegates, "GetWeaponScope", ref _getWeaponScope);
            AssignMethod(delegates, "IsInRange", ref _isInRange);
            return true;
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;
            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

        public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => _getCoreWeapons?.Invoke(collection);

        public void GetAllCoreStaticLaunchers(ICollection<MyDefinitionId> collection) =>
            _getCoreStaticLaunchers?.Invoke(collection);

        public void GetAllCoreTurrets(ICollection<MyDefinitionId> collection) => _getCoreTurrets?.Invoke(collection);

        public bool GetBlockWeaponMap(Sandbox.ModAPI.Ingame.IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
            _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;

        public void GetSortedThreats(Sandbox.ModAPI.Ingame.IMyTerminalBlock pBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
            _getSortedThreats?.Invoke(pBlock, collection);
        public void GetObstructions(Sandbox.ModAPI.Ingame.IMyTerminalBlock pBlock, ICollection<Sandbox.ModAPI.Ingame.MyDetectedEntityInfo> collection) =>
            _getObstructions?.Invoke(pBlock, collection);
        public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);

        public MyDetectedEntityInfo? GetWeaponTarget(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId = 0) =>
            _getWeaponTarget?.Invoke(weapon, weaponId);

        public bool IsWeaponReadyToFire(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
            bool shootReady = false) =>
            _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

        public float GetMaxWeaponRange(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
            _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;

        public bool CanShootTarget(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
            _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;

        public Vector3D? GetPredictedTargetPosition(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
            _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;

        public float GetHeatLevel(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
        public float GetCurrentPower(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;
        public float GetMaxPower(MyDefinitionId weaponDef) => _getMaxPower?.Invoke(weaponDef) ?? 0f;
        public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
        public bool HasCoreWeapon(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
        public float GetOptimalDps(long entity) => _getOptimalDps?.Invoke(entity) ?? 0f;

        public string GetActiveAmmo(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
            _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;

        public long GetPlayerController(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _getPlayerController?.Invoke(weapon) ?? -1;

        public Matrix GetWeaponAzimuthMatrix(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
            _getWeaponAzimuthMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        public Matrix GetWeaponElevationMatrix(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
            _getWeaponElevationMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        public bool IsTargetValid(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, long targetId, bool onlyThreats, bool checkRelations) =>
            _isTargetValid?.Invoke(weapon, targetId, onlyThreats, checkRelations) ?? false;

        public MyTuple<Vector3D, Vector3D> GetWeaponScope(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
            _getWeaponScope?.Invoke(weapon, weaponId) ?? new MyTuple<Vector3D, Vector3D>();
        // terminalBlock, Threat, Other, Something 
        public MyTuple<bool, bool> IsInRange(Sandbox.ModAPI.Ingame.IMyTerminalBlock block) =>
            _isInRange?.Invoke(block) ?? new MyTuple<bool, bool>();
    }
}

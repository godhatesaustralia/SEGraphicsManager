using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    public class WCPBAPI
    {
        public static bool isActive = false;
        private Func<IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;
        private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
        private Action<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>> _getObstructions;
        private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
        private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
        private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
        private Func<IMyTerminalBlock, long, int, bool> _canShootTarget;
        private Func<IMyTerminalBlock, long, int, Vector3D?> _getPredictedTargetPos;
        private Func<IMyTerminalBlock, float> _getHeatLevel;
        private Func<IMyTerminalBlock, float> _currentPowerConsumption;
        private Func<MyDefinitionId, float> _getMaxPower;
        //private Func<long, bool> _hasGridAi;
        //private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
        //private Func<long, float> _getOptimalDps;
        //private Func<IMyTerminalBlock, int, string> _getActiveAmmo;
        //private Func<Ingame.IMyTerminalBlock, long> _getPlayerController;
        //private Func<IMyTerminalBlock, int, Matrix> _getWeaponAzimuthMatrix;
        //private Func<IMyTerminalBlock, int, Matrix> _getWeaponElevationMatrix;
        //private Func<IMyTerminalBlock, long, bool, bool, bool> _isTargetValid;
        //private Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>> _getWeaponScope;
        //private Func<IMyTerminalBlock, MyTuple<bool, bool>> _isInRange;
        public static bool Activate(IMyTerminalBlock pbBlock, ref WCPBAPI api)
        {
            if (isActive) 
                return true;
            if (api != null)
                return false;

            var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null)
                return false;

            api = new WCPBAPI();
            api.ApiAssign(dict);
            isActive = true;
            return true;
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;

            AssignMethod(delegates, "GetBlockWeaponMap", ref _getBlockWeaponMap);
            AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
            AssignMethod(delegates, "GetObstructions", ref _getObstructions);
            AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
            AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
            AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
            AssignMethod(delegates, "CanShootTarget", ref _canShootTarget);
            AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
            AssignMethod(delegates, "GetHeatLevel", ref _getHeatLevel);
            AssignMethod(delegates, "GetCurrentPower", ref _currentPowerConsumption);
            AssignMethod(delegates, "GetMaxPower", ref _getMaxPower);
            //AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
            //AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
            //AssignMethod(delegates, "GetActiveAmmo", ref _getActiveAmmo);
            //AssignMethod(delegates, "GetPlayerController", ref _getPlayerController);
            //AssignMethod(delegates, "GetWeaponAzimuthMatrix", ref _getWeaponAzimuthMatrix);
            //AssignMethod(delegates, "GetWeaponElevationMatrix", ref _getWeaponElevationMatrix);
            //AssignMethod(delegates, "IsTargetValid", ref _isTargetValid);
            //AssignMethod(delegates, "GetWeaponScope", ref _getWeaponScope);
            //AssignMethod(delegates, "IsInRange", ref _isInRange);
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

        public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
            _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;

        public void GetSortedThreats(IMyTerminalBlock pBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
            _getSortedThreats?.Invoke(pBlock, collection);
        public void GetObstructions(IMyTerminalBlock pBlock, ICollection<MyDetectedEntityInfo> collection) =>
            _getObstructions?.Invoke(pBlock, collection);
        public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);

        public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
            _getWeaponTarget?.Invoke(weapon, weaponId);

        public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
            bool shootReady = false) =>
            _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

        public bool CanShootTarget(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
            _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;

        public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
            _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;

        public float GetHeatLevel(IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
        public float GetCurrentPower(IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;
        public float GetMaxPower(MyDefinitionId weaponDef) => _getMaxPower?.Invoke(weaponDef) ?? 0f;
        //public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
        //public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
        //public float GetOptimalDps(long entity) => _getOptimalDps?.Invoke(entity) ?? 0f;

        //public string GetActiveAmmo(IMyTerminalBlock weapon, int weaponId) =>
        //    _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;

        //public long GetPlayerController(IMyTerminalBlock weapon) => _getPlayerController?.Invoke(weapon) ?? -1;

        //public Matrix GetWeaponAzimuthMatrix(IMyTerminalBlock weapon, int weaponId) =>
        //    _getWeaponAzimuthMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        //public Matrix GetWeaponElevationMatrix(IMyTerminalBlock weapon, int weaponId) =>
        //    _getWeaponElevationMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        //public bool IsTargetValid(IMyTerminalBlock weapon, long targetId, bool onlyThreats, bool checkRelations) =>
        //    _isTargetValid?.Invoke(weapon, targetId, onlyThreats, checkRelations) ?? false;

        //public MyTuple<Vector3D, Vector3D> GetWeaponScope(IMyTerminalBlock weapon, int weaponId) =>
        //    _getWeaponScope?.Invoke(weapon, weaponId) ?? new MyTuple<Vector3D, Vector3D>();
        //// terminalBlock, Threat, Other, Something 
        //public MyTuple<bool, bool> IsInRange(IMyTerminalBlock block) =>
        //    _isInRange?.Invoke(block) ?? new MyTuple<bool, bool>();
    }
}

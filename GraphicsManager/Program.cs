using Sandbox.ModAPI.Ingame;
namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public GraphicsManager Manager;
        string tag = "GCM";
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Manager = new GraphicsManager(this, tag);

            Manager.useCustomDisplays = true;
            Manager.Keys = new IniKeys();

            
            InventoryUtilities inv = new InventoryUtilities(this, Manager, tag);
            Manager.InfoUtilities.Add(inv);
            Manager.InfoUtilities.Add(new FlightUtilities());
            Manager.InfoUtilities.Add(new GasUtilities(inv));
            Manager.InfoUtilities.Add(new PowerUtilities(inv));
            Manager.InfoUtilities.Add(new WeaponUtilities(Manager.Tag));
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Manager.Update(argument, updateSource);
        }
    }
}

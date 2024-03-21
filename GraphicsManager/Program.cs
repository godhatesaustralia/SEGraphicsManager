using Sandbox.ModAPI.Ingame;
namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public GraphicsManager Manager;
        string tag = "GCM";
        string format = "0000";
        public Program()
        {
            Manager = new GraphicsManager(this, tag);

            Manager.useCustomDisplays = true;
            Manager.Keys = new IniKeys();
            
            Manager.Inventory = new InventoryUtilities(this, tag);
            Manager.InfoUtilities.Add(new FlightUtilities(format));
            Manager.InfoUtilities.Add(new GasUtilities(ref Manager.Inventory));
            Manager.InfoUtilities.Add(new PowerUtilities(Manager.Inventory));
            Manager.InfoUtilities.Add(new WeaponUtilities(Manager.Tag));
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Manager.Update(argument, updateSource);
        }
    }
}

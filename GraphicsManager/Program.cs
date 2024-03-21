using Sandbox.ModAPI.Ingame;
namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public GraphicsManager Manager;
        string tag = "GCM";
        public Program()
        {
            Manager = new GraphicsManager(this, tag);

            Manager.useCustomDisplays = true;
            Manager.Keys = new IniKeys();
            
            Manager.Inventory = new InventoryUtilities(this, tag);
            Manager.Utilities.Add(new FlightUtilities(tag));
            Manager.Utilities.Add(new GasUtilities(ref Manager.Inventory));
            Manager.Utilities.Add(new PowerUtilities(ref Manager.Inventory));
            Manager.Utilities.Add(new WeaponUtilities(Manager.Tag));
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Manager.Update(argument, updateSource);
        }
    }
}

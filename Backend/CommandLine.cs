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
    public class CommandLine
    {
        public MyGridProgram thisprogram;
        public MyCommandLine commandLine;
        public IMyProgrammableBlock me;
        string
        Subsystem,
        DebugString,
        badResult = "[INVALID]";

        public CommandLine(MyGridProgram program)
        {
            thisprogram = program;
            me = thisprogram.Me;
            commandLine = new MyCommandLine();
        }

        //public string JoinArguments(INTStringBuilder builder, int start, int end)
        //{
        //    builder.Clear();
        //    int termination = Math.Max(ArgumentCount - 1, end);
        //    for (int i = start; i <= termination; ++i)
        //    {
        //        builder.Append(CommandLine.Items[i]);
        //        if (i != termination)
        //            builder.Append(' ');
        //    }
        //    return builder.ToString();
        //}
        public bool TryParse(string line)
        {
            DebugString = line;
            bool success = commandLine.TryParse(line);
            if (success)
            {
                Subsystem = commandLine.Argument(0);
                int index = line.IndexOf(Subsystem);
                line = line.Remove(index, Subsystem.Length);
                success = commandLine.TryParse(line);
            }
            return success;
        }
        public string Argument(int index)
        {
            return commandLine.Argument(index);
        }
        public int ArgumentCount => commandLine.ArgumentCount;
        public string CommandDataInput(double data)
        {
            return BitConverter.DoubleToInt64Bits(data).ToString("x");
        }
        public bool CommandDataOutput(string input, out double data)
        {
            long dataLong = Convert.ToInt64(input, 16);
            bool success = $"{dataLong:x}" == input ? true : false;
            data = BitConverter.Int64BitsToDouble(dataLong);
            return success;
        }

    }
}
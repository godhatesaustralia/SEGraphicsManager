using System;

namespace IngameScript
{
    [Flags]
    public enum Priority
    {
        None,
        Normal,
        Fast,
        High,
        Once
    }

}

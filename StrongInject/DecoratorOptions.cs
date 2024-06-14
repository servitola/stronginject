using System;

namespace StrongInject
{
    [Flags]
    public enum DecoratorOptions : long
    {
        Default = 0,

        Dispose = 1L << 1,
    }
}
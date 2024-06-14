using System;

namespace StrongInject.Generator
{
    [Flags]
    public enum DecoratorOptions : long
    {
        Default = 0,

        Dispose = 1L << 1,
    }
}
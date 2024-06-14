using System;

namespace StrongInject.Modules
{
    public static class LazyModule
    {
        [Factory]
        public static Lazy<T> CreateLazy<T>(Func<T> func)
        {
            return new Lazy<T>(func);
        }
    }
}
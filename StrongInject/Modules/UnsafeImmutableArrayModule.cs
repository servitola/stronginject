using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace StrongInject.Modules
{
    public static class UnsafeImmutableArrayModule
    {
        [Factory(Scope.InstancePerDependency)]
        public static ImmutableArray<T> UnsafeCreateImmutableArray<T>(T[] arr)
        {
            return Unsafe.As<T[], ImmutableArray<T>>(ref arr);
        }
    }
}
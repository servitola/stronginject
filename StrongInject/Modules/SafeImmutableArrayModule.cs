using System.Collections.Immutable;

namespace StrongInject.Modules
{
    public static class SafeImmutableArrayModule
    {
        [Factory(Scope.InstancePerDependency)]
        public static ImmutableArray<T> CreateImmutableArray<T>(T[] arr)
        {
            return arr.ToImmutableArray();
        }
    }
}
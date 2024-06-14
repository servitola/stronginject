using System.Collections.Generic;

namespace StrongInject.Modules
{
    public static class CollectionsModule
    {
        [Factory(Scope.InstancePerDependency)]
        public static IEnumerable<T> CreateEnumerable<T>(T[] arr)
        {
            return arr;
        }

        [Factory(Scope.InstancePerDependency)]
        public static IReadOnlyList<T> CreateReadOnlyList<T>(T[] arr)
        {
            return arr;
        }

        [Factory(Scope.InstancePerDependency)]
        public static IReadOnlyCollection<T> CreateReadOnlyCollection<T>(T[] arr)
        {
            return arr;
        }
    }
}
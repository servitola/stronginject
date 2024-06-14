using System;

namespace StrongInject
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class FactoryAttribute : Attribute
    {
        public FactoryAttribute(Scope scope = Scope.InstancePerResolution)
        {
            Scope = scope;
            AsTypes = Array.Empty<Type>();
        }

        public FactoryAttribute(Scope scope, params Type[] asTypes)
        {
            Scope = scope;
            AsTypes = asTypes;
        }

        public FactoryAttribute(params Type[] asTypes)
        {
            AsTypes = asTypes;
        }

        public Scope Scope { get; }
        public Type[] AsTypes { get; }
    }
}
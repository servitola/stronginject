using System;

namespace StrongInject
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RegisterAttribute : Attribute
    {
        public RegisterAttribute(Type type, params Type[] registerAs)
            : this(type, Scope.InstancePerResolution, registerAs)
        {
        }

        public RegisterAttribute(Type type, Scope scope, params Type[] registerAs)
        {
            Type = type;
            RegisterAs = registerAs;
            Scope = scope;
        }

        public Type Type { get; }
        public Type[] RegisterAs { get; }
        public Scope Scope { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RegisterAttribute<T> : Attribute
    {
        public RegisterAttribute(Scope scope = Scope.InstancePerResolution)
        {
            Scope = scope;
        }

        public Scope Scope { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RegisterAttribute<TImpl, TService> : Attribute
        where TImpl : TService
    {
        public RegisterAttribute(Scope scope = Scope.InstancePerResolution)
        {
            Scope = scope;
        }

        public Scope Scope { get; }
    }
}
﻿using System;

namespace StrongInject
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RegisterDecoratorAttribute : Attribute
    {
        public RegisterDecoratorAttribute(
            Type type,
            Type decoratedType,
            DecoratorOptions decoratorOptions = DecoratorOptions.Default)
        {
            Type = type;
            DecoratedType = decoratedType;
            DecoratorOptions = decoratorOptions;
        }

        public Type Type { get; }
        public Type DecoratedType { get; }
        public DecoratorOptions DecoratorOptions { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RegisterDecoratorAttribute<TDecorator, TDecorated> : Attribute
        where TDecorator : TDecorated
    {
        public RegisterDecoratorAttribute(DecoratorOptions decoratorOptions = DecoratorOptions.Default)
        {
            DecoratorOptions = decoratorOptions;
        }

        public DecoratorOptions DecoratorOptions { get; }
    }
}
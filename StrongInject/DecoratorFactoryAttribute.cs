﻿using System;

namespace StrongInject
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class DecoratorFactoryAttribute : Attribute
    {
        public DecoratorFactoryAttribute(DecoratorOptions decoratorOptions = DecoratorOptions.Default)
        {
            DecoratorOptions = decoratorOptions;
        }

        public DecoratorOptions DecoratorOptions { get; }
    }
}
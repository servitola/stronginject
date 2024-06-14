using System;

namespace StrongInject
{
    public sealed class StrongInjectException : Exception
    {
        public StrongInjectException(string message)
            : base(message)
        {
        }
    }
}
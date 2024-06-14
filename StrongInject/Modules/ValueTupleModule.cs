namespace StrongInject.Modules
{
    public static class ValueTupleModule
    {
        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2) CreateValueTuple<T1, T2>(T1 a, T2 b)
        {
            return (a, b);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3) CreateValueTuple<T1, T2, T3>(T1 a, T2 b, T3 c)
        {
            return (a, b, c);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3, T4) CreateValueTuple<T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d)
        {
            return (a, b, c, d);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3, T4, T5) CreateValueTuple<T1, T2, T3, T4, T5>(T1 a, T2 b, T3 c, T4 d, T5 e)
        {
            return (a, b, c, d, e);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3, T4, T5, T6) CreateValueTuple<T1, T2, T3, T4, T5, T6>(T1 a, T2 b, T3 c, T4 d, T5 e, T6 f)
        {
            return (a, b, c, d, e, f);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3, T4, T5, T6, T7) CreateValueTuple<T1, T2, T3, T4, T5, T6, T7>(
            T1 a,
            T2 b,
            T3 c,
            T4 d,
            T5 e,
            T6 f,
            T7 g)
        {
            return (a, b, c, d, e, f, g);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3, T4, T5, T6, T7, T8) CreateValueTuple<T1, T2, T3, T4, T5, T6, T7, T8>(
            T1 a,
            T2 b,
            T3 c,
            T4 d,
            T5 e,
            T6 f,
            T7 g,
            T8 h)
        {
            return (a, b, c, d, e, f, g, h);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3, T4, T5, T6, T7, T8, T9) CreateValueTuple<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            T1 a,
            T2 b,
            T3 c,
            T4 d,
            T5 e,
            T6 f,
            T7 g,
            T8 h,
            T9 i)
        {
            return (a, b, c, d, e, f, g, h, i);
        }

        [Factory(Scope.InstancePerDependency)]
        public static (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) CreateValueTuple<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
            T1 a,
            T2 b,
            T3 c,
            T4 d,
            T5 e,
            T6 f,
            T7 g,
            T8 h,
            T9 i,
            T10 j)
        {
            return (a, b, c, d, e, f, g, h, i, j);
        }
    }
}
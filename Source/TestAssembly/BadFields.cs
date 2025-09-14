using BootstrapApi;

namespace TestAssembly;

public static class BadFields {
    [AddField] public static extern ref T FailGenericMethod<T>(TargetClass target);
    [AddField] public static extern ref T FailNestedGeneric<T>(TargetGeneric<List<T>> target);
    [AddField] public static extern ref int FailConcreteGeneric(TargetGeneric<int> target);
    [AddField] public static extern ref int FailGenericCount<T, T2>(TargetGeneric<T> target);

    [AddField] private static extern ref int FailGenericsDontMatch<T>(TargetGeneric3<T, T, T> target);

    [AddField] private static extern ref int FailInterface(ITarget target);

    [AddField] [PostInitField] private static extern ref BasePostInitComp FailInjectionByRef(BasePostInit target);

    [AddField] [PostInitField] private static extern BasePostInitComp FailUnknownInjection(TargetClass target);
}
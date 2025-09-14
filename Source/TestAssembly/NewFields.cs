using BootstrapApi;

namespace TestAssembly;

public static class NewFields {
    [AddField] public static extern ref int MyInt(this TargetClass target);

    [AddField] public static extern ref int MyIntStruct(this ref TargetStruct target);

    [AddField] public static extern ref List<T> MyList<T>(this TargetGeneric<T> target);

    [AddField] public static extern ref (T, TW, TU) MyTriple<T, TU, TW>(this TargetGeneric3<T, TU, TW> b);

    [AddField] public static extern ref (T, T) MyPair<T, TU, TW>(this TargetGeneric3<T, TU, TW> b);

    [AddField] public static extern ref (int, TW) MyPairPartial<T, TU, TW>(this TargetGeneric3<T, TU, TW> b);
}
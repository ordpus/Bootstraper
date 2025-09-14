using BootstrapApi;

namespace TestAssembly;

public static class DefaultValues {
    // bool defaults
    [AddField] [DefaultValueDirect(false)] public static extern ref bool MyBoolDefaultFalse(this TargetClass target);

    [AddField] [DefaultValueDirect(true)] public static extern ref bool MyBoolDefaultTrue(this TargetClass target);

    [AddField]
    [DefaultValueDirect(byte.MinValue)]
    public static extern ref byte MyByteDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(byte.MaxValue)]
    public static extern ref byte MyByteDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(sbyte.MinValue)]
    public static extern ref sbyte MySByteDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(sbyte.MaxValue)]
    public static extern ref sbyte MySByteDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(char.MinValue)]
    public static extern ref char MyCharDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(char.MaxValue)]
    public static extern ref char MyCharDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(short.MinValue)]
    public static extern ref short MyShortDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(short.MaxValue)]
    public static extern ref short MyShortDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(ushort.MinValue)]
    public static extern ref ushort MyUShortDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(ushort.MaxValue)]
    public static extern ref ushort MyUShortDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(int.MinValue)]
    public static extern ref int MyIntDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(int.MaxValue)]
    public static extern ref int MyIntDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(uint.MinValue)]
    public static extern ref uint MyUIntDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(uint.MaxValue)]
    public static extern ref uint MyUIntDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(long.MinValue)]
    public static extern ref long MyLongDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(long.MaxValue)]
    public static extern ref long MyLongDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(ulong.MinValue)]
    public static extern ref ulong MyULongDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(ulong.MaxValue)]
    public static extern ref ulong MyULongDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(float.MinValue)]
    public static extern ref float MyFloatDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(float.MaxValue)]
    public static extern ref float MyFloatDefaultMax(this TargetClass target);

    [AddField]
    [DefaultValueDirect(double.MinValue)]
    public static extern ref double MyDoubleDefaultMin(this TargetClass target);

    [AddField]
    [DefaultValueDirect(double.MaxValue)]
    public static extern ref double MyDoubleDefaultMax(this TargetClass target);

    [AddField] [DefaultValueDirect("a")] public static extern ref string MyStringDefault(this TargetClass target);

    // Value initializers
    [AddField]
    [DefaultValueInjector(nameof(IntParameterlessInitializer))]
    public static extern ref int MyIntParameterless(this TargetClass target);

    [AddField]
    [DefaultValueInjector(nameof(IntThisInitializer))]
    public static extern ref int MyIntFromThis(this TargetClass target);

    [AddField]
    [DefaultValueInjector(nameof(ObjectThisInitializer))]
    public static extern ref SecondTargetClass MyObjectFromThis(this TargetClass target);

    [AddField]
    [DefaultValueInjector(nameof(CounterInitializer))]
    public static extern ref int MyIntCounter(this DerivedCtorsClass target);

    public static int IntParameterlessInitializer() => 1;

    public static int IntThisInitializer(TargetClass? obj) => obj != null ? 1 : -1;

    public static SecondTargetClass ObjectThisInitializer(TargetClass obj) => new(obj);

    public static int CounterInitializer(DerivedCtorsClass ctors) => ++ctors.Counter;
}
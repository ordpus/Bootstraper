using BootstrapApi;


namespace TestAssembly;

public static class Injections
{
    [AddField]
    [PostInitField]
    public static extern OtherPostInitComp SomeComp(this BasePostInit target);

    [AddField]
    [PostInitField]
    public static extern OtherPostInitComp SomeOtherComp(this OtherPostInit target);

}

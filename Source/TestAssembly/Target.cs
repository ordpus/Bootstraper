using JetBrains.Annotations;

namespace TestAssembly;

public class TargetClass;

public struct TargetStruct;

public class SecondTargetClass(TargetClass inner) {
    public readonly TargetClass Inner = inner;
}

public class TargetGeneric<T>;

public class TargetGeneric3<T1, T2, T3>;

public interface ITarget;

public class BasePostInit {
    private BasePostInitComp? comp;

    public void Initialize() {
        comp = new OtherPostInitComp();
    }

    public T? Get<T>() where T : BasePostInitComp {
        return (T?)comp;
    }
}

public class OtherPostInit : BasePostInit;

public class BasePostInitComp;

public class OtherPostInitComp : BasePostInitComp;

public class CtorsClass {
    public int Counter;

    protected CtorsClass() { }

    protected CtorsClass(int a) { }
}

public class DerivedCtorsClass : CtorsClass {
    public DerivedCtorsClass() { }

    public DerivedCtorsClass(int a) { }

    public DerivedCtorsClass(int a, int b) : base(1) {
        for (; b < 10; b++)
            if (a == 0 && b == 9)
                return;

        switch (a) {
            case 1:
                Console.WriteLine("1");
                return;
            case 2:
                Console.WriteLine("2");
                return;
            default:
                throw new Exception();
        }
    }

    public DerivedCtorsClass(string c) : this(1) { }
}
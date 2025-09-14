using TestAssembly;

namespace TestCases;

[TestFixture]
public class Tests {
    [Test]
    public void TestDefaultValue() {
        var target = new TargetClass();
        var target2 = new DerivedCtorsClass();
        var target3 = new DerivedCtorsClass();

        Assert.Multiple(() => {
            Assert.That(target.MyBoolDefaultFalse(), Is.EqualTo(false));
            Assert.That(target.MyBoolDefaultTrue(), Is.EqualTo(true));
            Assert.That(target.MyByteDefaultMin(), Is.EqualTo(byte.MinValue));
            Assert.That(target.MyByteDefaultMax(), Is.EqualTo(byte.MaxValue));
            Assert.That(target.MySByteDefaultMin(), Is.EqualTo(sbyte.MinValue));
            Assert.That(target.MySByteDefaultMax(), Is.EqualTo(sbyte.MaxValue));
            Assert.That(target.MyCharDefaultMin(), Is.EqualTo(char.MinValue));
            Assert.That(target.MyCharDefaultMax(), Is.EqualTo(char.MaxValue));
            Assert.That(target.MyShortDefaultMin(), Is.EqualTo(short.MinValue));
            Assert.That(target.MyShortDefaultMax(), Is.EqualTo(short.MaxValue));
            Assert.That(target.MyUShortDefaultMin(), Is.EqualTo(ushort.MinValue));
            Assert.That(target.MyUShortDefaultMax(), Is.EqualTo(ushort.MaxValue));
            Assert.That(target.MyIntDefaultMin(), Is.EqualTo(int.MinValue));
            Assert.That(target.MyIntDefaultMax(), Is.EqualTo(int.MaxValue));
            Assert.That(target.MyUIntDefaultMin(), Is.EqualTo(uint.MinValue));
            Assert.That(target.MyUIntDefaultMax(), Is.EqualTo(uint.MaxValue));
            Assert.That(target.MyLongDefaultMin(), Is.EqualTo(long.MinValue));
            Assert.That(target.MyLongDefaultMax(), Is.EqualTo(long.MaxValue));
            Assert.That(target.MyULongDefaultMin(), Is.EqualTo(ulong.MinValue));
            Assert.That(target.MyULongDefaultMax(), Is.EqualTo(ulong.MaxValue));
            Assert.That(target.MyFloatDefaultMin(), Is.EqualTo(float.MinValue));
            Assert.That(target.MyFloatDefaultMax(), Is.EqualTo(float.MaxValue));
            Assert.That(target.MyDoubleDefaultMin(), Is.EqualTo(double.MinValue));
            Assert.That(target.MyDoubleDefaultMax(), Is.EqualTo(double.MaxValue));
            Assert.That(target.MyStringDefault(), Is.EqualTo("a"));
            Assert.That(target.MyIntParameterless(), Is.EqualTo(1));
            Assert.That(target.MyIntFromThis(), Is.EqualTo(1));
            Assert.That(target.MyObjectFromThis().Inner, Is.EqualTo(target));
            Assert.That(target2.MyIntCounter(), Is.EqualTo(1));
            Assert.That(target3.MyIntCounter(), Is.EqualTo(1));
        });
        Assert.Pass("Passed");
    }

    [Test]
    public void TestInjections() {
        var target = new BasePostInit();
        var otherTarget = new OtherPostInit();
        target.Initialize();
        otherTarget.Initialize();
        Assert.Multiple(() => {
            Assert.That(target.SomeComp(), Is.Not.Null);
            Assert.That(otherTarget.SomeOtherComp(), Is.Not.Null);
        });
        Assert.Pass("Passed");
    }

    [Test]
    public void TestNewFields() {
        Assert.Multiple(() => {
            var target = new TargetClass();
            var targetStruct = new TargetStruct();
            var targetGenericValue = new TargetGeneric<int>();
            var targetGenericReference = new TargetGeneric<string>();
            var targetGeneric3 = new TargetGeneric3<int, string, string>();
            Assert.That(target.MyInt(), Is.EqualTo(0));
            target.MyInt() = 2;
            Assert.That(target.MyInt(), Is.EqualTo(2));
            Assert.That(targetStruct.MyIntStruct(), Is.EqualTo(0));
            targetStruct.MyIntStruct() = 2;
            Assert.That(targetStruct.MyIntStruct(), Is.EqualTo(2));
            Assert.That(targetGenericValue.MyList(), Is.Null);
            targetGenericValue.MyList() = [];
            targetGenericValue.MyList().Add(0);
            Assert.That(targetGenericValue.MyList(), Is.EquivalentTo(new List<int> { 0 }));
            Assert.That(targetGenericReference.MyList(), Is.Null);
            targetGenericReference.MyList() = [];
            targetGenericReference.MyList().Add("a");
            Assert.That(targetGenericReference.MyList(), Is.EquivalentTo(new List<string> { "a" }));
            Assert.That(targetGeneric3.MyTriple().Item1, Is.EqualTo(0));
            Assert.That(targetGeneric3.MyTriple().Item2, Is.Null);
            Assert.That(targetGeneric3.MyTriple().Item3, Is.Null);
            targetGeneric3.MyTriple() = (1, "a", "b");
            Assert.That(targetGeneric3.MyTriple().Item1, Is.EqualTo(1));
            Assert.That(targetGeneric3.MyTriple().Item2, Is.EqualTo("a"));
            Assert.That(targetGeneric3.MyTriple().Item3, Is.EqualTo("b"));
            Assert.That(targetGeneric3.MyPair().Item1, Is.EqualTo(0));
            Assert.That(targetGeneric3.MyPair().Item2, Is.EqualTo(0));
            targetGeneric3.MyPair() = (1, 2);
            Assert.That(targetGeneric3.MyPair().Item1, Is.EqualTo(1));
            Assert.That(targetGeneric3.MyPair().Item2, Is.EqualTo(2));
            Assert.That(targetGeneric3.MyPairPartial().Item1, Is.EqualTo(0));
            Assert.That(targetGeneric3.MyPairPartial().Item2, Is.Null);
            targetGeneric3.MyPairPartial() = (1, "a");
            Assert.That(targetGeneric3.MyPairPartial().Item1, Is.EqualTo(1));
            Assert.That(targetGeneric3.MyPairPartial().Item2, Is.EqualTo("a"));
            
        });
        Assert.Pass("Passed");
    }
}
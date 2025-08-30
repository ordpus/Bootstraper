using Entrypoint;
using Mono.Cecil;

// ReSharper disable once CheckNamespace
namespace Doorstop;

public static class Entrypoint {

    public static void Start() {
        BootstrapEntrypoint.Start();
    }

    public static byte[] GetAssemblyBytes() {
        var assembly = AssemblyDefinition.ReadAssembly("RimWorldWin64_Data/Managed/Assembly-CSharp.dll");
        var module = assembly.MainModule;
        module.GetType("RimWorld.AbilityComp").Fields
            .Add(new FieldDefinition("myField", FieldAttributes.Public, module.TypeSystem.Boolean));
        using var stream = new MemoryStream();
        assembly.Write(stream);
        return stream.ToArray();
    }
}
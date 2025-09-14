using Bootstrap;
using Bootstrap.Patcher;

using BootstrapApi;

using Mono.Cecil;

try {
    var fileBytes = File.ReadAllBytes("TestAssembly.dll");
    using var stream = new MemoryStream(fileBytes);
    var assembly = AssemblyDefinition.ReadAssembly(
        stream,
        new ReaderParameters {
            AssemblyResolver = AssemblySet.s_assemblyResolver, MetadataResolver = AssemblySet.s_metadataResolver
        });
    AssemblySet.Reset();
    AssemblySet.SAssemblyDefinitions["TestAssembly"] = assembly;
    AssemblySet.SModuleDefinitions[assembly.MainModule.Name] = assembly.MainModule;
    BootstrapPluginManager.Register(
        new PostInitInfo(
            "TestAssembly.BasePostInit",
            "TestAssembly.BasePostInitComp",
            "Initialize",
            new DefaultPostInitProvider("TestAssembly.BasePostInit:Get")));
    var assemblies = Patcher.GetAllPatches();
    assemblies.ForEach(x => File.WriteAllBytes($"{x.Name.Name}.dll", x.GetRawBytes()));
} catch (Exception e) {
    Console.WriteLine(e.ToString());
    throw;
}
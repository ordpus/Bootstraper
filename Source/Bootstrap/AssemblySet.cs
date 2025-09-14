using BootstrapApi;

using Mono.Cecil;

using MonoMod.Utils;

using Serilog;

namespace Bootstrap;

public static class AssemblySet {
    private static Dictionary<string, AssemblyDefinition> s_assemblyDefinitions = [];
    private static Dictionary<string, ModuleDefinition> s_moduleDefinitions = [];
    public static IReadOnlyDictionary<string, AssemblyDefinition> Assemblies => s_assemblyDefinitions;
    public static IReadOnlyList<AssemblyDefinition> AssemblyList => s_assemblyDefinitions.Values.ToList();
    public static IReadOnlyDictionary<string, ModuleDefinition> Modules => s_moduleDefinitions;
    public static IReadOnlyList<ModuleDefinition> ModuleList => s_moduleDefinitions.Values.ToList();

    public static void Reset() {
        Clear();
        var assemblies = AppDomain.CurrentDomain
                                         .GetAssemblies()
                                         .Where(x => !x.IsDynamic)
                                         .ToDictionary(
                                             x => x.GetFileData().ToSHA256Hex(),
                                             x => x)
                                         .Values
                                         .Select(x => AssemblyDefinition.ReadAssembly(x.Location))
                                         .ToList();
        s_assemblyDefinitions = assemblies.ToDictionary(x => x.Name.Name, x => x);
        s_moduleDefinitions = assemblies.SelectMany(x => x.Modules).ToDictionary(x => x.Name, x => x);
    }

    public static void Clear() {
        s_assemblyDefinitions.Clear();
        s_moduleDefinitions.Clear();
    }

    public static FieldDefinition? FindFieldDefinition(string colonName) {
        var spilt = colonName.Split(':');
        var typeName = spilt[0];
        var fieldName = spilt[1];
        return ModuleList.Select(x => x.GetType(typeName))
                          .Where(x => x != null)
                          .Select(x => x.FindField(fieldName))
                          .FirstOrDefault();
    }

    public static MethodDefinition? FindMethodDefinition(string colonName) {
        var spilt = colonName.Split(':');
        var typeName = spilt[0];
        var methodName = spilt[1];
        Log.Information("type: {type}, method: {method}", typeName, methodName);
        Log.Information("Modules: {}", ModuleList.Select(x => x.Name));
        return ModuleList.Select(x => x.GetType(typeName))
                          .Where(x => x != null)
                          .Select(x => x.FindMethod(methodName))
                          .FirstOrDefault();
    }
}
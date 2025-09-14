using BootstrapApi;

using Mono.Cecil;

using MonoMod.Utils;

namespace Bootstrap;

public static class AssemblySet {
    private static IAssemblyResolver s_assemblyResolver;
    private static IMetadataResolver s_metadataResolver;
    private static readonly Dictionary<string, AssemblyDefinition> SAssemblyDefinitions = [];
    private static readonly Dictionary<string, ModuleDefinition> SModuleDefinitions = [];
    public static IReadOnlyDictionary<string, AssemblyDefinition> Assemblies => SAssemblyDefinitions;
    public static IReadOnlyList<AssemblyDefinition> AssemblyList => SAssemblyDefinitions.Values.ToList();
    public static IReadOnlyDictionary<string, ModuleDefinition> Modules => SModuleDefinitions;
    internal static IReadOnlyList<ModuleDefinition> ModuleList => SModuleDefinitions.Values.ToList();

    static AssemblySet() {
        s_assemblyResolver = new DefaultAssemblyResolver();
        s_metadataResolver = new MetadataResolver(s_assemblyResolver);
    }

    public static void Reset() {
        Clear();
        var assemblies = AppDomain.CurrentDomain
                                  .GetAssemblies()
                                  .Where(x => !x.IsDynamic)
                                  .ToDictionary(
                                      x => x.GetFileData().ToSHA256Hex(),
                                      x => x)
                                  .Values
                                  .Select(x => AssemblyDefinition.ReadAssembly(
                                      x.Location,
                                      new ReaderParameters {
                                          AssemblyResolver = s_assemblyResolver, MetadataResolver = s_metadataResolver
                                      }))
                                  .ToList();
        SAssemblyDefinitions.AddRange(assemblies.ToDictionary(x => x.Name.Name, x => x));
        SModuleDefinitions.AddRange(assemblies.SelectMany(x => x.Modules).ToDictionary(x => x.Name, x => x));
        BootstrapPluginManager.Resolve();
    }

    public static void Clear() {
        SAssemblyDefinitions.Clear();
        SModuleDefinitions.Clear();
        s_assemblyResolver = new DefaultAssemblyResolver();
        s_metadataResolver = new MetadataResolver(s_assemblyResolver);
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
        return ModuleList.Select(x => x.GetType(typeName))
                         .Where(x => x != null)
                         .Select(x => x.FindMethod(methodName))
                         .FirstOrDefault();
    }

    public static TypeDefinition? FindTypeDefinition(string typeFullName) {
        return ModuleList
               .Select(x => x.GetType(typeFullName))
               .FirstOrDefault(x => x != null);
    }

    public static bool SameReference(this MemberReference self, MemberReference? other) {
        if (ReferenceEquals(self, other)) return true;
        if (other == null) return false;
        if (!self.IsDefinition && !other.IsDefinition) {
            return self.Module == other.Module && self.MetadataToken == other.MetadataToken;
        }

        var selfR = self.Resolve();
        var otherR = other.Resolve();
        if (selfR == null || otherR == null) return false;
        return selfR.Module() == otherR.Module() && selfR.MetadataToken == otherR.MetadataToken;
    }

    public static ModuleDefinition Module(this IMemberDefinition self) {
        return self switch {
            TypeDefinition x => x.Module,
            FieldDefinition x => x.Module,
            MethodDefinition x => x.Module,
            PropertyDefinition x => x.Module,
            EventDefinition x => x.Module,
            _ => throw new NotSupportedException($"Unknown member type: {self.GetType()}")
        };
    }


    public static bool IsAssignableFrom(this TypeDefinition target, TypeReference sub) =>
        target.SameReference(sub)
        || sub.Resolve().IsSubclassOf(target)
        || (target.IsInterface && sub.Resolve().ImplementInterface(target));


    public static bool IsSubclassOf(this TypeDefinition sub, TypeReference target) {
        if (target.SameReference(sub)) return false;
        for (var baseType = sub;
             baseType != null;
             baseType = baseType.BaseType?.Resolve())
            if (baseType.SameReference(target))
                return true;
        return false;
    }

    public static bool ImplementInterface(this TypeDefinition impl, TypeReference face) {
        var result = new bool[1];
        BootstrapUtility.Bfs<TypeReference>(
            impl.Interfaces.Select(x => x.InterfaceType),
            x => x.Resolve().Interfaces.Select(y => y.InterfaceType),
            x => result[0] = x.SameReference(face),
            ReferenceEqualityComparator.Instance);
        return result[0];
    }

    public static bool Overrides(this MethodDefinition self, MethodReference target) {
        var result = new bool[1];
        BootstrapUtility.Bfs<MethodReference>(
            self.Overrides,
            x => x.Resolve().Overrides,
            x => result[0] = x.SameReference(target),
            ReferenceEqualityComparator.Instance);
        return result[0];
    }

    private static TypeDefinition Resolve(this TypeDefinition type, TypeReference other) =>
        type.Module.MetadataResolver.Resolve(other);

    public class ReferenceEqualityComparator : IEqualityComparer<MemberReference> {
        public static readonly ReferenceEqualityComparator Instance = new();

        public bool Equals(MemberReference x, MemberReference y) {
            return x.SameReference(y);
        }

        public int GetHashCode(MemberReference? obj) {
            if (obj == null) return 0;
            var objR = obj.Resolve();
            return HashCode.Combine(objR.Module(), objR.MetadataToken);
        }
    }
}
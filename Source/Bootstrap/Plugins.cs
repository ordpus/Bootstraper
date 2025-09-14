using BootstrapApi;

using Mono.Cecil;
using Mono.Cecil.Cil;

using MonoMod.Utils;

using Serilog;

namespace Bootstrap;

public readonly struct PatchInfo
    (MethodDefinition method, FieldDefinition field) {
    public MethodDefinition Method => method;
    public FieldDefinition Field => field;
}

public readonly struct ValidateInfo(MethodDefinition method, FieldDefinition field) {
    public MethodDefinition Method => method;
    public FieldDefinition Field => field;
}

public readonly struct PostInitInfo
    (string mainType, string targetType, string initializer, IPostInitProvider ilProvider) {
    public PostInitInfo(Type mainType, Type targetType, string initializer, IPostInitProvider ilProvider) : this(
        mainType.FullName!,
        targetType.FullName!,
        initializer,
        ilProvider) { }

    public string MainType { get; } = mainType;
    public string TargetType { get; } = targetType;
    public string Initializer { get; } = initializer;
    public IPostInitProvider ILProvider { get; } = ilProvider;
}

public readonly struct ResolvedPostInitInfo
    (TypeDefinition mainType, TypeDefinition targetType, MethodDefinition initializer, IPostInitProvider ilProvider) {
    public TypeDefinition MainType => mainType;
    public TypeDefinition TargetType => targetType;
    public MethodDefinition Initializer => initializer;
    public IPostInitProvider ILProvider => ilProvider;
}

public interface IPostInitProvider {
    bool Validate(ValidateInfo info, ResolvedPostInitInfo postInitInfo);
    IEnumerable<Instruction> Provide(ResolvedPostInitInfo info, PatchInfo patch);
}

public interface IAddFieldPlugin {
    string AttributeName();

    bool Validate(ValidateInfo info);

    void Patch(PatchInfo info);
}

public static class BootstrapPluginManager {
    private static readonly List<IAddFieldPlugin> AddFieldPluginsField = [];
    private static readonly List<PostInitInfo> PostInitInfosField = [];
    private static readonly List<ResolvedPostInitInfo> ResolvedPostInitInfosField = [];

    public static void Register(IAddFieldPlugin plugin) => AddFieldPluginsField.Add(plugin);

    public static void Register(PostInitInfo info) {
        PostInitInfosField.Add(info);
        ResolvedPostInitInfosField.Clear();
    }

    public static IReadOnlyList<IAddFieldPlugin> AddFieldPlugins => AddFieldPluginsField;

    public static IReadOnlyList<PostInitInfo> PostInitInfos => PostInitInfosField;

    public static IReadOnlyList<ResolvedPostInitInfo> ResolvedPostInitInfos {
        get {
            if (ResolvedPostInitInfosField.Count == 0) Resolve();
            return ResolvedPostInitInfosField;
        }
    }

    public static bool FindResolvePostInitInfo(TypeReference baseType, TypeReference targetType, out ResolvedPostInitInfo result) {
        result = default;
        if (!ResolvedPostInitInfos.Any(x => x.MainType.IsAssignableFrom(baseType)
                                            && x.TargetType.IsAssignableFrom(targetType)))
            return false;
        result = ResolvedPostInitInfos.First(x => x.MainType.IsAssignableFrom(baseType)
                                                  && x.TargetType.IsAssignableFrom(targetType));
        return true;
    }

    public static void Resolve() {
        ResolvedPostInitInfosField.Clear();
        ResolvedPostInitInfosField.AddRange(
            PostInitInfos.Select(GetResolved)
                         .Where(x => x is { mainType: not null, targetType: not null, initializer: not null })
                         .Cast<(TypeDefinition mainType, TypeDefinition targetType, MethodDefinition initializer,
                             IPostInitProvider ilProvider)>()
                         .Select(x => new ResolvedPostInitInfo(
                             x.mainType,
                             x.targetType,
                             x.initializer,
                             x.ilProvider)));
        return;

        (TypeDefinition? mainType, TypeDefinition? targetType, MethodDefinition? initializer, IPostInitProvider
            ilProvider) GetResolved(PostInitInfo info) {
            var mainType = AssemblySet.FindTypeDefinition(info.MainType);
            var targetType = AssemblySet.FindTypeDefinition(info.TargetType);
            var initializer = info.Initializer.Contains(":")
                ? AssemblySet.FindMethodDefinition(info.Initializer)
                : mainType?.FindMethod(info.Initializer);
            if (mainType == null) Log.Error("Main type {argMainType} is not found", info.MainType);
            if (targetType == null) Log.Error("Target type {argTargetType} is not found", info.TargetType);
            if (initializer == null) Log.Error("Initializer {argInitializer} is not found", info.Initializer);
            return (mainType, targetType, initializer, info.ILProvider);
        }
    }
}
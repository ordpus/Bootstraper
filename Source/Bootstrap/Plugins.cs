using BootstrapApi;

using Mono.Cecil;
using Mono.Cecil.Cil;

using MonoMod.Utils;

using Serilog;

namespace Bootstrap;

public readonly struct PatchInfo
    (ILogger logger, MethodDefinition method, FieldDefinition field) {
    public ILogger Logger => logger;
    public MethodDefinition Method => method;
    public FieldDefinition Field => field;
}

public readonly struct ValidateInfo(ILogger logger, MethodDefinition method, FieldDefinition field) {
    public ILogger Logger => logger;
    public MethodDefinition Method => method;
    public FieldDefinition Field => field;
}

public readonly struct PostInitInfo
    (Type mainType, Type targetType, MethodDefinition initializer, IPostInitProvider ilProvider) {
    public Type MainType => mainType;
    public Type TargetType => targetType;
    public MethodDefinition Initializer => initializer;
    public IPostInitProvider ILProvider => ilProvider;
}

public interface IPostInitProvider {
    bool Validate(ValidateInfo info, PostInitInfo postInitInfo);
    IEnumerable<Instruction> Provide(MethodDefinition initializer, FieldDefinition field);
}

public class DefaultPostInitProvider(string methodName) : IPostInitProvider {
    public bool Validate(ValidateInfo info, PostInitInfo postInitInfo) {
        var getMethod = GetGetMethod(info.Field.DeclaringType);
        if (getMethod == null) {
            info.Logger.Error(
                "Get method {typeName}.{methodName} is not found",
                info.Field.DeclaringType.FullName,
                methodName);
            return false;
        }

        if (getMethod.Parameters.Count > 1) {
            info.Logger.Error(
                "Get method {methodName} has more than one parameter",
                getMethod.FullName);
            return false;
        }

        var returnType = getMethod.ReturnType.ToType();

        if (getMethod.ReturnType == getMethod.Module.TypeSystem.Void
            || returnType == null
            || !postInitInfo.TargetType.IsAssignableFrom(returnType)) {
            info.Logger.Error(
                "Get method {typeName}.{methodName} return type {returnType} is not {compTypeName}",
                info.Field.DeclaringType.FullName,
                postInitInfo.ILProvider,
                getMethod.ReturnType.FullName,
                postInitInfo.TargetType.FullName);
            return false;
        }

        if (getMethod.Parameters.Count == 1
            && getMethod.Parameters[0].ParameterType.FullName != typeof(Type).FullName) {
            info.Logger.Error(
                "Get method {typeName}.{methodName} parameter type {parameterTypeName} is not {nameofType}",
                info.Field.DeclaringType.FullName,
                postInitInfo.ILProvider,
                getMethod.Parameters[0].ParameterType.FullName,
                typeof(Type).FullName);
            return false;
        }

        if (!getMethod.HasParameters
            && !(getMethod.HasGenericParameters
                 && getMethod.GenericParameters.Count == 1)) {
            info.Logger.Error(
                "Get method {typeName}.{methodName} must have one parameter or generic parameter",
                info.Field.DeclaringType.FullName,
                postInitInfo.ILProvider);
            return false;
        }

        return true;
    }

    public IEnumerable<Instruction> Provide(MethodDefinition initializer, FieldDefinition field) {
        var getMethod = GetGetMethod(field.DeclaringType)!;
        var fieldReference = initializer.Module.ImportReference(field);
        yield return Instruction.Create(OpCodes.Ldarg_0);
        yield return Instruction.Create(OpCodes.Ldarg_0);
        if (getMethod.HasGenericParameters) {
            var genericGet = new GenericInstanceMethod(getMethod);
            genericGet.GenericArguments.Add(fieldReference.FieldType);
            yield return Instruction.Create(OpCodes.Callvirt, genericGet);
        } else {
            yield return Instruction.Create(OpCodes.Ldtoken, fieldReference.FieldType);
            yield return Instruction.Create(
                OpCodes.Call,
                getMethod.Module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))));
            yield return Instruction.Create(OpCodes.Callvirt, getMethod);
            yield return Instruction.Create(OpCodes.Castclass, fieldReference.FieldType);
        }

        yield return Instruction.Create(OpCodes.Stfld, fieldReference);
    }

    private MethodReference? GetGetMethod(TypeDefinition type) {
        if (!methodName.Contains(":")) return type.FindMethod(methodName);
        var typeName = methodName.Split(':')[0];
        var rawMethodName = methodName.Split(':')[1];
        MethodReference? method = type.Module.GetType(typeName)?.FindMethod(rawMethodName);
        if (method != null) return method;

        var toImport = AssemblySet.FindMethodDefinition(methodName);
        if (toImport != null) method = type.Module.ImportReference(toImport);

        return method;
    }

}

public interface IAddFieldPlugin {
    string AttributeName();

    bool Validate(ValidateInfo info);

    void Patch(PatchInfo info);
}

public static class BootstrapPluginManager {
    private static readonly List<IAddFieldPlugin> AddFieldPluginsField = [];
    private static readonly List<PostInitInfo> PostInitInfosField = [];

    public static void Register(IAddFieldPlugin plugin) => AddFieldPluginsField.Add(plugin);

    public static void Register(PostInitInfo info) => PostInitInfosField.Add(info);

    public static IReadOnlyList<IAddFieldPlugin> AddFieldPlugins => AddFieldPluginsField;

    public static IReadOnlyList<PostInitInfo> PostInitInfos => PostInitInfosField;
}
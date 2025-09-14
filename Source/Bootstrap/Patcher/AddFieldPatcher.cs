using BootstrapApi;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

using MonoMod.Utils;

using Serilog;
using Serilog.Formatting.Display;

using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace Bootstrap.Patcher;

internal class AddFieldPatcher(IReadOnlyList<IAddFieldPlugin> plugins) {
    public static readonly ILogger Logger;

    static AddFieldPatcher() {
        BootstrapPluginManager.Register(new AddFieldDefaultValueDirectPlugin());
        BootstrapPluginManager.Register(new AddFieldDefaultValueInjectorPlugin());
        BootstrapPluginManager.Register(new AddFieldPostInitField());


        BootstrapUtility.RollLogFile("Bootstrap/logs/add-field.log");
        Logger = new LoggerConfiguration()
                 .WriteTo
                 .File(
                     new MessageTemplateTextFormatter(
                         "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Source}] {Message}{NewLine}{Exception}"),
                     "Bootstrap/logs/add-field.log")
                 .CreateLogger()
                 .ForContext("Source", "AddField");
    }

    internal List<ModuleDefinition> Patch() {
        var pluginsDict = plugins.GroupBy(x => x.AttributeName())
                                 .ToDictionary(
                                     x => x.Key,
                                     x => x.ToList());
        var toAddInfo = AssemblySet.Modules.Values
                                   .SelectMany(x => x.Types)
                                   .SelectMany(x => x.Methods)
                                   .Where(x => x.HasCustomAttribute(typeof(AddFieldAttribute).FullName!))
                                   .Where(ValidateField)
                                   .Select(x => (x, AddField(x)))
                                   .Select(t => new PatchInfo(t.x, t.Item2))
                                   .ToList();
        toAddInfo.ForEach(x =>
            x.Method.CustomAttributes.Select(y => y.AttributeType)
             .Select(y => y.FullName)
             .Where(pluginsDict.ContainsKey)
             .SelectMany(y => pluginsDict[y])
             .Where(y => y.Validate(new ValidateInfo(x.Method, x.Field)))
             .ToList()
             .ForEach(y => y.Patch(x)));
        return toAddInfo.Select(x => x.Method.Module)
                        .Concat(toAddInfo.Select(x => x.Field.Module))
                        .Distinct()
                        .ToList();
    }

    internal static FieldDefinition AddField(MethodDefinition method) {
        var fieldName = method.Module.Assembly.Name.Name + "_" + method.Name + method.MetadataToken.RID;
        Logger.Information("Adding Field {fieldName}", fieldName);
        var typeBase = method.Parameters[0].ParameterType;
        var targetType = typeBase.Resolve();
        var fieldTypeBase = method.ReturnType is ByReferenceType byRefType
            ? byRefType.ElementType
            : method.ReturnType;
        TypeReference fieldType;
        if (fieldTypeBase is GenericInstanceType genericType) {
            var genericMappingType = genericType.GenericArguments
                                                .Distinct()
                                                .Zip(targetType.GenericParameters, (x, y) => (x, y))
                                                .ToDictionary(x => x.x, y => y.y);
            var finalGeneric = genericType.GenericArguments
                                          .Select(x => x.IsGenericParameter
                                              ? genericMappingType[
                                                  genericType.GenericArguments.FirstOrDefault(y => y.Name == x.Name)]
                                              : x)
                                          .ToArray();
           fieldType = targetType.Module.ImportReference(genericType.ElementType)
                                  .MakeGenericInstanceType(finalGeneric);
        } else fieldType = targetType.Module.ImportReference(fieldTypeBase);

        var field = new FieldDefinition(
            fieldName,
            FieldAttributes.Public,
            targetType.Module.ImportReference(fieldType, targetType));
        targetType.Fields.Add(field);
        method.ImplAttributes &= MethodImplAttributes.InternalCall;
        var fieldReference = method.Module.ImportReference(field, method.Module.ImportReference(targetType));
        var body = method.Body = new MethodBody(method);
        var il = body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        if (typeBase.IsByReference && !fieldType.IsValueType) il.Emit(OpCodes.Ldind_Ref);
        il.Emit(method.ReturnType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, fieldReference);
        il.Emit(OpCodes.Ret);
        return field;
    }

    internal static string FullNameWithoutGeneric(TypeReference type) {
        var fullName = type.FullName;
        var genericMarkerIndex = fullName.IndexOf('<');
        return genericMarkerIndex != -1 ? fullName.Substring(0, genericMarkerIndex) : fullName;
    }

    internal static bool ValidateField(MethodDefinition method) {
        return MethodValidate() && MethodParameterValidate();

        bool MethodValidate() {
            if (!method.IsStatic) {
                Logger.Error(
                    "Add Field {typeName}.{methodName} is not static",
                    method.DeclaringType?.FullName,
                    method.Name);
                return false;
            }

            if (method.ReturnType.GetElementType() == method.Module.TypeSystem.Void) {
                Logger.Error(
                    "Add Field {typeName}.{methodName} has no return",
                    method.DeclaringType?.FullName,
                    method.Name);
                return false;
            }

            return true;
        }

        bool MethodParameterValidate() {
            if (method.Parameters.Count != 1) {
                Logger.Error(
                    "Add Field {typeName}.{methodName} has more than one parameter",
                    method.DeclaringType?.FullName,
                    method.Name);
                return false;
            }

            if (method.Parameters[0].ParameterType.Resolve().IsInterface) {
                Logger.Error(
                    "Add Field {typeName}.{methodName} parameter is interface",
                    method.DeclaringType?.FullName,
                    method.Name);
                return false;
            }

            if (method.Parameters[0].ParameterType.IsArray) {
                Logger.Error(
                    "Add Field {typeName}.{methodName} parameter is array",
                    method.DeclaringType?.FullName,
                    method.Name);
                return false;
            }

            var genericType = method.Parameters[0].ParameterType as GenericInstanceType;
            if (genericType?.GenericArguments.Any(x => !x.IsGenericParameter) ?? false) {
                Logger.Error(
                    "Add Field {typeName}.{methodName} parameter is closed generic",
                    method.DeclaringType?.FullName,
                    method.Name);
                return false;
            }

            if (!(genericType?.GenericArguments ?? Enumerable.Empty<TypeReference>()).SequenceEqual(
                    method.GenericParameters)) {
                Logger.Error(
                    "Add Field {typeName}.{methodName} generic parameter is not same",
                    method.DeclaringType?.FullName,
                    method.Name);
                return false;
            }

            return true;
        }
    }
}

internal class AddFieldDefaultValueDirectPlugin : IAddFieldPlugin {
    private static readonly ILogger Logger = AddFieldPatcher.Logger.ForContext(
        "Source",
        "AddFieldDefaultValueDirectPlugin");

    public string AttributeName() {
        return typeof(DefaultValueDirectAttribute).FullName!;
    }

    public bool Validate(ValidateInfo info) {
        var defaultValueAttribute = info.Method.GetCustomAttribute(AttributeName());
        var value = defaultValueAttribute?.GetConstructor<object>(0);
        if (value == null) return false;
        if (value.GetType().FullName == info.Method.ReturnType.GetElementType().FullName) return true;

        Logger.Error(
            "{method} return type {methodReturn} is not equal to {valueType}",
            info.Method.Name,
            info.Method.ReturnType.FullName,
            value.GetType().FullName);
        return false;
    }

    public void Patch(PatchInfo info) {
        var constructors = info.Field
                               .DeclaringType
                               .GetConstructors()
                               .Where(x => !x.IsStatic);
        var value = info.Method.GetCustomAttribute(AttributeName())!.GetConstructor<object>(0)!;
        foreach (var constructor in constructors) {
            BootstrapUtility.InsertBefore(
                constructor,
                x => x.OpCode == OpCodes.Ret,
                [
                    Instruction.Create(OpCodes.Ldarg_0),
                    Type.GetTypeCode(value.GetType()) switch {
                        TypeCode.Boolean => Instruction.Create((bool)value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                        TypeCode.Byte => Instruction.Create(OpCodes.Ldc_I4, (int)(byte)value),
                        TypeCode.SByte => Instruction.Create(OpCodes.Ldc_I4, (int)(sbyte)value),
                        TypeCode.Char => Instruction.Create(OpCodes.Ldc_I4, (char)value),
                        TypeCode.Int16 => Instruction.Create(OpCodes.Ldc_I4, (short)value),
                        TypeCode.UInt16 => Instruction.Create(OpCodes.Ldc_I4, (ushort)value),
                        TypeCode.Int32 => Instruction.Create(OpCodes.Ldc_I4, (int)value),
                        TypeCode.UInt32 => Instruction.Create(OpCodes.Ldc_I4, (int)(uint)value),
                        TypeCode.Int64 => Instruction.Create(OpCodes.Ldc_I8, (long)value),
                        TypeCode.UInt64 => Instruction.Create(OpCodes.Ldc_I8, (long)(ulong)value),
                        TypeCode.Single => Instruction.Create(OpCodes.Ldc_R4, (float)value),
                        TypeCode.Double => Instruction.Create(OpCodes.Ldc_R8, (double)value),
                        TypeCode.String => Instruction.Create(OpCodes.Ldstr, (string)value),
                        _ => throw new ArgumentException($"Invalid default type {value.GetType()}")
                    },
                    Instruction.Create(OpCodes.Stfld, info.Field)
                ]);
        }
    }
}

internal class AddFieldDefaultValueInjectorPlugin : IAddFieldPlugin {
    private static readonly ILogger Logger = AddFieldPatcher.Logger.ForContext(
        "Source",
        "AddFieldDefaultValueInjectorPlugin");

    public string AttributeName() {
        return typeof(DefaultValueInjectorAttribute).FullName!;
    }

    public bool Validate(ValidateInfo info) {
        var injector = FindInjector(info.Method);
        if (injector == null) {
            Logger.Error("Injector is null");
            return false;
        }

        if (!injector.IsStatic) {
            Logger.Error(
                "Injector {typeName}.{methodName} is not static",
                info.Method.DeclaringType.FullName,
                injector.Name);
            return false;
        }

        if (injector.ReturnType.FullName != info.Field.FieldType.FullName) {
            Logger.Error(
                "Injector {typeName}.{methodName} return is not {fieldTypeName}",
                info.Method.DeclaringType.FullName,
                injector.Name,
                info.Field.FieldType.FullName);
            return false;
        }

        switch (injector.Parameters.Count) {
            case > 1:
                Logger.Error(
                    "Injector {typeName}.{methodName} has more than one parameter",
                    info.Method.DeclaringType.FullName,
                    injector.Name);
                return false;
            case 1
                when injector.Parameters[0].ParameterType.FullName != info.Field.DeclaringType.FullName:
                Logger.Error(
                    "Injector {typeName}.{methodName} parameter type is not {fieldTypeName}",
                    info.Method.DeclaringType.FullName,
                    injector.Name,
                    info.Field.FieldType.FullName
                );
                return false;
            default:
                return true;
        }
    }

    public void Patch(PatchInfo info) {
        var injector = FindInjector(info.Method)!;
        var injectorReference = info.Field.Module.ImportReference(injector);
        var ctors = info.Field.DeclaringType
                        .GetConstructors()
                        .Where(x => !x.IsStatic)
                        .ToList();
        foreach (var constructor in ctors) {
            BootstrapUtility.InsertBefore(
                constructor,
                x => x.OpCode == OpCodes.Ret,
                [
                    Instruction.Create(OpCodes.Ldarg_0),
                    injectorReference.Parameters.Count == 1
                        ? Instruction.Create(OpCodes.Ldarg_0)
                        : Instruction.Create(OpCodes.Nop),
                    Instruction.Create(OpCodes.Call, injectorReference),
                    Instruction.Create(OpCodes.Stfld, info.Field)
                ]);
        }
    }

    private MethodDefinition? FindInjector(MethodDefinition method) {
        var value = method.GetCustomAttribute(AttributeName())?.GetConstructor<string>(0);
        return method.DeclaringType.FindMethod(value ?? "");
    }
}

internal class AddFieldPostInitField : IAddFieldPlugin {
    private static readonly ILogger Logger = AddFieldPatcher.Logger.ForContext(
        "Source",
        "AddFieldPostInitField");

    public string AttributeName() {
        return typeof(PostInitFieldAttribute).FullName!;
    }

    public bool Validate(ValidateInfo info) {
        if (info.Method.ReturnType.IsByReference) {
            Logger.Error(
                "PostInitField {typeName}.{methodName} return type is by reference",
                info.Method.DeclaringType.FullName,
                info.Method.Name);
            return false;
        }

        var baseType = info.Field.DeclaringType;
        if (baseType == null) {
            Logger.Error(
                "Base type {typeName} is not found",
                info.Field.DeclaringType.FullName);
            return false;
        }

        var fieldType = info.Field.FieldType;
        if (fieldType == null) {
            Logger.Error(
                "Field type {typeName} is not found",
                info.Field.FieldType.FullName);
            return false;
        }

        if (!BootstrapPluginManager.FindResolvePostInitInfo(baseType, fieldType, out _)) {
            Logger.Error(
                "No comp combination found for ({typeName}, {fieldName}), existing combinations: {combination}",
                info.Field.DeclaringType.FullName,
                info.Field.Name,
                BootstrapPluginManager.ResolvedPostInitInfos.Select(x =>
                    $"({x.MainType.FullName}, {x.TargetType.FullName})"));
            return false;
        }


        return true;
    }

    public void Patch(PatchInfo info) {
        BootstrapPluginManager.FindResolvePostInitInfo(
            info.Field.DeclaringType,
            info.Field.FieldType,
            out var compInfo);
        BootstrapUtility.InsertBefore(
            compInfo.Initializer,
            x => x.OpCode == OpCodes.Ret,
            compInfo.ILProvider.Provide(compInfo, info).ToList());
    }
}

public class DefaultPostInitProvider(MethodDefinition method) : IPostInitProvider {
    private static readonly ILogger Logger = AddFieldPatcher.Logger.ForContext("Source", "DefaultPostInitProvider");

    public DefaultPostInitProvider(string methodName) :
        this(AssemblySet.FindMethodDefinition(methodName)!) { }

    public bool Validate(ValidateInfo info, ResolvedPostInitInfo postInitInfo) {
        if (method.Parameters.Count > 1) {
            Logger.Error(
                "Get method {methodName} has more than one parameter",
                method.FullName);
            return false;
        }

        var returnType = method.ReturnType;

        if (method.ReturnType == method.Module.TypeSystem.Void
            || returnType == null
            || !postInitInfo.TargetType.IsAssignableFrom(returnType)) {
            AddFieldPatcher.Logger.Error(
                "Get method {typeName}.{methodName} return type {returnType} is not {compTypeName}",
                info.Field.DeclaringType.FullName,
                postInitInfo.ILProvider,
                method.ReturnType.FullName,
                postInitInfo.TargetType.FullName);
            return false;
        }

        if (method.Parameters.Count == 1
            && method.Parameters[0].ParameterType.FullName != typeof(Type).FullName) {
            Logger.Error(
                "Get method {typeName}.{methodName} parameter type {parameterTypeName} is not {nameofType}",
                info.Field.DeclaringType.FullName,
                postInitInfo.ILProvider,
                method.Parameters[0].ParameterType.FullName,
                typeof(Type).FullName);
            return false;
        }

        if (!method.HasParameters
            && !(method.HasGenericParameters
                 && method.GenericParameters.Count == 1)) {
            Logger.Error(
                "Get method {typeName}.{methodName} must have one parameter or generic parameter",
                info.Field.DeclaringType.FullName,
                postInitInfo.ILProvider);
            return false;
        }

        return true;
    }

    public IEnumerable<Instruction> Provide(ResolvedPostInitInfo info, PatchInfo patch) {
        Logger.Information(
            "Post Init {methodName} for {field} with initializer {initializer}",
            method.FullName,
            patch.Field.FullName,
            info.Initializer.FullName);
        var fieldReference = info.Initializer.Module.ImportReference(patch.Field);
        var fieldDeclareType = patch.Field.DeclaringType;
        var shouldSkip = !fieldDeclareType.SameReference(info.MainType);
        var skip = Instruction.Create(OpCodes.Nop);
        if (shouldSkip) {
            yield return Instruction.Create(OpCodes.Ldarg_0);
            yield return Instruction.Create(OpCodes.Isinst, fieldDeclareType);
            yield return Instruction.Create(OpCodes.Ldnull);
            yield return Instruction.Create(OpCodes.Cgt_Un);
            yield return Instruction.Create(OpCodes.Brfalse, skip);
        }

        yield return Instruction.Create(OpCodes.Ldarg_0);
        yield return Instruction.Create(OpCodes.Ldarg_0);
        if (method.HasGenericParameters) {
            var genericGet = new GenericInstanceMethod(method);
            genericGet.GenericArguments.Add(fieldReference.FieldType);
            yield return Instruction.Create(OpCodes.Callvirt, genericGet);
        } else {
            yield return Instruction.Create(OpCodes.Ldtoken, fieldReference.FieldType);
            yield return Instruction.Create(
                OpCodes.Call,
                method.Module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))));
            yield return Instruction.Create(OpCodes.Callvirt, method);
            yield return Instruction.Create(OpCodes.Castclass, fieldReference.FieldType);
        }

        yield return Instruction.Create(OpCodes.Stfld, fieldReference);
        yield return skip;
    }
}
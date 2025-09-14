using BootstrapApi;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

using MonoMod.Utils;

using Serilog;
using Serilog.Formatting.Display;

namespace Bootstrap.Patcher;

internal class AddFieldPatcher(IReadOnlyList<IAddFieldPlugin> plugins) {
    private static readonly ILogger Logger;

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
                                   .Select(t => new PatchInfo(Logger, t.x, t.Item2))
                                   .ToList();
        toAddInfo.ForEach(x =>
            x.Method.CustomAttributes.Select(y => y.AttributeType)
             .Select(y => y.FullName)
             .Where(pluginsDict.ContainsKey)
             .SelectMany(y => pluginsDict[y])
             .Where(y => y.Validate(new ValidateInfo(Logger, x.Method, x.Field)))
             .ToList()
             .ForEach(y => y.Patch(x)));
        return toAddInfo.Select(x => x.Method.Module)
                        .Concat(toAddInfo.Select(x => x.Field.Module))
                        .Distinct()
                        .ToList();
    }

    internal static FieldDefinition AddField(MethodDefinition method) {
        var fieldName = method.Module.Assembly.Name.Name + "_" + method.Name + method.MetadataToken.RID;
        var type = method.Parameters[0].ParameterType;
        var targetType = AssemblySet.Modules[type.Resolve().Module.Name].GetType(type.FullName);
        var field = new FieldDefinition(
            fieldName,
            FieldAttributes.Private,
            targetType.Module.ImportReference(method.ReturnType.GetElementType()));
        targetType.Fields.Add(field);
        method.ImplAttributes &= MethodImplAttributes.InternalCall;
        var fieldReference = method.Module.ImportReference(field);
        var body = method.Body = new MethodBody(method);
        var il = body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(method.ReturnType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, fieldReference);
        il.Emit(OpCodes.Ret);
        return field;
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

            return true;
        }
    }
}

internal class AddFieldDefaultValueDirectPlugin : IAddFieldPlugin {
    public string AttributeName() {
        return typeof(DefaultValueDirectAttribute).FullName!;
    }

    public bool Validate(ValidateInfo info) {
        var defaultValueAttribute = info.Method.GetCustomAttribute(AttributeName());
        var value = defaultValueAttribute?.GetConstructor<object>(0);
        if (value == null) return false;
        if (value.GetType().FullName == info.Method.ReturnType.GetElementType().FullName) return true;

        info.Logger.Error(
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
                        TypeCode.Boolean or TypeCode.Byte => Instruction.Create(OpCodes.Ldc_I4, (byte)value),
                        TypeCode.Char => Instruction.Create(OpCodes.Ldc_I4, (char)value),
                        TypeCode.SByte => Instruction.Create(OpCodes.Ldc_I4, (sbyte)value),
                        TypeCode.Int16 => Instruction.Create(OpCodes.Ldc_I4, (short)value),
                        TypeCode.UInt16 => Instruction.Create(OpCodes.Ldc_I4, (ushort)value),
                        TypeCode.Int32 => Instruction.Create(OpCodes.Ldc_I4, (int)value),
                        TypeCode.UInt32 => Instruction.Create(OpCodes.Ldc_I4, (uint)value),
                        TypeCode.Int64 => Instruction.Create(OpCodes.Ldc_I8, (long)value),
                        TypeCode.UInt64 => Instruction.Create(OpCodes.Ldc_I8, (ulong)value),
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
    public string AttributeName() {
        return typeof(DefaultValueInjectorAttribute).FullName!;
    }

    public bool Validate(ValidateInfo info) {
        var injector = FindInjector(info.Method);
        if (injector == null) {
            info.Logger.Error("Injector is null");
            return false;
        }

        if (!injector.IsStatic) {
            info.Logger.Error(
                "Injector {typeName}.{methodName} is not static",
                info.Method.DeclaringType.FullName,
                injector.Name);
            return false;
        }

        if (injector.ReturnType.FullName != info.Field.FieldType.FullName) {
            info.Logger.Error(
                "Injector {typeName}.{methodName} return is not {fieldTypeName}",
                info.Method.DeclaringType.FullName,
                injector.Name,
                info.Field.FieldType.FullName);
            return false;
        }

        switch (injector.Parameters.Count) {
            case > 1:
                info.Logger.Error(
                    "Injector {typeName}.{methodName} has more than one parameter",
                    info.Method.DeclaringType.FullName,
                    injector.Name);
                return false;
            case 1
                when injector.Parameters[0].ParameterType.FullName != info.Field.DeclaringType.FullName:
                info.Logger.Error(
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
        info.Logger.Information("Constructors: {con}", ctors.Count);
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
    public string AttributeName() {
        return typeof(PostInitFieldAttribute).FullName!;
    }

    public bool Validate(ValidateInfo info) {
        if (info.Method.ReturnType.IsByReference) {
            info.Logger.Error(
                "PostInitField {typeName}.{methodName} return type is by reference",
                info.Method.DeclaringType.FullName,
                info.Method.Name);
            return false;
        }

        var compInfos = BootstrapPluginManager.PostInitInfos;
        var baseType = info.Field.DeclaringType.ToType();
        if (baseType == null) {
            info.Logger.Error(
                "Base type {typeName} is not found",
                info.Field.DeclaringType.FullName);
            return false;
        }

        var fieldType = info.Field.FieldType.ToType();
        if (fieldType == null) {
            info.Logger.Error(
                "Field type {typeName} is not found",
                info.Field.FieldType.FullName);
            return false;
        }

        var compInfo = compInfos.FirstOrDefault(x => x.MainType.IsAssignableFrom(baseType)
                                                     && x.TargetType.IsAssignableFrom(fieldType));
        if (compInfo.TargetType == null) {
            info.Logger.Error(
                "No comp combination found for ({typeName}, {fieldName}), existing combinations: {combination}",
                info.Field.DeclaringType.FullName,
                info.Field.Name,
                compInfos.Select(x => $"({x.MainType.FullName}, {x.TargetType.FullName})"));
            return false;
        }


        return true;
    }

    public void Patch(PatchInfo info) {
        var compInfos = BootstrapPluginManager.PostInitInfos;
        var baseType = info.Field.DeclaringType.ToType();
        var fieldType = info.Field.FieldType.ToType();
        var compInfo = compInfos.First(x => x.MainType.IsAssignableFrom(baseType)
                                            && x.TargetType.IsAssignableFrom(fieldType));
        BootstrapUtility.InsertBefore(
            compInfo.Initializer,
            x => x.OpCode == OpCodes.Ret,
            compInfo.ILProvider.Provide(compInfo.Initializer, info.Field).ToList());
    }
}
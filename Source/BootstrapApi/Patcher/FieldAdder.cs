using Bootstrap;

using Microsoft.Extensions.Logging;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

using MonoMod.Utils;

namespace BootstrapApi.Patcher;

internal class AddFieldPatcher
    (ILogger logger, MethodDefinition method, IReadOnlyDictionary<string, ModuleDefinition> modules) {
    private class AddFieldAction
    (
        ILogger logger,
        ModuleDefinition methodModule,
        MethodDefinition method,
        IReadOnlyDictionary<string, ModuleDefinition> modules
    ) {
        internal class DeclaredObjects
        (
            ModuleDefinition targetModule,
            TypeDefinition targetType,
            TypeReference targetTypeReference,
            ModuleDefinition methodModule,
            TypeDefinition methodType,
            TypeReference methodTypeReference,
            FieldDefinition fieldDefinition,
            FieldReference fieldReference
        ) {
            internal readonly ModuleDefinition TargetModule = targetModule;
            internal readonly TypeDefinition TargetType = targetType;
            internal readonly TypeReference TargetTypeReference = targetTypeReference;
            internal readonly ModuleDefinition MethodModule = methodModule;
            internal readonly TypeDefinition MethodType = methodType;
            internal readonly TypeReference MethodTypeReference = methodTypeReference;
            internal readonly FieldDefinition FieldDefinition = fieldDefinition;
            internal readonly FieldReference FieldReference = fieldReference;
        }

        internal bool Patch(out DeclaredObjects result) {
            result = null!;
            if (!ValidateAddField()) return false;
            var type = method.Parameters[0].ParameterType;
            var targetModule = modules[type.Resolve().Module.Name];
            var targetType = targetModule.GetType(type.FullName);
            var targetTypeReference = methodModule.ImportReference(targetType);
            var methodType = method.DeclaringType;
            var methodTypeReference = targetModule.ImportReference(methodType);
            var fieldDefinition = AddField(targetType);
            var fieldReference = methodModule.ImportReference(fieldDefinition);
            PatchAccessor(fieldReference);
            result = new DeclaredObjects(
                targetModule,
                targetType,
                targetTypeReference,
                methodModule,
                methodType,
                methodTypeReference,
                fieldDefinition,
                fieldReference);
            return true;
        }


        private void PatchAccessor(FieldReference fieldReference) {
            method.ImplAttributes &= MethodImplAttributes.InternalCall;
            var body = method.Body = new MethodBody(method);
            var il = body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, fieldReference);
            il.Emit(OpCodes.Ret);
        }

        private FieldDefinition AddField(TypeDefinition targetType) {
            if (!method.ReturnType.IsByReference) method.ReturnType = method.ReturnType.MakeByReferenceType();
            var fieldName = method.Module.Assembly.Name.Name + "_" + method.Name + method.MetadataToken.RID;
            var field = new FieldDefinition(
                fieldName,
                FieldAttributes.Private,
                targetType.Module.ImportReference(method.ReturnType.GetElementType()));
            targetType.Fields.Add(field);
            return field;
        }

        private bool ValidateAddField() {
            return MethodValidate() && MethodParameterValidate();

            bool MethodValidate() {
                if (!method.IsStatic) {
                    logger.LogError(
                        "Add Field {typeName}.{methodName} is not static",
                        method.DeclaringType?.FullName,
                        method.Name);
                    return false;
                }

                if (method.ReturnType.GetElementType() == methodModule.TypeSystem.Void) {
                    logger.LogError(
                        "Add Field {typeName}.{methodName} has no return",
                        method.DeclaringType?.FullName,
                        method.Name);
                    return false;
                }

                if (!method.ReturnType.IsByReference) {
                    logger.LogError(
                        "Add Field {typeName}.{methodName} return is not by reference",
                        method.DeclaringType?.FullName,
                        method.Name);
                }

                return true;
            }

            bool MethodParameterValidate() {
                if (method.Parameters.Count != 1) {
                    logger.LogError(
                        "Add Field {typeName}.{methodName} has more than one parameter",
                        method.DeclaringType?.FullName,
                        method.Name);
                    return false;
                }

                return true;
            }
        }
    }

    private class AddDirectDefaultValueAction
    (
        ILogger logger,
        MethodDefinition method,
        AddFieldAction.DeclaredObjects objects
    ) {
        internal bool Patch() {
            var defaultValueAttribute = method.GetCustomAttribute(typeof(DefaultValueDirectAttribute).FullName!);
            var value = defaultValueAttribute?.GetConstructor<object>(0);
            if (value == null || !ValidateAddDefaultValue(value)) return false;
            var constructors = objects.TargetType.GetConstructors().Where(x => !x.IsStatic);
            foreach (var constructor in constructors) {
                Utility.InsertBefore(
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
                        Instruction.Create(OpCodes.Stfld, objects.FieldDefinition)
                    ]);
            }

            return true;
        }

        private bool ValidateAddDefaultValue(object value) {
            return ValueValidate();

            bool ValueValidate() {
                if (value.GetType().FullName != method.ReturnType.GetElementType().FullName) {
                    logger.LogError(
                        "{method} return type {methodReturn} is not equal to {valueType}",
                        method.Name,
                        method.ReturnType.FullName,
                        value.GetType().FullName);
                    return false;
                }

                return true;
            }
        }
    }

    private class AddDefaultInjectorAction
        (ILogger logger, MethodDefinition method, AddFieldAction.DeclaredObjects objects) {
        internal bool Patch() {
            var defaultValueAttribute = method.GetCustomAttribute(typeof(DefaultValueInjectorAttribute).FullName!);
            if (defaultValueAttribute == null) return false;
            var value = defaultValueAttribute.GetConstructor<string>(0);
            var injectorDefinition = objects.MethodType.FindMethod(value ?? "");
            if (!ValidateAddDefaultInjector(
                    injectorDefinition,
                    objects.FieldDefinition,
                    objects.MethodType,
                    objects.TargetType)) {
                return false;
            }

            var injectorReference = objects.TargetModule.ImportReference(injectorDefinition);
            var ctors = objects.TargetType.GetConstructors().Where(x => !x.IsStatic).ToList();
            logger.LogInformation("Methods: {}", ctors.Select(x => x.FullName));
            foreach (var constructor in ctors) {
                Utility.InsertBefore(
                    constructor,
                    x => x.OpCode == OpCodes.Ret,
                    [
                        Instruction.Create(OpCodes.Ldarg_0), 
                        injectorReference.Parameters.Count == 1
                            ? Instruction.Create(OpCodes.Ldarg_0)
                            : Instruction.Create(OpCodes.Nop),
                        Instruction.Create(OpCodes.Call, injectorReference),
                        Instruction.Create(OpCodes.Stfld, objects.FieldDefinition)
                    ]);
            }

            return true;
        }

        private bool ValidateAddDefaultInjector(
            MethodDefinition? injectionDefinition, FieldDefinition fieldDefinition, TypeDefinition methodType,
            TypeDefinition targetType) {
            return ValidateInjector();

            bool ValidateInjector() {
                if (injectionDefinition == null) {
                    logger.LogError("Injector is null");
                    return false;
                }

                if (!injectionDefinition.IsStatic) {
                    logger.LogError(
                        "Injector {typeName}.{methodName} is not static",
                        methodType.FullName,
                        injectionDefinition.Name);
                    return false;
                }

                if (injectionDefinition.ReturnType.FullName != fieldDefinition.FieldType.FullName) {
                    logger.LogError(
                        "Injector {typeName}.{methodName} return is not {fieldTypeName}",
                        methodType.FullName,
                        injectionDefinition.Name,
                        fieldDefinition.FieldType.FullName);
                    return false;
                }

                if (injectionDefinition.Parameters.Count > 1) {
                    logger.LogError(
                        "Injector {typeName}.{methodName} has more than one parameter",
                        methodType.FullName,
                        injectionDefinition.Name);
                    return false;
                }

                if (injectionDefinition.Parameters.Count == 1
                    && injectionDefinition.Parameters[0].ParameterType.FullName != targetType.FullName) {
                    logger.LogError(
                        "Injector {typeName}.{methodName} parameter type is not {fieldTypeName}",
                        methodType.FullName,
                        injectionDefinition.Name,
                        fieldDefinition.FieldType.FullName
                    );
                    return false;
                }

                return true;
            }
        }
    }

    private class InjectComponentInjectorAction
        (ILogger logger, MethodDefinition method, AddFieldAction.DeclaredObjects objects) {
    }

    internal List<ModuleDefinition> Patch() {
        if (!new AddFieldAction(logger, method.Module, method, modules).Patch(out var objects1)) return [];
        var result = new List<ModuleDefinition> { objects1.MethodModule, objects1.TargetModule };
        if (new AddDirectDefaultValueAction(logger, method, objects1).Patch()) return result;
        new AddDefaultInjectorAction(logger, method, objects1).Patch();
        return result;
    }
}
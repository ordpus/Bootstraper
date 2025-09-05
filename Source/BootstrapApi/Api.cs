using System.Runtime.InteropServices;
// ReSharper disable once CheckNamespace

namespace BootstrapApi;

[AttributeUsage(AttributeTargets.Method)]
public class AddFieldAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class FreePatchAttribute(string id, string module, string[] importModules) : Attribute {
    public readonly string ID = id;
    public readonly string Module = module;
    public readonly string[] ImportModules = importModules;
}

[AttributeUsage(AttributeTargets.Method)]
public class DefaultValueDirectAttribute(object value) : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class DefaultValueInjectorAttribute(string method) : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class InjectComponentAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class InjectModExtensionAttribute : Attribute;
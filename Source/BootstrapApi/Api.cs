namespace BootstrapApi;

[AttributeUsage(AttributeTargets.Method)]
public class AddFieldAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class FreePatchAttribute(string id, string module, params string[] importModules) : Attribute {
    public readonly string ID = id;
    public readonly string Module = module;
    public readonly string[] ImportModules = importModules;
}

[AttributeUsage(AttributeTargets.Method)]
public class DefaultValueDirectAttribute(object value) : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class DefaultValueInjectorAttribute(string method) : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class PostInitFieldAttribute : Attribute {
    public PostInitFieldAttribute() { }

    public PostInitFieldAttribute(Type baseType) { }

    public PostInitFieldAttribute(Type baseType, Type fieldType) { }
}


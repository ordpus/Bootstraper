// ReSharper disable once CheckNamespace

namespace BootstrapApi;

public interface IDefaultValueInjector {
    object? Supply(object? value);
}

public abstract class DefaultValueInjector<TP, TR> : IDefaultValueInjector {
    public abstract TR? Supply(TP? value);

    public object? Supply(object? value) {
        return Supply((TP?)value);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class AddFieldAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class FreePatchAttribute(string id, string module) : Attribute {
    public string id = id;
    public string module = module;
}

[AttributeUsage(AttributeTargets.Method)]
public class DefaultValueAttribute(object? value) : Attribute {
    public object? value = value;
}

[AttributeUsage(AttributeTargets.Method)]
public class DefaultValueInjectorAttribute : Attribute {
    public IDefaultValueInjector injector;

    public DefaultValueInjectorAttribute(Type type) {
        if (!type.IsSubclassOf(typeof(IDefaultValueInjector)))
            throw new ArgumentException($"Expected subclass if IDefaultValueInjector, but is {type.FullName}");
        injector = (IDefaultValueInjector)Activator.CreateInstance(type);
    }
}
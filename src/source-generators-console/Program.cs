using System.Reflection;


partial class Program
{
    static void Main(string[] args)
    {
        HelloFrom("Generated Code");

        var fullname = typeof(ServiceAttribute<>).FullName;

        var instance = Container.Instance;

        var container = instance.GetService(typeof(IServiceProvider)); // returns null right now.. should return self
        var myObject = instance.GetService(typeof(IMyObject));
                

        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes().Where(x => x.GetInterfaces().Contains(typeof(ISomeType)));

        foreach(var type in types)
        {
            Console.WriteLine(type.Name);

            if( type.GetConstructors().Any(x=>x.GetParameters().Length == 0) )
            {
                var obj = Activator.CreateInstance(type);
                if (obj is ISomeType someType)
                {
                    Console.WriteLine(someType.Name);
                }
            }
        }
    }

    static partial void HelloFrom(string name);
}


public interface ISomeType
{
    string Name { get; }
}

public interface IContainer : IServiceProvider
{

}

/// <summary>
/// Container class. The rest will be implemented by the genreator.
/// </summary>
public partial class Container
{
    public static IContainer Instance { get; } = null!;
}

/// <summary>
/// Serivce attribute to put on the serviceClass
/// Also it should proberly be possible to add as an assembly attribute instead: 
/// Ex.: [assembly:ServiceRegistrationAttribute<TContract,TImplementation>(ServiceLifetime.Transient)]
/// </summary>
public class ServiceAttribute<T> : Attribute
{
    public Type Type { get; } = typeof(T);
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;
}

public enum ServiceLifetime
{
    Transient,
    Scoped,
    Singleton,
}

public interface IMyObject { }

[ServiceAttribute<IMyObject>(Lifetime = ServiceLifetime.Singleton)]
public class MyObject : IMyObject
{
    private readonly MyObjectDependency _dependency;
    public MyObject(MyObjectDependency dependency)
    {
        _dependency = dependency;
    }
}


[ServiceAttribute<MyObjectDependency>]
public class MyObjectDependency
{

}

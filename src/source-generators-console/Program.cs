
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

partial class Program
{
    static void Main(string[] args)
    {
        HelloFrom("Generated Code");


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
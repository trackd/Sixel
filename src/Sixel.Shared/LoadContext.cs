using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Sixel.Shared;

public class LoadContext : AssemblyLoadContext
{
    private static LoadContext? _instance;
    private static object _sync = new object();

    private Assembly _thisAssembly;
    private AssemblyName _thisAssemblyName;
    private Assembly _moduleAssembly;
    private string _assemblyDir;

    private LoadContext(string mainModulePathAssemblyPath)
        : base (name: "Sixel", isCollectible: false)
    {
        _assemblyDir = Path.GetDirectoryName(mainModulePathAssemblyPath) ?? "";
        _thisAssembly = typeof(LoadContext).Assembly;
        _thisAssemblyName = _thisAssembly.GetName();
        _moduleAssembly = LoadFromAssemblyPath(mainModulePathAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Checks to see if we are trying to access our current assembly
        // (ALCLoader.Shared). If so return the already loaded assembly object
        // as it provides a common interface between Pwsh and the ALC.
        if (AssemblyName.ReferenceMatchesDefinition(_thisAssemblyName, assemblyName))
        {
            return _thisAssembly;
        }

        // Checks to see if the assembly exists in our path, if so load it in
        // the ALC. Otherwise fallback to the default loading behaviour.
        string asmPath = Path.Join(_assemblyDir, $"{assemblyName.Name}.dll");
        if (File.Exists(asmPath))
        {
            return LoadFromAssemblyPath(asmPath);
        }
        else
        {
            return null;
        }
    }

    public static Assembly Initialize()
    {
        LoadContext? instance = _instance;
        if (instance is not null)
        {
            return instance._moduleAssembly;
        }

        lock (_sync)
        {
            if (_instance is not null)
            {
                return _instance._moduleAssembly;
            }

            string assemblyPath = typeof(LoadContext).Assembly.Location;
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            // Removes the '.Shared' from the assembly name to refer to our main module.
            string moduleName = assemblyName.Substring(0, assemblyName.Length - 7);
            string modulePath = Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
                $"{moduleName}.dll"
            );

            // Creates the ALC which loads our module in the ALC and returns
            // the loaded Assembly object for the psm1 to load.
            _instance = new LoadContext(modulePath);
            return _instance._moduleAssembly;
        }
    }
}

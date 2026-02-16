// AssemblyLoadContext won't work in net472 so we conditionally compile this
// for net5.0 or greater.
// 5.1 uses => src/Sixel/Helpers/ModuleAssemblyInitializer.cs
#if NET5_0_OR_GREATER
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Sixel.Shared;

/// <summary>
/// Custom AssemblyLoadContext for isolating and resolving assemblies in .NET 5.0 or greater environments.
/// </summary>
public class LoadContext : AssemblyLoadContext {
    private static LoadContext? _instance;
    private static readonly object _sync = new();

    private readonly Assembly _thisAssembly;
    private readonly AssemblyName _thisAssemblyName;
    private readonly Assembly _moduleAssembly;
    private readonly string _assemblyDir;

    private LoadContext(string mainModulePathAssemblyPath)
        : base(name: "Sixel", isCollectible: false) {
        _assemblyDir = Path.GetDirectoryName(mainModulePathAssemblyPath) ?? "";
        _thisAssembly = typeof(LoadContext).Assembly;
        _thisAssemblyName = _thisAssembly.GetName();
        _moduleAssembly = LoadFromAssemblyPath(mainModulePathAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName) {
        if (AssemblyName.ReferenceMatchesDefinition(_thisAssemblyName, assemblyName)) {
            return _thisAssembly;
        }
        string asmPath = Path.Join(_assemblyDir, $"{assemblyName.Name}.dll");
        return File.Exists(asmPath) ? LoadFromAssemblyPath(asmPath) : null;
    }

    public static Assembly Initialize() {
        LoadContext? instance = _instance;
        if (instance is not null) {
            return instance._moduleAssembly;
        }

        lock (_sync) {
            if (_instance is not null) {
                return _instance._moduleAssembly;
            }

            string assemblyPath = typeof(LoadContext).Assembly.Location;
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            string moduleName = assemblyName[..^7];
            string modulePath = Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
                $"{moduleName}.dll"
            );
            _instance = new LoadContext(modulePath);
            return _instance._moduleAssembly;
        }
    }
}
#endif

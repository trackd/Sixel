// 5.1 (net472) can't do ALC so we can just have the resolver here with conditional compilation.
// > net8.0 will use => src/Sixel.Shared/LoadContext.cs
#if NET472
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Sixel;

public sealed class ModuleAssemblyInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup {
    private static readonly ResolveEventHandler s_resolveHandler = Resolve;

    public void OnImport() => AppDomain.CurrentDomain.AssemblyResolve += s_resolveHandler;

    public void OnRemove(PSModuleInfo psModuleInfo) => AppDomain.CurrentDomain.AssemblyResolve -= s_resolveHandler;

    private static Assembly? Resolve(object? sender, ResolveEventArgs args) {
        AssemblyName assemblyName = new(args.Name);
        if (assemblyName.Name is null) {
            return null;
        }

        Assembly? loadedAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(existing => AssemblyName.ReferenceMatchesDefinition(existing.GetName(), assemblyName));
        if (loadedAssembly is not null) {
            return loadedAssembly;
        }

        string moduleAssemblyDir = Path.GetDirectoryName(typeof(ModuleAssemblyInitializer).Assembly.Location) ?? string.Empty;
        string candidatePath = Path.Combine(moduleAssemblyDir, $"{assemblyName.Name}.dll");

        return File.Exists(candidatePath)
            ? Assembly.LoadFrom(candidatePath)
            : null;
    }
}
#endif

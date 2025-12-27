#if NET472
using System;
using System.IO;
using System.Reflection;

namespace Sixel.Shared;

/// <summary>
/// Provides assembly resolution logic for .NET Framework 4.7.2, enabling dynamic loading of dependencies from the module directory.
/// </summary>
public static class AssemblyResolver {
    public static ResolveEventHandler ResolveHandler = new(Resolve);

    public static Assembly? Resolve(object? sender, ResolveEventArgs args) {
        AssemblyName asName = new(args.Name);
        string asPath = Path.Combine(
                Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location),
                $"{asName.Name}.dll");
        return File.Exists(asPath) ? Assembly.LoadFile(asPath) : null;
    }
}
#endif

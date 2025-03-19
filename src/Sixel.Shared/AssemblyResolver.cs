#if NET472
using System;
using System.IO;
using System.Reflection;

namespace Sixel.Shared;

public static class AssemblyResolver
{
  public static ResolveEventHandler ResolveHandler = new(Resolve);

  public static Assembly? Resolve(object? sender, ResolveEventArgs args)
  {
    AssemblyName asName = new(args.Name);
    string asPath = Path.Combine(
            Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location),
            $"{asName.Name}.dll");
    if (File.Exists(asPath))
    {
        return Assembly.LoadFile(asPath);
    }
    return null;
  }
}
#endif

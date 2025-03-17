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

        if (asName.Name == "System.Runtime.CompilerServices.Unsafe" && File.Exists(asPath))
        {
            // System.Text.Json has a dep on System.Threading.Tasks.Extensions
            // and System.Runtime.CompilerServices.Unsafe. S.T.T.E has a ref on
            // S.R.CS.U 4.0.4.1 while S.T.J pulls in 4.0.6.0. This redirection
            // ensure that we can load them all under the newer version ref.
            return Assembly.LoadFile(asPath);
        }

        return null;
    }
}
#endif

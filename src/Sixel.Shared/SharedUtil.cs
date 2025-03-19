using System;
using System.Collections.Generic;
using System.Reflection;
#if NET5_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace Sixel.Shared;

internal class SharedUtil
{
  public static void AddAssemblyInfo(Type type, Dictionary<string, object> data)
  {
    Assembly asm = type.Assembly;

    data["Assembly"] = new Dictionary<string, object?>()
        {
            { "Name", asm.GetName().FullName },
#if NET5_0_OR_GREATER
            { "ALC", AssemblyLoadContext.GetLoadContext(asm)?.Name },
#endif
            { "Location", asm.Location }
        };
  }
}

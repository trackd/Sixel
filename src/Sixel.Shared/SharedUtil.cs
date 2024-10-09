using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Sixel.Shared;

internal class SharedUtil
{
  public static void AddAssemblyInfo(Type type, Dictionary<string, object> data)
  {
    Assembly asm = type.Assembly;

    data["Assembly"] = new Dictionary<string, object?>()
        {
            { "Name", asm.GetName().FullName },
            { "ALC", AssemblyLoadContext.GetLoadContext(asm)?.Name },
            { "Location", asm.Location }
        };
  }
}

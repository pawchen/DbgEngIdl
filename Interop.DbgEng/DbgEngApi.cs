using System.Runtime.InteropServices;

namespace Interop.DbgEng;

internal static partial class DbgEngApi
{
    public static Guid IDebugClientGuid { get; } = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");

    [LibraryImport("dbgeng.dll")]
    public static partial void DebugCreate(in Guid interfaceGuid, out IntPtr comObjPtr);
}

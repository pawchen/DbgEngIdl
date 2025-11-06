using System.Runtime.InteropServices;

namespace Interop.DbgEng;

internal static partial class DbgEngApi
{
    [LibraryImport("dbgeng.dll")]
    public static partial void DebugCreate(in Guid interfaceGuid, out IntPtr comObjPtr);


    [LibraryImport("dbgeng.dll")]
    public static partial void DebugCreateEx(in Guid interfaceGuid, out IntPtr comObjPtr);
}

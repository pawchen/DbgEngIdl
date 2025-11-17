using System.Runtime.InteropServices;

namespace Interop.DbgEng;

internal static partial class DbgEngLib
{
    [LibraryImport("dbgeng.dll")]
    public static partial HRESULT DebugCreate(in Guid interfaceGuid, out IntPtr comObjPtr);


    [LibraryImport("dbgeng.dll")]
    public static partial HRESULT DebugCreateEx(in Guid interfaceGuid, out IntPtr comObjPtr);
}

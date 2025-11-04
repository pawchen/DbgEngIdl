namespace Interop.DbgEng;

public partial interface IDebugClient
{
    static IDebugClient Create()
    {
        DbgEngApi.DebugCreate(DbgEngApi.IDebugClientGuid, out var pDebugClient);

        throw new NotImplementedException();
    }
}

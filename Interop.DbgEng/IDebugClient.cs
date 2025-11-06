namespace Interop.DbgEng;

public partial interface IDebugClient
{
    static IDebugClient Create()
    {
        DbgEngApi.DebugCreate(new Guid(Constants.IID_IDebugClient), out var pDebugClient);

        throw new NotImplementedException();
    }
}

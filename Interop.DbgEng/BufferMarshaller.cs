using System.Runtime.InteropServices.Marshalling;

namespace Interop.DbgEng;

[CustomMarshaller(typeof(ReadOnlySpan<byte>), MarshalMode.UnmanagedToManagedIn, typeof(BufferMarshaller<byte, byte>.DirectBytes))]
[ContiguousCollectionMarshaller]
static class BufferMarshaller<TManagedElement, TNativeElement> where TNativeElement : unmanaged
{
    public static class DirectBytes
    {
        public unsafe static ReadOnlySpan<byte> AllocateContainerForManagedElements(nint unmanaged, int numElements)
            => new ReadOnlySpan<byte>((void*)unmanaged, numElements);

        public static ReadOnlySpan<TNativeElement> GetUnmanagedValuesSource(nint unmanaged, int numElements)
            => [];

        public static Span<byte> GetManagedValuesDestination(ReadOnlySpan<byte> managed)
            => [];
    }
}

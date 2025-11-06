namespace SrcGenTests;

public class StructTests : TestsBase
{
    [Fact]
    public void TestIngore1()
    {
        AssertGenerated(
            "",
            hppSrc: """
            #ifndef _WDBGEXTS_
            typedef struct _WINDBG_EXTENSION_APIS32* PWINDBG_EXTENSION_APIS32;
            typedef struct _WINDBG_EXTENSION_APIS64* PWINDBG_EXTENSION_APIS64;
            #endif
            """,
            "");
    }

    [Fact]
    public void TestStruct1()
    {
        AssertGenerated("""
            public struct DebugOffsetRegion
            {
                public ULONG64 Base;
                public ULONG64 Size;
            }
            """,
            hppSrc: """
            typedef struct _DEBUG_OFFSET_REGION
            {
                ULONG64 Base;
                ULONG64 Size;
            } DEBUG_OFFSET_REGION, *PDEBUG_OFFSET_REGION;
            """,
            "");
    }

    [Fact]
    public void TestStruct2()
    {
        AssertGenerated("""
            public struct DebugOffsetRegion
            {
                public ULONG64 Base;
                public ULONG64 Size;
            }
            """,
            "",
            missingSrc: """
            typedef struct _DEBUG_OFFSET_REGION
            {
                ULONG64 Base;
                ULONG64 Size;
            } DEBUG_OFFSET_REGION, *PDEBUG_OFFSET_REGION;
            """);
    }

    [Fact]
    public void TestStruct3()
    {
        AssertGenerated("""
            public struct DebugOffsetRegion
            {
                public ULONG64 Base; // comment
                public ULONG64 Size;
            }
            """,
            "",
            missingSrc: """
            typedef struct _DEBUG_OFFSET_REGION
            {
                ULONG64 Base; // comment
                ULONG64 Size;
            } DEBUG_OFFSET_REGION, *PDEBUG_OFFSET_REGION;
            """);
    }

    [Fact]
    public void TestNested1()
    {
        AssertGenerated("""
            [StructLayout(LayoutKind.Explicit)]
            public struct InlineFrameContext
            {
                [FieldOffset(0)] public DWORD ContextValue;

                public struct NestedStruct1
                {
                    public BYTE FrameId;
                    public WORD FrameSignature;
                }

                [FieldOffset(0)] public NestedStruct1 NestedStruct1;

                public struct NestedStruct2
                {
                    public BYTE FrameType;
                    public WORD FrameSignature;
                }

                [FieldOffset(0)] public NestedStruct2 Named;

                [StructLayout(LayoutKind.Explicit)]
                public struct NestedUnion3
                {
                    [FieldOffset(0)] public BYTE FrameId;
                    [FieldOffset(0)] public WORD FrameSignature;
                }

                [FieldOffset(0)] public NestedUnion3 NestedUnion3;

                [StructLayout(LayoutKind.Explicit)]
                public struct NestedUnion4
                {
                    [FieldOffset(0)] public BYTE FrameType;
                    [FieldOffset(0)] public WORD FrameSignature;
                }

                [FieldOffset(0)] public NestedUnion4 Named1;
            }
            """,
            hppSrc: """
            typedef union _INLINE_FRAME_CONTEXT {
                DWORD ContextValue;
                struct {
                    BYTE FrameId;
                    WORD FrameSignature;
                };
                struct {
                    BYTE FrameType;
                    WORD FrameSignature;
                } Named;
                union {
                    BYTE FrameId;
                    WORD FrameSignature;
                };
                union {
                    BYTE FrameType;
                    WORD FrameSignature;
                } Named1;
            } INLINE_FRAME_CONTEXT;
            """,
            "");
    }

    [Fact]
    public void TestNested2()
    {
        AssertGenerated("""
            public struct DebugValue
            {

                [StructLayout(LayoutKind.Explicit)]
                public struct NestedUnion1
                {
                    [FieldOffset(0)] public UCHAR I8;

                    public struct NestedStruct1
                    {
                        // Extra NAT indicator for IA64
                        // integer registers.  NAT will
                        // always be false for other CPUs.
                        public ULONG64 I64;
                        public BOOL Nat;
                    }

                    [FieldOffset(0)] public NestedStruct1 NestedStruct1;

                    public struct NestedStruct2
                    {
                        public ULONG LowPart;
                        public ULONG HighPart;
                    }

                    [FieldOffset(0)] public NestedStruct2 I64Parts32;

                    public struct NestedStruct3
                    {
                        public ULONG64 LowPart;
                        public LONG64 HighPart;
                    }

                    [FieldOffset(0)] public NestedStruct3 F128Parts64;
                }

                public NestedUnion1 NestedUnion1;
                public ULONG TailOfRawBytes;
                public ULONG Type;
            }
            """,
            hppSrc: """
            typedef struct _DEBUG_VALUE
            {
                union
                {
                    UCHAR I8;
                    struct
                    {
                        // Extra NAT indicator for IA64
                        // integer registers.  NAT will
                        // always be false for other CPUs.
                        ULONG64 I64;
                        BOOL Nat;
                    };
                    struct
                    {
                        ULONG LowPart;
                        ULONG HighPart;
                    } I64Parts32;
                    struct
                    {
                        ULONG64 LowPart;
                        LONG64 HighPart;
                    } F128Parts64;
                };
                ULONG TailOfRawBytes;
              ULONG Type;
            } DEBUG_VALUE, *PDEBUG_VALUE;
            """,
            "");
    }

    [Fact]
    public void TestConstant1()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                public const UINT32 X = 0;
            }
            """,
            hppSrc: """
            #define X 0
            """,
            "");
    }

    [Fact]
    public void TestConstant2()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                public const UINT32 X = 0;
            }
            """,
            "",
            missingSrc: """
            #define X 0
            """);
    }

    [Fact]
    public void TestInlineArray1()
    {
        AssertGenerated("""
            public struct DebugOffsetRegion
            {
                public ArrayOf3<ULONG64> Base;
                public ULONG64 Size;
            }

            [InlineArray(3)]
            public struct ArrayOf3<T> { private T _item; }
            """,
            "",
            missingSrc: """
            typedef struct _DEBUG_OFFSET_REGION
            {
                ULONG64 Base[3];
                ULONG64 Size;
            } DEBUG_OFFSET_REGION, *PDEBUG_OFFSET_REGION;
            """);
    }

}

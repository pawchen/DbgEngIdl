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
    public void TestStruct4()
    {
        AssertGenerated("""
            public struct DebugOffsetRegion
            {
                [MarshalAs(UnmanagedType.LPWStr)]
                public string Base; // comment
            }
            """,
            hppSrc: """
            typedef struct _DEBUG_OFFSET_REGION
            {
                PCWSTR Base; // comment
            } DEBUG_OFFSET_REGION, *PDEBUG_OFFSET_REGION;
            """,
            "");
    }

    [Fact]
    public void TestStruct5()
    {
        AssertGenerated("""
            public struct DebugOffsetRegion
            {
                public IntPtr/*PVOID*/ Base; // comment
            }
            """,
            hppSrc: """
            typedef struct _DEBUG_OFFSET_REGION
            {
                PVOID Base; // comment
            } DEBUG_OFFSET_REGION, *PDEBUG_OFFSET_REGION;
            """,
            "");
    }

    [Fact]
    public void TestStructRemarks1()
    {
        AssertGenerated("""
            /// <remarks>
            /// haha
            /// </remarks>
            public struct DebugOffsetRegion
            {
                public ULONG64 Base;
                public ULONG64 Size;
            }
            """,
            hppSrc: """
            // haha
            typedef struct _DEBUG_OFFSET_REGION
            {
                ULONG64 Base;
                ULONG64 Size;
            } DEBUG_OFFSET_REGION, *PDEBUG_OFFSET_REGION;
            """,
            "");
    }

    [Fact]
    public void TestStructsRef1()
    {
        AssertGenerated("""
            public struct REF
            {
                public ULONG64 Size;
            }

            public struct B
            {
                public REF Base; // comment
            }
            """,
            "",
            missingSrc: """
            typedef struct _R_E_F
            {
                ULONG64 Size;
            } R_E_F, *PR_E_F;

            typedef struct _B
            {
                R_E_F Base; // comment
            } B, *PB;
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

                public struct _NestedStruct1
                {
                    public BYTE FrameId;
                    public WORD FrameSignature;
                }

                [FieldOffset(0)] public _NestedStruct1 NestedStruct1;

                public struct _NestedStruct2
                {
                    public BYTE FrameType;
                    public WORD FrameSignature;
                }

                [FieldOffset(0)] public _NestedStruct2 Named;

                [StructLayout(LayoutKind.Explicit)]
                public struct _NestedUnion3
                {
                    [FieldOffset(0)] public BYTE FrameId;
                    [FieldOffset(0)] public WORD FrameSignature;
                }

                [FieldOffset(0)] public _NestedUnion3 NestedUnion3;

                [StructLayout(LayoutKind.Explicit)]
                public struct _NestedUnion4
                {
                    [FieldOffset(0)] public BYTE FrameType;
                    [FieldOffset(0)] public WORD FrameSignature;
                }

                [FieldOffset(0)] public _NestedUnion4 Named1;
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
                public struct _NestedUnion1
                {
                    [FieldOffset(0)] public UCHAR I8;

                    public struct _NestedStruct1
                    {
                        // Extra NAT indicator for IA64
                        // integer registers.  NAT will
                        // always be false for other CPUs.
                        public ULONG64 I64;
                        public BOOL Nat;
                    }

                    [FieldOffset(0)] public _NestedStruct1 NestedStruct1;

                    public struct _NestedStruct2
                    {
                        public ULONG LowPart;
                        public ULONG HighPart;
                    }

                    [FieldOffset(0)] public _NestedStruct2 I64Parts32;

                    public struct _NestedStruct3
                    {
                        public ULONG64 LowPart;
                        public LONG64 HighPart;
                    }

                    [FieldOffset(0)] public _NestedStruct3 F128Parts64;
                }

                public _NestedUnion1 NestedUnion1;
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
    public void TestConstant3()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                public const UINT32 X = 0; // comment
            }
            """,
            hppSrc: """
            #define X 0 // comment
            """,
            "");
    }

    [Fact]
    public void TestConstant4()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                public const DWORD EXCEPTION_MAXIMUM_PARAMETERS = 15; // maximum number of exception parameters
            }
            """,
            hppSrc: """
            const DWORD EXCEPTION_MAXIMUM_PARAMETERS = 15; // maximum number of exception parameters
            """,
            "");
    }

    [Fact]
    public void TestConstant5()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                public const UINT64 X = 0x100000000; // comment
            }
            """,
            hppSrc: """
            #define X 0x100000000 // comment
            """,
            "");
    }

    [Fact]
    public void TestConstantRemarks1()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                /// <remarks>
                /// remarks
                /// </remarks>
                public const UINT64 X = 0x100000000; // comment
            }
            """,
            hppSrc: """
            // remarks
            #define X 0x100000000 // comment
            """,
            "");
    }

    [Fact]
    public void TestConstantRemarks2()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                /// <remarks>
                /// ///////
                /// <br />
                /// -------
                /// <br />
                /// <br />
                /// In
                /// <br />
                /// <br />
                /// Out
                /// <br />
                /// </remarks>
                public const UINT64 X = 0x100000000; // comment
            }
            """,
            hppSrc: """
            /////////
            //-------
            // In
            // Out
            #define X 0x100000000 // comment
            """,
            "");
    }

    [Fact]
    public void TestConstantRemarks3()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                /// <remarks>
                /// remarks &gt; 0
                /// </remarks>
                public const UINT64 X = 0x100000000; // comment
            }
            """,
            hppSrc: """
            // remarks > 0
            #define X 0x100000000 // comment
            """,
            "");
    }

    [Fact]
    public void TestConstantRemarks4()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                /// <remarks>
                /// remarks
                /// </remarks>
                public const UINT64 X = 0x100000000; // comment
                /// <remarks>
                /// remarks 2
                /// </remarks>
                public const UINT64 Y = 0x100000000; // comment
            }
            """,
            hppSrc: """
            // remarks
            #define X 0x100000000 // comment
            // remarks 2
            #define Y 0x100000000 // comment
            """,
            "");
    }

    [Fact]
    public void TestConstantRemarks5()
    {
        AssertGenerated("""
            public static partial class Constants
            {
                /// <remarks>
                /// remarks
                /// </remarks>
                public const UINT64 X = 0x100000000; // comment
                public const UINT64 Y = 0x100000000; // comment
            }
            """,
            hppSrc: """
            // remarks
            #define X 0x100000000 // comment
            #define Y 0x100000000 // comment
            """,
            "");
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

    [Fact]
    public void TestInlineArray2()
    {
        AssertGenerated("""
            public struct ExceptionRecord64
            {
                public ArrayOf15<LONGLONG> ExceptionInformation;
            }

            public static partial class Constants
            {
                public const DWORD EXCEPTION_MAXIMUM_PARAMETERS = 15; // maximum number of exception parameters
            }

            [InlineArray(15)]
            public struct ArrayOf15<T> { private T _item; }
            """,
            "",
            missingSrc: """
            const DWORD EXCEPTION_MAXIMUM_PARAMETERS = 15; // maximum number of exception parameters

            typedef struct _EXCEPTION_RECORD64 {
                LONGLONG ExceptionInformation[EXCEPTION_MAXIMUM_PARAMETERS];
            } EXCEPTION_RECORD64, * PEXCEPTION_RECORD64;
            """);
    }

}

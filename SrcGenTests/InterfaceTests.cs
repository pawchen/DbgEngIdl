namespace SrcGenTests;

public class InterfaceTests : TestsBase
{
    [Fact]
    public void TestEmptyInterface1()
    {
        AssertGenerated("""
            [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
            [Guid("f2df5f53-071f-47bd-9de6-5734c3fed689")]
            public partial interface ISomeInterface
            {
            }

            public static partial class Constants
            {
                public static ReadOnlySpan<byte> IID_ISomeInterface => [0x53, 0x5f, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0xd6, 0x89];
            }
            """,
                hppSrc: """
            typedef interface DECLSPEC_UUID("f2df5f53-071f-47bd-9de6-5734c3fed689")
                ISomeInterface* PSOME_INTERFACE;

            #undef INTERFACE
            #define INTERFACE ISomeInterface
            DECLARE_INTERFACE_(ISomeInterface, IUnknown)
            {
                // ISomeInterface.
            };
            """,
            "");
    }

    [Fact]
    public void TestEmptyCallbackInterface1()
    {
        AssertGenerated("""
            [GeneratedComInterface(Options = ComInterfaceOptions.ManagedObjectWrapper)]
            [Guid("f2df5f53-071f-47bd-9de6-5734c3fed689")]
            public partial interface ISomeCallback
            {
            }

            public static partial class Constants
            {
                public static ReadOnlySpan<byte> IID_ISomeCallback => [0x53, 0x5f, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0xd6, 0x89];
            }
            """,
                hppSrc: """
            typedef interface DECLSPEC_UUID("f2df5f53-071f-47bd-9de6-5734c3fed689")
                ISomeCallback* PSOME_CALLBACK;

            #undef INTERFACE
            #define INTERFACE ISomeCallback
            DECLARE_INTERFACE_(ISomeCallback, IUnknown)
            {
                // ISomeCallback.
            };
            """,
            "");
    }

    [Fact]
    public void TestEmptyInterface2()
    {
        AssertGenerated("""
            [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
            [Guid("f2df5f53-071f-47bd-9de6-5734c3fed689")]
            public partial interface ISomeInterface
            {
            }

            [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
            [Guid("f2df5f53-071f-47bd-9de6-5734c3fe6489")]
            public partial interface ISomeInterface2 : ISomeInterface
            {
            }
            
            public static partial class Constants
            {
                public static ReadOnlySpan<byte> IID_ISomeInterface => [0x53, 0x5f, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0xd6, 0x89];
                public static ReadOnlySpan<byte> IID_ISomeInterface2 => [0x53, 0x5f, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0x64, 0x89];
            }
            """,
                hppSrc: """
            typedef interface DECLSPEC_UUID("f2df5f53-071f-47bd-9de6-5734c3fed689")
                ISomeInterface* PSOME_INTERFACE;

            typedef interface DECLSPEC_UUID("f2df5f53-071f-47bd-9de6-5734c3fe6489")
                ISomeInterface2* PSOME_INTERFACE2;

            #undef INTERFACE
            #define INTERFACE ISomeInterface
            DECLARE_INTERFACE_(ISomeInterface, IUnknown)
            {
                // ISomeInterface.
            };

            #undef INTERFACE
            #define INTERFACE ISomeInterface2
            DECLARE_INTERFACE_(ISomeInterface2, IUnknown)
            {
                // ISomeInterface2.
            };
            """,
            "");
    }

    [Fact]
    public void TestEmptyInterface3()
    {
        AssertGenerated("""
            [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
            [Guid("f2df5453-071f-47bd-9de6-5734c3fe6489")]
            public partial interface ISomeInterface3 : ISomeInterface2
            {
            }
            
            [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
            [Guid("f2df5f53-071f-47bd-9de6-5734c3fed689")]
            public partial interface ISomeInterface
            {
            }

            [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
            [Guid("f2df5f53-071f-47bd-9de6-5734c3fe6489")]
            public partial interface ISomeInterface2 : ISomeInterface
            {
            }
            
            public static partial class Constants
            {
                public static ReadOnlySpan<byte> IID_ISomeInterface3 => [0x53, 0x54, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0x64, 0x89];
                public static ReadOnlySpan<byte> IID_ISomeInterface => [0x53, 0x5f, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0xd6, 0x89];
                public static ReadOnlySpan<byte> IID_ISomeInterface2 => [0x53, 0x5f, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0x64, 0x89];
            }
            """,
                hppSrc: """
            typedef interface DECLSPEC_UUID("f2df5453-071f-47bd-9de6-5734c3fe6489")
                ISomeInterface3* PSOME_INTERFACE3;
            
            typedef interface DECLSPEC_UUID("f2df5f53-071f-47bd-9de6-5734c3fed689")
                ISomeInterface* PSOME_INTERFACE;
            
            typedef interface DECLSPEC_UUID("f2df5f53-071f-47bd-9de6-5734c3fe6489")
                ISomeInterface2* PSOME_INTERFACE2;
            
            #undef INTERFACE
            #define INTERFACE ISomeInterface3
            DECLARE_INTERFACE_(ISomeInterface3, IUnknown)
            {
                // ISomeInterface3
            };
            
            #undef INTERFACE
            #define INTERFACE ISomeInterface
            DECLARE_INTERFACE_(ISomeInterface, IUnknown)
            {
                // ISomeInterface.
            };
            
            #undef INTERFACE
            #define INTERFACE ISomeInterface2
            DECLARE_INTERFACE_(ISomeInterface2, IUnknown)
            {
                // ISomeInterface2.
            };
            """,
            "");
    }

    [Fact]
    public void TestEmptyMethod1()
    {
        AssertGenerated("""
            [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
            [Guid("f2df5f53-071f-47bd-9de6-5734c3fed689")]
            public partial interface ISomeInterface
            {
                [PreserveSig]
                HRESULT Boom
                (
                );

            }

            public static partial class Constants
            {
                public static ReadOnlySpan<byte> IID_ISomeInterface => [0x53, 0x5f, 0xdf, 0xf2, 0x1f, 0x07, 0xbd, 0x47, 0x9d, 0xe6, 0x57, 0x34, 0xc3, 0xfe, 0xd6, 0x89];
            }
            """,
                hppSrc: """
            typedef interface DECLSPEC_UUID("f2df5f53-071f-47bd-9de6-5734c3fed689")
                ISomeInterface* PSOME_INTERFACE;

            #undef INTERFACE
            #define INTERFACE ISomeInterface
            DECLARE_INTERFACE_(ISomeInterface, IUnknown)
            {
                // ISomeInterface.
                STDMETHOD(Boom)(
                    THIS
                    ) PURE;
            };
            """,
            "");
    }

}

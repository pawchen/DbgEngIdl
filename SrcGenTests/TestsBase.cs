using System.Text;

namespace SrcGenTests;

public class TestsBase
{
    protected static void AssertGenerated(string expected, string hppSrc, string missingSrc)
    {
        var hpp = new StringReader(hppSrc);
        var missing = new StringReader(missingSrc);
        var sb = new StringBuilder();

        var program = new SrcGen.Program(new StringWriter(sb));
        program.Generate(hpp, missing);

        var result = sb.ToString();
        var resultLines = result.AsSpan().Trim().EnumerateLines();

        var commonHeader = """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            
            namespace Interop.DbgEng;
            

            """;
        var expectLines = (commonHeader + expected).AsSpan().Trim().EnumerateLines();

        while (expectLines.MoveNext())
        {
            Assert.True(resultLines.MoveNext());
            Assert.Equal(expectLines.Current.Trim(), resultLines.Current.Trim());
        }

        Assert.False(resultLines.MoveNext());
    }
}

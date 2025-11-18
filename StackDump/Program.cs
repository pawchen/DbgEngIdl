using System.Runtime.InteropServices;
using System.Text.Json;

namespace StackDump;

internal class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            return -2;
        }

        if (!File.Exists("dbgeng.dll") && Directory.Exists(Path.Combine("native", RuntimeInformation.RuntimeIdentifier)))
        {
            foreach (var item in Directory.EnumerateFiles(Path.Combine("native", RuntimeInformation.RuntimeIdentifier)))
            {
                File.Copy(item, Path.GetFileName(item));
            }
        }

        try
        {
            using var da = DumpAnalyzer.Create(dumpFile: args[0], imagePaths: args[1], symbolPaths: args[2]);
            using var stdOut = Console.OpenStandardOutput();

            var stack = da.GetExceptionStackTrace().ToArray();

            JsonSerializer.Serialize(stdOut, stack, DumpStackJsonSerializationContext.Default.DumpStackFrameArray);

            return 0;
        }
        catch (Exception e)
        {
            Console.Error.Write(e);

            return e is COMException com ? com.ErrorCode : -4;
        }
    }
}
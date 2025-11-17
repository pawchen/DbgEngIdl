using System.Runtime.InteropServices;
using System.Text;
using Interop.DbgEng;

namespace StackDump;

sealed class DumpAnalyzer : IDisposable
{
    private bool IsDisposed;
    private IDebugClient Client;

    private DumpAnalyzer(IDebugClient client)
    {
        Client = client;
    }

    public static DumpAnalyzer Create(string dumpFile, string imagePaths, string symbolPaths)
    {
        var root = IDebugClient.Create();

        root.OpenDumpFile(dumpFile);
        ((IDebugControl)root).WaitForEvent(0, 0);

        var symbols = (IDebugSymbols)root;
        symbols.SetImagePath(imagePaths);
        symbols.SetSymbolPath(symbolPaths);

        return new DumpAnalyzer(root);
    }

    const int E_Fail = unchecked((int)0x80004005);
    const int E_NoInterface = unchecked((int)0x80004002);

    public List<DumpStackFrame> GetExceptionStackTrace()
    {
        var symbols = (IDebugSymbols)Client;
        var control = (IDebugControl4)Client;

        byte[] context, extraInfo;
        var hr = control.GetStoredEventInformation(out _, out _, out _
                                                  , null, 0, out var contextSize
                                                  , null, 0, out var extraInfoSize
                                                  );

        if (hr != 0)
        {
            throw new COMException(nameof(control.GetStoredEventInformation), hr);
        }

        context = new byte[contextSize];
        extraInfo = new byte[extraInfoSize];
        hr = control.GetStoredEventInformation(out _, out _, out _
                                              , context, contextSize, out _
                                              , extraInfo, extraInfoSize, out _
                                              );

        if (hr != 0)
        {
            throw new COMException(nameof(control.GetStoredEventInformation), hr);
        }

        uint maxFrames = 150;
        byte[] frameContexts = new byte[maxFrames * contextSize];
        DebugStackFrame[] stackFrames = new DebugStackFrame[maxFrames];
        hr = control.GetContextStackTrace(context, contextSize
                                         , stackFrames, maxFrames
                                         , frameContexts, (uint)frameContexts.Length, contextSize
                                         , out var frames);

        if (hr != 0)
        {
            throw new COMException(nameof(control.GetContextStackTrace), hr);
        }

        const int nameSpanSize = 512;

        Span<byte> imageNameSpan = stackalloc byte[nameSpanSize];
        Span<byte> moduleNameSpan = stackalloc byte[nameSpanSize];
        Span<byte> loadedImageNameSpan = stackalloc byte[nameSpanSize];
        Span<byte> symbolNameSpan = stackalloc byte[nameSpanSize];

        var stackTrace = new List<DumpStackFrame>((int)frames);

        for (int f = 0; f < frames; f++)
        {
            var frame = new DumpStackFrame();
            var pc = frame.InstructionAddress = stackFrames[f].InstructionOffset;

            hr = symbols.GetModuleByOffset(pc, 0, out var moduleIndex, out var moduleBase);

            if (hr != 0)
            {
                stackTrace.Add(frame);
                continue;
            }

            frame.ModuleBaseAddress = moduleBase;

            var unknownModule = $"<unknown_{moduleBase}>";

            hr = symbols.GetModuleNames(moduleIndex, moduleBase
                                       , imageNameSpan, nameSpanSize, out var imageNameSize
                                       , moduleNameSpan, nameSpanSize, out var moduleNameSize
                                       , loadedImageNameSpan, nameSpanSize, out var loadedImageNameSize
                                       );

            string imageName, moduleName, loadedImageName;

            if (hr != 0)
            {
                imageName = moduleName = loadedImageName = unknownModule;
            }
            else
            {
                imageName = Encoding.ASCII.GetString(imageNameSpan[..(int)imageNameSize]);
                moduleName = Encoding.ASCII.GetString(moduleNameSpan[..(int)moduleNameSize]);
                loadedImageName = Encoding.ASCII.GetString(loadedImageNameSpan[..(int)loadedImageNameSize]);
            }

            frame.ModuleName = loadedImageName;
            if (String.IsNullOrWhiteSpace(frame.ModuleName))
            {
                frame.ModuleName = unknownModule;
            }

            hr = symbols.GetNameByOffset(pc, symbolNameSpan, nameSpanSize, out var nameSize, out _);

            if (hr != 0)
            {
                stackTrace.Add(frame);
                continue;
            }

            var symbolName = Encoding.ASCII.GetString(symbolNameSpan[..(int)nameSize]);
            frame.SymbolName = symbolName.Contains('!') ? symbolName[(symbolName.IndexOf('!') + 1)..] : "<unknown>";
            stackTrace.Add(frame);
        }

        return stackTrace;
    }

    private void Destroy()
    {
        if (IsDisposed)
        {
            return;
        }

        if (Client is not null)
        {
            Client.EndSession(0);
            Client = null!;
        }

        IsDisposed = true;
    }

    ~DumpAnalyzer()
    {
        Destroy();
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }
}

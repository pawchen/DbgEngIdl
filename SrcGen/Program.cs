using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SrcGen
{
    public sealed partial class Program
    {
        private const string ExplicitLayoutAttribute = "[StructLayout(LayoutKind.Explicit)]";
        private const string FieldOffsetAttribute = "[FieldOffset(0)] ";

        public const string GeneratedHeader = """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using System.Runtime.InteropServices.Marshalling;
            
            namespace Interop.DbgEng;
            
            """;

        readonly TextWriter Output;
        readonly Dictionary<string, string> UUIDs = [];
        readonly Dictionary<string, (string generatedName, bool isValueType)> Types = [];
        readonly Dictionary<string, (string type, string value, string comment)> Constants = [];
        readonly HashSet<int> InlineArrays = [];

        bool TryGetGeneratedType(ReadOnlySpan<char> native, out (string generatedName, bool isValueType) managed)
            => Types.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(native, out managed);

        public Program(TextWriter output)
        {
            Output = output;
        }

        public static void Main(string[] args)
        {
            var dbgEngHeaderFileName = "dbgeng.h";
            var missingHeaderFileName = "missing.h";
            var generatedFileName = "DbgEng.g.cs";

            if (args.Length > 0)
            {
                dbgEngHeaderFileName = args[0];
            }

            if (args.Length > 1)
            {
                missingHeaderFileName = args[1];
            }

            if (args.Length > 2)
            {
                generatedFileName = args[2];
            }

            Console.WriteLine($"SrcGen is running at {Environment.CurrentDirectory}");

            using var hpp = File.OpenText(dbgEngHeaderFileName);
            using var missing = File.Exists(missingHeaderFileName) ? File.OpenText(missingHeaderFileName) : StreamReader.Null;
            using var output = new StreamWriter(new FileStream(generatedFileName, FileMode.Create));

            var program = new Program(output);
            program.Generate(hpp, missing);
        }

        public void Generate(TextReader hpp, TextReader missing)
        {
            Output.WriteLine(GeneratedHeader);

            WriteDefinitions(missing);
            WriteDefinitions(hpp);

            WriteConstants();
            WriteIIDs();
            WriteInlineArrays();
        }

        private void WriteDefinitions(TextReader hpp)
        {
            const string DECLSPEC_UUID = "typedef interface DECLSPEC_UUID(\"";

            while (hpp.Peek() > -1)
            {
                var line = hpp.ReadLine();

                if (line.StartsWith("#define ") || line.StartsWith("const "))
                {
                    TryCollectConstant(line);
                }
                else if (line.StartsWith(DECLSPEC_UUID))
                {
                    var guid = line.Substring(DECLSPEC_UUID.Length, "f2df5f53-071f-47bd-9de6-5734c3fed689".Length);
                    var typedef = hpp.ReadLine().AsSpan().Trim();
                    var star = typedef.IndexOf('*');
                    var name = typedef[..star].ToString();
                    var pointerType = typedef[(star + 1)..typedef.IndexOf(';')].Trim();

                    UUIDs.Add(name, guid);
                    Types.Add(pointerType.ToString(), (name, isValueType: false));
                }
                else if (line.StartsWith("typedef struct _") || line.StartsWith("typedef union _"))
                {
                    WriteStruct(hpp, line);
                }
                else if (line.StartsWith("DECLARE_INTERFACE_"))
                {
                    WriteInterface(hpp, line);
                }
            }
        }

        private bool TryCollectConstant(string line)
        {
            if (line[0] == '#')
            {
                Span<Range> parts = stackalloc Range[2];
                var define = line.AsSpan("#define ".Length);
                var count = define.Split(parts, ' ', StringSplitOptions.RemoveEmptyEntries);

                if (count < 2 || !Char.IsDigit(define[parts[1]][0]))
                {
                    return false;
                }

                var name = define[parts[0]].ToString();
                var value = define[parts[1]];
                var comment = GetComment(ref value);
                var type = (value.StartsWith("0x") && value.Trim().Length > 10) ? "UINT64" : "UINT32";

                Constants[name] = (type, value.ToString(), comment);

                return true;
            }
            else
            {
                Span<Range> parts = stackalloc Range[3];
                var expression = line.AsSpan("const ".Length);
                var count = expression.SplitAny(parts, " =", StringSplitOptions.RemoveEmptyEntries);

                if (count < 3 || !Char.IsDigit(expression[parts[2]][0]))
                {
                    return false;
                }

                var type = expression[parts[0]].ToString();
                var name = expression[parts[1]].ToString();
                var value = expression[parts[2]];
                var comment = GetComment(ref value);

                value = value[..value.IndexOf(';')];

                Constants[name] = (type, value.ToString(), comment);

                return true;
            }

            static string GetComment(ref ReadOnlySpan<char> value)
            {
                string comment = null;
                var slash = value.IndexOf("//");
                if (slash > -1)
                {
                    comment = value[(slash + 2)..].ToString();
                    value = value[..slash];
                }

                return comment;
            }
        }

        private void WriteConstants()
        {
            if (Constants.Count < 1)
            {
                return;
            }

            Output.WriteLine("public static partial class Constants");
            Output.WriteLine("{");

            foreach (var (name, def) in Constants)
            {
                if (def.comment is null)
                {
                    Output.WriteLine($"    public const {def.type} {name} = {def.value};");
                }
                else
                {
                    Output.WriteLine($"    public const {def.type} {name} = {def.value.Trim()}; //{def.comment}");
                }
            }

            Output.WriteLine("}");
            Output.WriteLine();
        }

        private void WriteIIDs()
        {
            if (UUIDs.Count < 1)
            {
                return;
            }

            Output.WriteLine("public static partial class Constants");
            Output.WriteLine("{");

            Span<byte> bytes = stackalloc byte[16];
            var formatted = new StringBuilder(", 0x00".Length * bytes.Length);

            foreach (var (name, iid) in UUIDs)
            {
                Guid.Parse(iid).TryWriteBytes(bytes);

                formatted.Length = 0;
                formatted.Append($"0x{bytes[0]:x2}");
                for (int i = 1; i < bytes.Length; i++)
                {
                    formatted.Append($", 0x{bytes[i]:x2}");
                }

                Output.WriteLine($"    public static ReadOnlySpan<byte> IID_{name} => [{formatted}];");
            }

            Output.WriteLine("}");
            Output.WriteLine();
        }

        private void WriteStruct(TextReader hpp, string fullLine)
        {
            var line = fullLine.AsSpan().Trim();
            var isUnion = line["typedef ".Length] == 'u';

            var structName = line[(isUnion ? "typedef union _" : "typedef struct _").Length..];

            if (structName.ContainsAny("*;"))
            {
                return;
            }
            else if (structName.EndsWith('{'))
            {
                structName = structName[..structName.IndexOf(' ')];
            }
            else
            {
                hpp.ReadLine();
            }

            if (isUnion)
            {
                Output.WriteLine(ExplicitLayoutAttribute);
            }

            var generatedStructName = SnakeToCamel(structName);
            Types[$"{structName}"] = (generatedStructName, isValueType: true);
            Types[$"P{structName}"] = (generatedStructName, isValueType: true);

            Output.WriteLine($"public struct {generatedStructName}");
            Output.WriteLine("{");

            WriteStructBody(hpp, 0, isUnion);

            Output.WriteLine("}");
            Output.WriteLine();
        }

        private string WriteStructBody(TextReader hpp, int level, bool isUnion)
        {
            var nestedStructs = 0;

            while (hpp.Peek() > -1)
            {
                var fullLine = hpp.ReadLine();
                var line = fullLine.AsSpan().Trim();

                if (line.IsEmpty)
                {
                    Output.WriteLine();
                }
                else if (line.StartsWith("//"))
                {
                    Output.WriteLine(fullLine);
                }
                else if (line.StartsWith("struct") || line.StartsWith("union"))
                {
                    WriteNestedStruct(hpp, fullLine, level + 1, ++nestedStructs, isUnion);
                }
                else if (line.StartsWith('}'))
                {
                    return fullLine;
                }
                else
                {
                    var space = line.IndexOf(' ');
                    var type = line[..space];

                    if (type.SequenceEqual("IN") || type.SequenceEqual("OUT"))
                    {
                        Output.WriteLine($"    // {type}");

                        space += line[(space + 1)..].IndexOf(' ') + 1;
                        type = line[(type.Length + 1)..space];
                    }

                    if (TryGetGeneratedType(type, out var managed))
                    {
                        type = managed.generatedName;
                    }
                    else if (type.EndsWith("PCWSTR"))
                    {
                        Output.WriteLine("    [MarshalAs(UnmanagedType.LPWStr)]");
                        type = "string";
                    }
                    else if (type.StartsWith('P'))
                    {
                        type = $"IntPtr/*{type}*/";
                    }

                    var memberName = line[(space + 1)..];
                    var bracket = memberName.IndexOf('[');

                    if (bracket > 0)
                    {
                        var length = memberName[(bracket + 1)..memberName.IndexOf(']')];
                        memberName = memberName[..bracket];

                        if (!Int32.TryParse(length, out var nLength))
                        {
                            nLength = Int32.Parse(Constants.GetAlternateLookup<ReadOnlySpan<char>>()[length].value);
                        }

                        InlineArrays.Add(nLength);

                        type = $"ArrayOf{nLength}<{type}>";
                    }
                    else
                    {
                        memberName = memberName[..memberName.IndexOf(';')];
                    }

                    WriteIndent(level + 1);

                    if (isUnion)
                    {
                        Output.Write(FieldOffsetAttribute);
                    }

                    Output.Write($"public {type} {memberName};");

                    var semicoln = line.IndexOf(';');
                    if (semicoln + 1 < line.Length)
                    {
                        Output.Write(line[(semicoln + 1)..]);
                    }

                    Output.WriteLine();
                }
            }

            return null;
        }

        private void WriteNestedStruct(TextReader hpp, string fullLine, int level, int index, bool insideUnion)
        {
            Output.WriteLine();

            var line = fullLine.AsSpan().Trim();
            var isUnion = line[0] == 'u';

            ReadOnlySpan<char> structName;

            if (isUnion)
            {
                WriteIndent(level);
                Output.WriteLine(ExplicitLayoutAttribute);

                structName = $"NestedUnion{index}";
            }
            else
            {
                structName = $"NestedStruct{index}";
            }

            if (!fullLine.Contains('{'))
            {
                hpp.ReadLine();
            }

            WriteIndent(level);
            Output.WriteLine($"public struct _{structName}");

            WriteIndent(level);
            Output.WriteLine('{');

            fullLine = WriteStructBody(hpp, level, isUnion);
            line = fullLine.AsSpan().Trim();

            WriteIndent(level);
            Output.WriteLine('}');

            WriteIndent(level);
            Output.WriteLine();

            var memberName = structName;

            if (!line.StartsWith("};"))
            {
                memberName = line[(line.IndexOf(' ') + 1)..line.IndexOf(';')];
            }

            WriteIndent(level);

            if (insideUnion)
            {
                Output.Write(FieldOffsetAttribute);
            }

            Output.WriteLine($"public _{structName} {memberName};");
        }

        private void WriteIndent(int level)
        {
            for (int i = 0; i < level; i++)
            {
                Output.Write("    ");
            }
        }

        private void WriteInlineArrays()
        {
            if (InlineArrays.Count < 1)
            {
                return;
            }

            foreach (var length in InlineArrays)
            {
                Output.WriteLine($$"""
                    [InlineArray({{length}})]
                    public struct ArrayOf{{length}}<T> { private T _item; }
                    """);
                Output.WriteLine();
            }
        }

        private static string SeekToLine(TextReader hpp, string prefix, bool ignoreLeadingSpaces)
        {
            while (hpp.Peek() > -1)
            {
                var line = hpp.ReadLine();
                var span = line.AsSpan();

                if (ignoreLeadingSpaces)
                {
                    span = span.TrimStart();
                }

                if (span.StartsWith(prefix))
                {
                    return line;
                }
            }

            return null;
        }

        private void WriteInterface(TextReader hpp, string fullLine)
        {
            // See https://devblogs.microsoft.com/oldnewthing/20041005-00/?p=37653
            // What are the rules?
            //  * ...
            //    Note: In practice, you will never find the plain DECLARE_INTERFACE macro because all interfaces derive from IUnknown if nothing else.
            //  * You must list all the methods of the base interfaces in exactly the same order that they are listed by that base interface;
            //    the methods that you are adding in the new interface must go last.
            //  * You must use the STDMETHOD or STDMETHOD_ macros to declare the methods.
            //    Use STDMETHOD if the return value is HRESULT and STDMETHOD_ if the return value is some other type.
            //  * If your method has no parameters, then the argument list must be (THIS).
            //    Otherwise, you must insert THIS_ immediately after the open-parenthesis of the parameter list.
            //  * After the parameter list and before the semicolon, you must say PURE.

            var line = fullLine.AsSpan();
            var interfaceName = line["DECLARE_INTERFACE_(".Length..line.IndexOf(',')];
            var wrapperType = interfaceName.Contains("Callback", StringComparison.Ordinal) ? "ManagedObjectWrapper" : "ComObjectWrapper";

            Output.Write($$"""
                [GeneratedComInterface(Options = ComInterfaceOptions.{{wrapperType}})]
                [Guid("{{UUIDs.GetAlternateLookup<ReadOnlySpan<char>>()[interfaceName]}}")]
                public partial interface {{interfaceName}}
                """);

            if (Char.IsDigit(interfaceName[^1]))
            {
                var n = interfaceName.IndexOfAnyInRange('0', '9');
                var family = interfaceName[..n];
                var generation = interfaceName[n..];
                var prevGen = Int32.Parse(generation) - 1;

                if (prevGen == 1)
                {
                    Output.Write($" : {family}");
                }
                else
                {
                    Output.Write($" : {family}{prevGen}");
                }
            }

            Output.WriteLine();
            Output.WriteLine("{");

            SeekToLine(hpp, $"// {interfaceName}", true);

            ReadOnlySpan<char> methodName = default;

            while (hpp.Peek() > -1)
            {
                fullLine = hpp.ReadLine();
                line = fullLine.AsSpan().Trim();

                if (methodName.IsEmpty)
                {
                    if (line.StartsWith("STDMETHOD"))
                    {
                        var L = line.IndexOf('(') + 1;
                        var R = line.IndexOf(')');

                        ReadOnlySpan<char> returnType;

                        if (line["STDMETHOD".Length] == '_')
                        {
                            var comma = line.IndexOf(',');

                            returnType = line[L..comma];
                            methodName = line[(comma + ", ".Length)..R];
                        }
                        else
                        {
                            returnType = "HRESULT";
                            methodName = line[L..R];
                        }

                        Output.WriteLine($"""
                                [PreserveSig]
                                {returnType} {methodName}
                                (
                            """);
                    }
                    else if (fullLine.StartsWith("};"))
                    {
                        break;
                    }
                }
                else
                {
                    if (line.StartsWith('_'))
                    {
                        if (line.Contains(" _Reserved_ ", StringComparison.Ordinal))
                        {
                            // reserved pointer must be 0

                            WriteIndent(2);
                            Output.Write("IntPtr Reserved");

                            if (!line.Contains(','))
                            {
                                Output.Write(" = 0");
                            }
                            else
                            {
                                Output.Write(',');
                            }

                            Output.WriteLine();

                            continue;
                        }

                        /*
                         * See https://learn.microsoft.com/en-us/cpp/code-quality/annotating-function-parameters-and-return-values?view=msvc-170
                         * Currently used annotations are listed below.
                         * Please note that 'n' means the capacity of the array, 'x' means the number of elements valid in post-state.
                         * For input parameters, all n elements must be valid in pre-state.
                         * For output parameters, those n elements don't have to be valid in pre-state.
                         * 
                         * _In_: opt, reads(n), reads_opt(n), reads_bytes(n), reads_bytes_opt(n)
                         * _Out_: opt, writes(n), writes_opt(n), writes_bytes(n), writes_bytes_opt(n), writes_to(n,x), writes_to_opt(n,x)
                         * _Outptr_: result_buffer(n)
                         * _Inout_: opt
                         */

                        var annotation = line[..line.IndexOfAny(" (")];
                        var annotationLength = annotation.Length;
                        var isReadOnly = annotation.StartsWith("_In_");
                        var mayBeDefault = annotation.EndsWith("_opt_");

                        ReadOnlySpan<char> spanSizeExpr = default, writtenSizeExpr = default;
                        if (annotation.ContainsAny("rw"))
                        {
                            Debug.Assert(line.ContainsAny("(,)"));

                            spanSizeExpr = line[(annotation.Length + "(".Length)..line.IndexOfAny(",)")];
                            annotationLength += spanSizeExpr.Length + "()".Length;

                            if (annotation.Contains("_to_", StringComparison.Ordinal))
                            {
                                Debug.Assert(line.Contains(','));

                                writtenSizeExpr = line[(line.IndexOf(',') + ",".Length)..line.IndexOf(')')];
                                annotationLength += writtenSizeExpr.Length + ")".Length;
                            }
                        }

                        WriteIndent(2);
                        Output.WriteLine($"// {line[..annotationLength]}");

                        line = line[annotationLength..].TrimStart();

                        if (line.StartsWith("/*"))
                        {
                            var afterComment = "/*".Length + line["/*".Length..].IndexOf("*/", StringComparison.Ordinal) + "*/".Length;

                            line = line[afterComment..].TrimStart();
                        }

                        var nativeType = line[..line.IndexOfAny(" *")];

                        Debug.Assert(!nativeType.IsEmpty);

                        var pointerIndirections = 0;
                        if (nativeType[0] == 'P' || nativeType.StartsWith("LP"))
                        {
                            pointerIndirections++;
                        }
                        if (line[nativeType.Length..].Contains('*'))
                        {
                            pointerIndirections++;
                        }

                        string generatedName;
                        var isValueType = true;

                        if (TryGetGeneratedType(nativeType, out var managed))
                        {
                            generatedName = managed.generatedName;
                            isValueType = managed.isValueType;
                        }
                        else
                        {
                            generatedName = (nativeType[0] == 'P' ? nativeType[1..] : nativeType.StartsWith("LP") ? nativeType[2..] : nativeType).ToString();

                            if (nativeType.EndsWith("STR"))
                            {
                                isValueType = false;
                            }
                        }

                        if (pointerIndirections == 1)
                        {
                            if (spanSizeExpr.IsEmpty)
                            {
                                if (isValueType)
                                {
                                    generatedName = isReadOnly ? $"in {generatedName}" : $"out {generatedName}";
                                }
                                else if (nativeType.EndsWith("STR"))
                                {
                                    switch (nativeType)
                                    {
                                        case "PSTR" when isReadOnly:
                                        case "PCSTR":
                                            WriteIndent(2);
                                            Output.WriteLine("[MarshalAs(UnmanagedType.LPStr)]");
                                            generatedName = "string";
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                if (generatedName == "VOID")
                                {
                                    generatedName = "byte";
                                }

                                generatedName = isReadOnly ? $"ReadOnlySpan<{generatedName}>" : $"Span<{generatedName}>";
                            }
                        }

                        var nameAndRest = line[nativeType.Length..].TrimStart();
                        if (nameAndRest[0] == '*')
                        {
                            nameAndRest = nameAndRest[1..];
                        }

                        WriteIndent(2);
                        Output.WriteLine($"{generatedName} {nameAndRest}");
                    }
                    else if (line.StartsWith('.'))
                    {
                        //output.AppendLine("        [optional] SAFEARRAY(VARIANT)");
                    }
                    else if (line.StartsWith(") PURE;"))
                    {
                        methodName = "";

                        Output.WriteLine("    );");
                        Output.WriteLine();
                    }
                }
            }

            Output.WriteLine("}");
            Output.WriteLine();
        }

        private static string SnakeToCamel(ReadOnlySpan<char> snake)
        {
            var result = new StringBuilder(snake.Length);

            Span<char> lower = snake.Length <= 64 ? stackalloc char[snake.Length] : new char[snake.Length];
            snake.ToLowerInvariant(lower);

            foreach (var range in snake.Split('_'))
            {
                var part = lower[range];

                if (part.Length > 0)
                {
                    result.Append(Char.ToUpperInvariant(part[0]));

                    if (part.Length > 1)
                    {
                        result.Append(part[1..]);
                    }
                }
            }

            return result.ToString();
        }

        [GeneratedRegex(@" *?/\*.*?\*/ *")]
        private static partial Regex InlineCommentRegex { get; }
    }
}

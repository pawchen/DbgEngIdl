using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SrcGen
{
    public sealed class Program
    {
        private const string ExplicitLayoutAttribute = "[StructLayout(LayoutKind.Explicit)]";
        private const string FieldOffsetAttribute = "[FieldOffset(0)] ";

        readonly TextWriter Output;
        readonly Dictionary<string, string> UUIDs = [];
        readonly Dictionary<string, string> Types = [];
        readonly Dictionary<string, (string type, string value, string comment)> Constants = [];
        readonly HashSet<int> InlineArrays = [];

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
            Output.WriteLine("""
                using System.Runtime.CompilerServices;
                using System.Runtime.InteropServices;

                namespace Interop.DbgEng;

                """);

            WriteDefinitions(missing);
            WriteDefinitions(hpp);

            WriteConstants();
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
                    var name = typedef[..typedef.IndexOf('*')].ToString();

                    UUIDs.Add(name, guid);
                }
                else if (line.StartsWith("typedef struct _") || line.StartsWith("typedef union _"))
                {
                    WriteStruct(hpp, line);
                }
                else if (line.StartsWith("DECLARE_INTERFACE_"))
                {
                    //WriteInterface(hpp, uuids, line);
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
                string comment = GetComment(ref value);

                Constants[name] = ("UINT32", value.ToString(), comment);

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
            Types.GetAlternateLookup<ReadOnlySpan<char>>()[structName] = generatedStructName;

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
                        space += line[(space + 1)..].IndexOf(' ') + 1;
                        type = line[(type.Length + 1)..space];
                    }

                    if (Types.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(type, out var generatedType))
                    {
                        type = generatedType;
                    }
                    else if (type.EndsWith("STR"))
                    {
                        Output.WriteLine("    [string]");
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

        //private static int WriteInterface(TextWriter output, TextReader hpp, Dictionary<string, string> uuids, string line)
        //{
        //    var signature = "DECLARE_INTERFACE_(";
        //    var name = line.Substring(signature.Length, line.IndexOf(',') - signature.Length);
        //    var super = line.Substring(line.IndexOf(',') + 1);
        //    super = super.Substring(0, super.Length - 1).Trim();

        //    output.AppendLine("[")
        //          .AppendLine("    object,")
        //          .AppendLine("    uuid(" + uuids[name] + "),")
        //          .AppendLine("    helpstring(\"" + name + "\")")
        //          .AppendLine("]")
        //          .AppendLine($"interface {name} : {super} ")
        //          .AppendLine("{")
        //          ;

        //    var methodStart = "STDMETHOD";
        //    bool inMethod = false, paramWasOptional = false;

        //    while (!hpp.EndOfStream)
        //    {
        //        if (!inMethod && line.StartsWith(methodStart))
        //        {
        //            var L = line.IndexOf('(') + 1;
        //            var R = line.IndexOf(')');
        //            var methodName = line.Substring(L, R - L);
        //            if (methodName == "QueryInterface")
        //            {
        //                i += 10;
        //            }
        //            else
        //            {
        //                output.Append("    HRESULT ").Append(methodName).AppendLine("(");
        //                inMethod = true;
        //                paramWasOptional = false;
        //            }
        //        }
        //        else if (inMethod && line.StartsWith("_"))
        //        {
        //            line = Regex.Replace(line, @" *?/\*.*?\*/ *", " ");
        //            var parts = line.Split(' ');
        //            if (parts[1] == "_Reserved_")
        //            {
        //                parts[1] = parts[2];
        //                parts[2] = parts[3];
        //            }

        //            var cppAttr = parts[0];
        //            var type = parts[1];
        //            var param = parts[2];

        //            bool isArray;
        //            output.Append("        ")
        //                  .Append(ToIdlAttr(cppAttr, ref paramWasOptional, type, out isArray)).Append(' ');

        //            if (isArray)
        //            {
        //                if (type == "PVOID")
        //                {
        //                    type = "byte";
        //                }
        //                if (type.StartsWith("P"))
        //                {
        //                    type = type.Substring(1);
        //                }
        //                if (param.EndsWith(","))
        //                {
        //                    param = param.Replace(",", "[],");
        //                }
        //                else
        //                {
        //                    param += "[]";
        //                }
        //            }

        //            output.Append(type).Append(' ').AppendLine(param);
        //        }
        //        else if (inMethod && line.StartsWith("."))
        //        {
        //            output.AppendLine("        [optional] SAFEARRAY(VARIANT)");
        //        }
        //        else if (inMethod && line.StartsWith(")"))
        //        {
        //            output.AppendLine("    );");
        //            inMethod = paramWasOptional = false;
        //        }
        //    }

        //    output.AppendLine("};").AppendLine();
        //}

        private static string ToIdlAttr(string cppAttr, ref bool wasOptional, string type, out bool isArray)
        {
            // http://msdn.microsoft.com/en-us/library/hh916382.aspx

            var result = new StringBuilder("[");

            if (cppAttr.StartsWith("_In_"))
            {
                result.Append("in");
            }
            else if (cppAttr.StartsWith("_Out_"))
            {
                result.Append("out");
            }
            else
            {
                result.Append("in,out");
            }

            if (cppAttr.Contains("_opt_") || wasOptional)
            {
                result.Append(",optional");
                wasOptional = true;
            }

            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa366731(v=vs.85).aspx

            isArray = false;
            if (type.EndsWith("STR"))
            {
                result.Append(",string");
            }
            else
            {
                var lp = cppAttr.IndexOf('(');
                if (lp > 0)
                {
                    var param = cppAttr.Substring(lp + 1, cppAttr.Length - lp - 2);
                    if (cppAttr.Contains("_to_"))
                    {
                        param = param.Split(',')[0];
                    }
                    if (!cppAttr.Contains("_bytes_"))
                    {
                        if (type.StartsWith("P"))
                        {
                            type = type.Substring(1);
                        }
                        param = $"{param} * sizeof({type})";
                    }

                    isArray = true;
                    result.Append($",size_is({param})");
                }
            }

            return result.Append(']').ToString();
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
    }
}

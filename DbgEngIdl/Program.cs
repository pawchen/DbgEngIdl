using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DbgEngIdl
{
    public class Program
    {
        public static void Main( string[] args )
        {
            var header = File.ReadAllText("header.idl");
            var footer = File.ReadAllText("footer.idl");
            var hpp = File.ReadAllLines("dbgeng.h");

            File.WriteAllText("DbgEng.idl", GenerateIdl(header, footer, hpp));
        }

        private static string GenerateIdl( string header, string footer, string[] hpp )
        {
            var output = new StringBuilder(512000);

            output.Append(header);

            var uuids = ExtractForwardDefinitions(output, hpp, out var lineNumber);

            ExtractDefinitions(output, uuids, hpp, lineNumber);

            output.Append(footer);
            return output.ToString();
        }

        private static Dictionary<string, string> ExtractForwardDefinitions(StringBuilder output, string[] hpp, out int i)
        {
            output.AppendLine("//// Interface forward definitions");

            var guids = new Dictionary<string, string>();
            var signature = "typedef interface DECLSPEC_UUID(\"";

            var found = false;
            for ( i = 0; i < hpp.Length; i++ )
            {
                var line = hpp[i];
                if ( line.StartsWith(signature) )
                {
                    found = true;
                    var guid = line.Substring(signature.Length, "f2df5f53-071f-47bd-9de6-5734c3fed689".Length);
                    var typedef = hpp[i + 1].Trim();
                    var name = typedef.Substring(0, typedef.IndexOf('*'));

                    guids.Add(name, guid);

                    output.AppendLine();
                    output.AppendLine("interface " + name + ";");
                    output.AppendLine("typedef " + typedef);

                    i++;
                }

                if ( found && line.StartsWith("//--") )
                {
                    break;
                }
            }

            output.AppendLine();

            return guids;
        }

        private static void ExtractDefinitions( StringBuilder output, Dictionary<string, string> uuids, string[] hpp, int i )
        {
            var constants = new List<(string name, string value)>();

            for ( ; i < hpp.Length; i++ )
            {
                var line = hpp[i];
                if ( line.StartsWith("#define ") )
                {
                    if (TryCollectConstant(line, out var key, out var value))
                    {
                        constants.Add((key, value));
                    }
                }
                else if ( line.StartsWith("typedef struct _") )
                {
                    i = WriteStructTypeDef(output, hpp, i);
                }
                else if ( line.StartsWith("typedef union _") )
                {
                    i = WriteStructTypeDef(output, hpp, i, true);
                }
                else if ( line.StartsWith("DECLARE_INTERFACE_") )
                {
                    i = WriteInterfaceDef(output, uuids, hpp, i);
                }
            }

            foreach ( var (name, value) in constants )
            {
                output.Append($"#define {name} {value}").AppendLine();
            }
        }

        private static string CamelCase( string str )
        {
            var parts = str.ToLower()
                           .Split('_')
                           .Select(s => $"{Char.ToUpper(s[0])}{(s.Length == 1 ? "" : s.Substring(1))}");
            return String.Join("", parts);
        }

        private static bool TryCollectConstant( string line, out string key, out string value )
        {
            key = value = null;
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if ( !Char.IsDigit(parts[2][0]) )
            {
                return false;
            }
            else
            {
                key = parts[1];
                value = parts[2];
                return true;
            }
        }

        private static int WriteStructTypeDef( StringBuilder output, string[] hpp, int i, bool isUnion = false )
        {
            var line = hpp[i].Trim();

            var structName = line.Substring((isUnion ? "typedef union _" : "typedef struct _").Length);
            if ( structName.EndsWith("{") )
            {
                structName = structName.Remove(structName.IndexOf(' '));
                i++;
            }
            else
            {
                i += 2;
            }

            output.Append($"typedef {(isUnion ? "union" : "struct")} {CamelCase(structName)} {{")
                  .AppendLine();

            var endSignature = "} " + structName;

            for ( ; !(line = hpp[i].Trim()).StartsWith(endSignature); i++ )
            {
                if ( line.StartsWith("struct")
                  || line.StartsWith("union")
                  || line.StartsWith("{")
                  || line.StartsWith("}")
                  || line.StartsWith("//")
                  || String.IsNullOrEmpty(line)
                   )
                {
                    output.AppendLine(line);
                }
                else
                {
                    output.Append("    ");

                    var sep = line.IndexOf(' ');
                    var type = line.Substring(0, sep);
                    if ( type == "IN" || type == "OUT" )
                    {
                        //output.Append('[').Append(type.ToLower()).Append("] ");

                        sep = line.IndexOf(' ', sep + 1);
                        var used = type.Length + 1;
                        type = line.Substring(used, sep - used);
                    }

                    if ( type.EndsWith("STR") )
                    {
                        output.Append("[string] ");
                    }

                    output.Append(type)
                          .AppendLine(line.Substring(sep));
                }
            }

            output.AppendLine(line).AppendLine();
            return i;
        }

        private static int WriteInterfaceDef( StringBuilder output, Dictionary<string, string> uuids, string[] hpp, int i )
        {
            var signature = "DECLARE_INTERFACE_(";
            var line = hpp[i];
            var name = line.Substring(signature.Length, line.IndexOf(',') - signature.Length);
            var super = line.Substring(line.IndexOf(',') + 1);
            super = super.Substring(0, super.Length - 1).Trim();

            output.AppendLine("[")
                  .AppendLine("    object,")
                  .AppendLine("    uuid(" + uuids[name] + "),")
                  .AppendLine("    helpstring(\"" + name + "\")")
                  .AppendLine("]")
                  .AppendLine($"interface {name} : {super} ")
                  .AppendLine("{")
                  ;

            var methodStart = "STDMETHOD";
            bool inMethod = false, paramWasOptional = false;
            for ( ; (line = hpp[i].Trim()) != "};"; i++ )
            {
                if ( !inMethod && line.StartsWith(methodStart) )
                {
                    var L = line.IndexOf('(') + 1;
                    var R = line.IndexOf(')');
                    var methodName = line.Substring(L, R - L);
                    if ( methodName == "QueryInterface" )
                    {
                        i += 10;
                    }
                    else
                    {
                        output.Append("    HRESULT ").Append(methodName).AppendLine("(");
                        inMethod = true;
                        paramWasOptional = false;
                    }
                }
                else if ( inMethod && line.StartsWith("_") )
                {
                    line = Regex.Replace(line, @" *?/\*.*?\*/ *", " ");
                    var parts = line.Split(' ');
                    if ( parts[1] == "_Reserved_" )
                    {
                        parts[1] = parts[2];
                        parts[2] = parts[3];
                    }

                    var cppAttr = parts[0];
                    var type = parts[1];
                    var param = parts[2];

                    bool isArray;
                    output.Append("        ")
                          .Append(ToIdlAttr(cppAttr, ref paramWasOptional, type, out isArray)).Append(' ');

                    if ( isArray )
                    {
                        if ( type == "PVOID" )
                        {
                            type = "byte";
                        }
                        if ( type.StartsWith("P") )
                        {
                            type = type.Substring(1);
                        }
                        if ( param.EndsWith(",") )
                        {
                            param = param.Replace(",", "[],");
                        }
                        else
                        {
                            param += "[]";
                        }
                    }

                    output.Append(type).Append(' ').AppendLine(param);
                }
                else if ( inMethod && line.StartsWith(".") )
                {
                    output.AppendLine("        [optional] SAFEARRAY(VARIANT)");
                }
                else if ( inMethod && line.StartsWith(")") )
                {
                    output.AppendLine("    );");
                    inMethod = paramWasOptional = false;
                }
            }

            output.AppendLine("};").AppendLine();
            return i;
        }

        private static string ToIdlAttr( string cppAttr, ref bool wasOptional, string type, out bool isArray )
        {
            // http://msdn.microsoft.com/en-us/library/hh916382.aspx

            var result = new StringBuilder("[");

            if ( cppAttr.StartsWith("_In_") )
            {
                result.Append("in");
            }
            else if ( cppAttr.StartsWith("_Out_") )
            {
                result.Append("out");
            }
            else
            {
                result.Append("in,out");
            }

            if ( cppAttr.Contains("_opt_") || wasOptional )
            {
                result.Append(",optional");
                wasOptional = true;
            }

            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa366731(v=vs.85).aspx

            isArray = false;
            if ( type.EndsWith("STR") )
            {
                result.Append(",string");
            }
            else
            {
                var lp = cppAttr.IndexOf('(');
                if ( lp > 0 )
                {
                    var param = cppAttr.Substring(lp + 1, cppAttr.Length - lp - 2);
                    if ( cppAttr.Contains("_to_") )
                    {
                        param = param.Split(',')[0];
                    }
                    if ( !cppAttr.Contains("_bytes_") )
                    {
                        if ( type.StartsWith("P") )
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
    }
}

using System.Text.Json.Serialization;

namespace StackDump;

[JsonSerializable(typeof(DumpStackFrame[]))]
public partial class DumpStackJsonSerializationContext : JsonSerializerContext
{
}

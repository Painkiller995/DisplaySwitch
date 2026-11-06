using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplaySwitch.Core.Inputs;

/// <summary>Persists an <see cref="InputCode"/> as a plain number so settings files stay readable.</summary>
internal sealed class InputCodeJsonConverter : JsonConverter<InputCode>
{
    public override InputCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out var value))
        {
            throw new JsonException("An input code must be a number between 0 and 255.");
        }

        return new InputCode(value);
    }

    public override void Write(Utf8JsonWriter writer, InputCode value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Value);
    }
}

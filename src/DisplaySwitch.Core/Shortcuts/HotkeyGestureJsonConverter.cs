using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplaySwitch.Core.Shortcuts;

/// <summary>Persists a <see cref="HotkeyGesture"/> as readable text such as "Ctrl+Alt+1".</summary>
internal sealed class HotkeyGestureJsonConverter : JsonConverter<HotkeyGesture>
{
    public override HotkeyGesture Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        // Invalid gestures load as default (invalid) and are removed by the settings sanitizer,
        // so one hand-edited mistake does not discard the whole file.
        reader.TokenType == JsonTokenType.String && HotkeyGesture.TryParse(reader.GetString(), out var gesture) ? gesture : default;

    public override void Write(Utf8JsonWriter writer, HotkeyGesture value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}

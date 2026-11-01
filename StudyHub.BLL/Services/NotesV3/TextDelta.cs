using System.Text;
using System.Text.Json;

namespace StudyHub.BLL.Services.NotesV3;

// Bảng hiện có lưu Quill Delta JSON. Giao diện mới dùng textarea thuần.
internal static class TextDelta
{
    public static string ToText(string deltaJson)
    {
        using var document = JsonDocument.Parse(deltaJson);
        var text = new StringBuilder();

        foreach (var operation in document.RootElement.GetProperty("ops").EnumerateArray())
        {
            if (operation.TryGetProperty("insert", out var insert) &&
                insert.ValueKind == JsonValueKind.String)
            {
                text.Append(insert.GetString());
            }
        }

        // Quill luôn thêm một newline cuối tài liệu.
        if (text.Length > 0 && text[text.Length - 1] == '\n')
        {
            text.Length--;
        }

        return text.ToString();
    }

    public static string FromText(string? value)
    {
        var text = (value ?? string.Empty).Replace("\r\n", "\n");

        if (!text.EndsWith('\n'))
        {
            text += "\n";
        }

        var json = JsonSerializer.Serialize(new { ops = new[] { new { insert = text } } });

        if (Encoding.UTF8.GetByteCount(json) > NotesV3Validation.MaxDeltaBytes)
        {
            throw new NotesV3Exception(413, "Ghi chú vượt quá 2 MiB.");
        }

        return json;
    }
}

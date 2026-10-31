using System.Text;
using System.Text.Json;

namespace StudyHub.BLL.Services.NotesV3;

public sealed record ValidatedDelta(
    string Json,
    string PlainText)
{
    public bool HasMeaningfulText =>
        !string.IsNullOrWhiteSpace(PlainText);
}

public static class NotesV3Validation
{
    public const string EmptyDelta =
        "{\"ops\":[{\"insert\":\"\\n\"}]}";

    public const int MaxDeltaBytes = 2 * 1024 * 1024;
    public const int MaxUpdateBytes = 256 * 1024;

    public static string Title(string? value)
    {
        var title = value?.Trim();

        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new NotesV3Exception(
                400, "Tiêu đề phải có từ 1 đến 200 ký tự.");
        }

        return title;
    }

    public static byte[] Version(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NotesV3Exception(
                400, "Thiếu phiên bản dữ liệu.");
        }

        try
        {
            var bytes = Convert.FromBase64String(value);

            if (bytes.Length != 8)
            {
                throw new FormatException();
            }

            return bytes;
        }
        catch (FormatException)
        {
            throw new NotesV3Exception(
                400, "Phiên bản dữ liệu không hợp lệ.");
        }
    }

    public static ValidatedDelta Delta(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NotesV3Exception(
                400, "Thiếu nội dung ghi chú.");
        }

        if (Encoding.UTF8.GetByteCount(value) > MaxDeltaBytes)
        {
            throw new NotesV3Exception(
                413, "Nội dung ghi chú vượt quá 2 MiB.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                value,
                new JsonDocumentOptions { MaxDepth = 16 });

            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("ops", out var operations) ||
                operations.ValueKind != JsonValueKind.Array ||
                operations.GetArrayLength() == 0)
            {
                throw InvalidDelta();
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "ops")
                {
                    throw InvalidDelta();
                }
            }

            var plainText = new StringBuilder();
            var normalized = new List<object>();

            foreach (var operation in operations.EnumerateArray())
            {
                if (operation.ValueKind != JsonValueKind.Object ||
                    !operation.TryGetProperty("insert", out var insert) ||
                    insert.ValueKind != JsonValueKind.String)
                {
                    throw InvalidDelta();
                }

                foreach (var property in operation.EnumerateObject())
                {
                    if (property.Name is not ("insert" or "attributes"))
                    {
                        throw InvalidDelta();
                    }
                }

                var text = insert.GetString()!;
                plainText.Append(text);

                if (operation.TryGetProperty(
                    "attributes", out var attributes))
                {
                    ValidateAttributes(attributes);

                    normalized.Add(new
                    {
                        insert = text,
                        attributes = attributes.Clone()
                    });
                }
                else
                {
                    normalized.Add(new { insert = text });
                }
            }

            // Quill document hoàn chỉnh luôn kết thúc bằng newline.
            if (plainText.Length == 0 || plainText[^1] != '\n')
            {
                throw InvalidDelta();
            }

            return new ValidatedDelta(
                JsonSerializer.Serialize(new { ops = normalized }),
                plainText.ToString());
        }
        catch (JsonException)
        {
            throw InvalidDelta();
        }
    }

    private static void ValidateAttributes(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object)
        {
            throw InvalidDelta();
        }

        foreach (var attribute in attributes.EnumerateObject())
        {
            var value = attribute.Value;

            switch (attribute.Name)
            {
                case "bold":
                case "italic":
                case "underline":
                    if (value.ValueKind is not
                        (JsonValueKind.True or JsonValueKind.False))
                    {
                        throw InvalidDelta();
                    }
                    break;

                case "header":
                    if (value.ValueKind != JsonValueKind.Number ||
                        !value.TryGetInt32(out var level) ||
                        level is < 1 or > 6)
                    {
                        throw InvalidDelta();
                    }
                    break;

                case "list":
                    if (value.ValueKind != JsonValueKind.String ||
                        value.GetString() is not ("ordered" or "bullet"))
                    {
                        throw InvalidDelta();
                    }
                    break;

                case "link":
                    if (value.ValueKind != JsonValueKind.String ||
                        !Uri.TryCreate(
                            value.GetString(),
                            UriKind.Absolute,
                            out var uri) ||
                        uri.Scheme is not ("https" or "http" or "mailto"))
                    {
                        throw new NotesV3Exception(
                            400, "Liên kết không hợp lệ.");
                    }
                    break;

                default:
                    throw new NotesV3Exception(
                        400, "Định dạng ghi chú chưa được hỗ trợ.");
            }
        }
    }

    private static NotesV3Exception InvalidDelta() =>
        new(400, "Nội dung phải là Quill Delta văn bản hợp lệ.");
}
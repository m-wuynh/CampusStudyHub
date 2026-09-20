using StudyHub.Web.Enums.Flexcil;

namespace StudyHub.Web.Data.Entities.Flexcil;

public class Annotation
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public AnnotationType Type { get; set; }

    /*
     * JSON chứa dữ liệu hình học của annotation.
     *
     * Ví dụ Pen:
     * {
     *   "points": [
     *      { "x": 0.2, "y": 0.3 },
     *      { "x": 0.21, "y": 0.31 }
     *   ]
     * }
     *
     * Highlight:
     * {
     *   "x": 0.2,
     *   "y": 0.3,
     *   "width": 0.4,
     *   "height": 0.05
     * }
     */
    public string Data { get; set; } = string.Empty;

    public string Color { get; set; } = "#FACC15";

    public double Opacity { get; set; } = 0.4;

    public double StrokeWidth { get; set; } = 2;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Document? Document { get; set; }
}

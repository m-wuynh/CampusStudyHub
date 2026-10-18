using System.Globalization;
using System.Text;
using System.Security;

static class VectorDiagrams
{
    static string E(string text) => SecurityElement.Escape(text) ?? "";
    static string N(double n) => n.ToString("0.##", CultureInfo.InvariantCulture);
    static StringBuilder Start(int width, int height, string title) => new($$"""
        <svg xmlns="http://www.w3.org/2000/svg" width="{{width}}" height="{{height}}" viewBox="0 0 {{width}} {{height}}" role="img" aria-label="{{E(title)}}">
        <defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto"><path d="M0 0 L10 5 L0 10 Z" fill="#475569"/></marker><marker id="inherit" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="12" markerHeight="12" orient="auto"><path d="M1 1 L11 6 L1 11 Z" fill="white" stroke="#475569"/></marker></defs>
        <rect width="100%" height="100%" fill="white"/>
        <style>text{font-family:Arial,sans-serif;fill:#0f172a}.edge{fill:none;stroke:#64748b;stroke-width:1.7;stroke-linejoin:round}.label{font-size:13px;fill:#334155}</style>
        <text x="30" y="38" font-size="24" font-weight="700">{{E(title)}}</text>
        """);
    static void Text(StringBuilder s, double x, double cy, string value, int size = 16, string weight = "400")
    {
        var lines = value.Split('\n');
        s.Append($"<text x='{N(x)}' text-anchor='middle' font-size='{size}' font-weight='{weight}'>");
        for (var i = 0; i < lines.Length; i++) s.Append($"<tspan x='{N(x)}' y='{N(cy - (lines.Length - 1) * 11 + i * 22 + 5)}'>{E(lines[i])}</tspan>");
        s.Append("</text>");
    }
    public static string Usecase()
    {
        var s = Start(1460, 1580, "STUDY GROUP — USE CASE THEO TÁC NHÂN");
        s.Append("<rect x='235' y='70' width='1200' height='1435' rx='6' fill='#f8fafc' stroke='#334155' stroke-width='2'/>");
        Text(s, 835, 92, "Hệ thống StudyHub / Phân hệ Study Group", 18, "700");
        var groups = new[] {
            (Y:120, H:230, Role:"Người dùng\nđã đăng nhập", Title:"KHÁM PHÁ VÀ THAM GIA", Fill:"#eff6ff", Items:new[]{"Tìm kiếm và xem trước nhóm", "Tạo nhóm", "Tham gia / gửi yêu cầu\nhoặc chấp nhận lời mời", "Rút yêu cầu / xin gỡ cấm\n(theo trạng thái)"}),
            (Y:370, H:420, Role:"Thành viên\nActive", Title:"HOẠT ĐỘNG HỌC NHÓM", Fill:"#f0fdf4", Items:new[]{"Xem thông tin và thành viên nhóm", "Chat và đọc thông báo", "Xem buổi học\nĐăng ký / hủy đăng ký", "Xem bài tập / nộp bài\nXem lịch sử và phản hồi", "Đọc tài nguyên chung\nChia sẻ / gỡ tài nguyên của mình", "Tìm / đặt câu hỏi / trả lời\nChọn lời giải cho câu hỏi của mình", "Xem tiến độ nhóm và cá nhân", "Rời nhóm\n(trừ Owner)"}),
            (Y:810, H:420, Role:"Người quản lý\nOwner / Moderator", Title:"ĐIỀU PHỐI VÀ QUẢN LÝ", Fill:"#fff7ed", Items:new[]{"Duyệt / từ chối yêu cầu\nKick / ban / gỡ cấm có điều kiện", "Tạo và sao chép lời mời", "Đăng / ghim thông báo", "Tạo / sửa / hủy buổi học\nĐiểm danh / kết thúc / tổng kết", "Giao / sửa / đóng bài tập\nĐánh giá bài nộp mới nhất", "Gỡ tài nguyên khỏi nhóm\nChọn lời giải trong hỏi đáp", "Đặt mục tiêu tuần\nXem tiến độ từng thành viên"}),
            (Y:1250, H:230, Role:"Owner", Title:"QUYỀN RIÊNG CỦA OWNER", Fill:"#f5f3ff", Items:new[]{"Sửa thông tin và cấu hình nhóm", "Bổ nhiệm / hạ Moderator"})
        };
        var previous = 0;
        foreach (var g in groups)
        {
            var actorY = g.Y + g.H / 2;
            s.Append($"<rect x='255' y='{g.Y}' width='1160' height='{g.H}' rx='10' fill='{g.Fill}' stroke='#cbd5e1'/>");
            Text(s, 835, g.Y + 24, g.Title, 15, "700");
            s.Append($"<circle cx='118' cy='{actorY - 44}' r='13' fill='white' stroke='#334155' stroke-width='2'/><path d='M118 {actorY - 31} V{actorY + 6} M93 {actorY - 16} H143 M118 {actorY + 6} L94 {actorY + 35} M118 {actorY + 6} L142 {actorY + 35}' fill='none' stroke='#334155' stroke-width='2'/>");
            Text(s, 118, actorY + 65, g.Role, 15, "700");
            if (previous > 0)
            {
                s.Append($"<path class='edge' d='M92 {actorY - 16} H40 V{previous - 16} H91' marker-end='url(#inherit)'/>");
            }
            previous = actorY;
            for (var i = 0; i < g.Items.Length; i++)
            {
                var cx = 555 + i % 2 * 555;
                var cy = g.Y + 86 + i / 2 * 93;
                s.Append($"<path class='edge' d='M143 {actorY - 16} H218 V{cy - 42} H{cx} V{cy - 34}'/>");
            }
            for (var i = 0; i < g.Items.Length; i++)
            {
                var cx = 555 + i % 2 * 555;
                var cy = g.Y + 86 + i / 2 * 93;
                s.Append($"<ellipse cx='{cx}' cy='{cy}' rx='240' ry='34' fill='white' stroke='#64748b' stroke-width='1.5'/>");
                Text(s, cx, cy, g.Items[i], 16);
            }
        }
        Text(s, 730, 1530, "Đường nối: tương tác với use case. Mũi tên tam giác rỗng: kế thừa vai trò.", 14);
        Text(s, 730, 1553, "Moderator không quản lý Owner / Moderator khác. Owner không rời nhóm trực tiếp.", 14);
        return s.Append("</svg>").ToString();
    }

    sealed record Node(string Id, int Lane, int Row, string Label, string Shape = "box");
    sealed record Edge(string From, string To, string Label = "", string Route = "normal", bool Dashed = false);
    static string Swim(string title, string[] lanes, Node[] nodes, Edge[] edges, string footnote)
    {
        const int width = 1460, step = 118;
        var height = 245 + (nodes.Max(n => n.Row) + 1) * step;
        var s = Start(width, height, title);
        var colors = new[] { "#eff6ff", "#f5f3ff", "#fff7ed", "#f0fdf4" };
        double X(Node n) => 190 + n.Lane * 350;
        double Y(Node n) => 185 + n.Row * step;
        for (var i = 0; i < lanes.Length; i++)
        {
            s.Append($"<rect x='{15 + i * 350}' y='65' width='350' height='{height - 135}' fill='{colors[i]}' stroke='#cbd5e1'/>");
            s.Append($"<rect x='{15 + i * 350}' y='65' width='350' height='55' fill='{colors[i]}' stroke='#94a3b8'/>");
            Text(s, 190 + i * 350, 93, lanes[i], 15, "700");
        }
        foreach (var e in edges)
        {
            var a = nodes.Single(n => n.Id == e.From); var b = nodes.Single(n => n.Id == e.To);
            var ax = X(a); var ay = Y(a); var bx = X(b); var by = Y(b);
            string path; double lx, ly;
            if (e.Route is "right" or "left")
            {
                var outer = e.Route == "right" ? 1438 : 7;
                var sign = e.Route == "right" ? 1 : -1;
                path = $"M{N(ax + sign * 142)} {N(ay)} H{outer} V{N(by)} H{N(bx + sign * 142)}";
                lx = outer + (sign == 1 ? -75 : 75); ly = (ay + by) / 2;
            }
            else if (a.Row == b.Row)
            {
                var sign = b.Lane > a.Lane ? 1 : -1;
                path = $"M{N(ax + sign * 142)} {N(ay)} H{N(bx - sign * 142)}";
                lx = (ax + bx) / 2; ly = ay - 13;
            }
            else if (Math.Abs(a.Row - b.Row) > 1)
            {
                var down = b.Row > a.Row ? 1 : -1;
                var sign = b.Lane >= a.Lane ? 1 : -1;
                var track = ax + sign * 160;
                var middle = by - down * 59;
                path = $"M{N(ax + sign * 142)} {N(ay)} H{N(track)} V{N(middle)} H{N(bx)} V{N(by - down * 39)}";
                lx = (track + bx) / 2; ly = middle - 10;
            }
            else
            {
                var down = b.Row > a.Row ? 1 : -1;
                var middle = (ay + by) / 2;
                path = $"M{N(ax)} {N(ay + down * 39)} V{N(middle)} H{N(bx)} V{N(by - down * 39)}";
                lx = (ax + bx) / 2 + (ax == bx ? 50 : 0); ly = middle - 9;
            }
            s.Append($"<path class='edge' d='{path}' marker-end='url(#arrow)' {(e.Dashed ? "stroke-dasharray='6 4'" : "")}/>");
            if (e.Label.Length > 0)
            {
                s.Append($"<rect x='{N(lx - e.Label.Length * 3.7)}' y='{N(ly - 13)}' width='{N(e.Label.Length * 7.4)}' height='20' rx='4' fill='white'/>");
                Text(s, lx, ly - 3, e.Label, 13);
            }
        }
        foreach (var n in nodes)
        {
            var x = X(n); var y = Y(n);
            if (n.Shape == "decision") s.Append($"<path d='M{x} {y - 45} L{x + 142} {y} L{x} {y + 45} L{x - 142} {y} Z' fill='#fefce8' stroke='#a16207' stroke-width='1.7'/>");
            else s.Append($"<rect x='{x - 142}' y='{y - 38}' width='284' height='76' rx='{(n.Shape == "end" ? 35 : 9)}' fill='white' stroke='#64748b' stroke-width='1.5'/>");
            Text(s, x, y, n.Label, 15);
        }
        Text(s, width / 2, height - 36, footnote, 13);
        return s.Append("</svg>").ToString();
    }
    public static string Join() => Swim("STUDY GROUP — SWIMLANE: THAM GIA NHÓM",
        ["NGƯỜI MUỐN THAM GIA", "HỆ THỐNG STUDY HUB", "OWNER / MODERATOR", "DATABASE"],
        [new("start",0,0,"Bắt đầu","end"),new("choose",0,1,"Chọn tham gia nhóm\nhoặc chấp nhận link mời"),new("check",1,1,"Kiểm tra đăng nhập, nhóm,\ntrạng thái và điều kiện tham gia"),
         new("valid",1,2,"Hợp lệ, không Banned,\ncòn chỗ?","decision"),new("error",0,2,"Xem lỗi / trạng thái hiện có\nKhông tạo membership trùng","end"),
         new("mode",1,3,"Cách tham gia?","decision"),new("active",3,3,"Open / lời mời hợp lệ:\nGhi Active, JoinedAt\nTăng lượt dùng mã mời nếu có"),
         new("pending",3,4,"Approval:\nGhi membership = Pending"),new("wait",0,4,"Xem trạng thái chờ duyệt\nCó thể rút yêu cầu"),
         new("review",2,5,"Mở tab Thành viên\nChọn Duyệt hoặc Từ chối"),new("recheck",1,6,"Kiểm tra lại quyền, Pending\nvà sức chứa khi duyệt"),
         new("decision",1,7,"Kết quả xử lý?","decision"),new("approved",3,7,"Duyệt hợp lệ:\nPending → Active"),new("rejected",3,8,"Từ chối yêu cầu:\nPending → Rejected"),
         new("result",0,9,"Tải lại để xem kết quả\nActive: vào nội dung nhóm\nRejected: xem thông báo","end")],
        [new("start","choose"),new("choose","check"),new("check","valid"),new("valid","error","Không"),new("valid","mode","Có"),
         new("mode","active","Open / lời mời"),new("mode","pending","Approval"),new("pending","wait"),new("wait","review","Quản lý mở danh sách",Dashed:true),
         new("review","recheck"),new("recheck","decision"),new("decision","approved","Duyệt, còn chỗ"),new("decision","rejected","Từ chối"),
         new("approved","result"),new("rejected","result"),new("active","result",Route:"right",Dashed:true)],
        "Nếu quyền / trạng thái / sức chứa đã đổi: báo lỗi, không duyệt. Rút yêu cầu hợp lệ chuyển Pending → Left.");

    public static string Learning() => Swim("STUDY GROUP — SWIMLANE: NỘP BÀI VÀ PHẢN HỒI",
        ["OWNER / MODERATOR", "THÀNH VIÊN ACTIVE", "HỆ THỐNG STUDY HUB", "DATABASE"],
        [new("start",0,0,"Bắt đầu\nTạo lịch học và giao bài","end"),new("createcheck",2,0,"Kiểm tra quyền, lịch học,\nhạn nộp và người nhận"),new("save",3,1,"Lưu buổi học / bài tập\nChốt danh sách người nhận"),
         new("read",1,2,"Xem lịch, đăng ký,\nđọc tài nguyên và làm bài"),new("attendance",0,3,"Sau giờ bắt đầu: điểm danh\nKết thúc và tổng kết buổi học"),new("saveattendance",3,3,"Lưu điểm danh / tổng kết\nBuổi học → Completed"),
         new("submit",1,4,"Nộp lời giải / liên kết\nhoặc nộp phiên bản mới"),new("check",2,4,"Đủ điều kiện nộp?","decision"),
         new("error",1,5,"Xem lý do bị chặn\nSửa dữ liệu / tải lại"),new("submission",3,5,"Tạo bài nộp phiên bản mới\nGiữ nội dung và phản hồi cũ"),
         new("review",0,6,"Đọc bài nộp mới nhất\nNhập đánh giá và nhận xét"),new("reviewcheck",2,6,"Kiểm tra quyền quản lý\nvà phiên bản mới nhất"),
         new("decision",2,7,"Kết quả đánh giá?","decision"),new("revision",3,8,"Lưu NeedsRevision\nNhận xét là bắt buộc"),new("fix",1,8,"Đọc nhận xét và sửa bài\nNộp lại nếu còn đủ điều kiện"),
         new("complete",3,9,"Lưu Completed\nLưu người và thời gian đánh giá"),new("progress",2,10,"Tính tiến độ khi tải tab\nBuổi Completed + bài mới nhất\nTuần theo UTC+7"),
         new("end",1,11,"Xem kết quả, phản hồi\nvà tiến độ tuần","end")],
        [new("start","createcheck"),new("createcheck","save"),new("save","read"),new("read","submit"),new("read","attendance",Dashed:true),
         new("attendance","saveattendance"),new("submit","check"),new("check","error","Không"),new("check","submission","Có"),
         new("submission","review"),new("review","reviewcheck"),new("reviewcheck","decision"),new("decision","revision","Cần sửa"),
         new("revision","fix"),new("fix","submit",Route:"left",Dashed:true),new("decision","complete","Hoàn thành"),
         new("complete","progress"),new("progress","end"),new("saveattendance","progress",Route:"right",Dashed:true)],
        "Nộp bài: Active, được giao, bài đang mở, đúng hạn hoặc cho nộp muộn, đúng phiên bản. Buổi học không cần đã kết thúc.");

    public static string Resources() => Swim("STUDY GROUP — SWIMLANE: CHIA SẺ TÀI NGUYÊN",
        ["CHỦ SỞ HỮU", "HỆ THỐNG STUDY HUB", "THÀNH VIÊN / QUẢN LÝ", "DATABASE"],
        [new("start",0,0,"Chọn Note / PDF / link / deck\nNhấn Chia sẻ","end"),new("check",1,0,"Kiểm tra Active, quyền sở hữu,\ntài nguyên hợp lệ và\nkhông thuộc nhóm khác"),
         new("valid",1,1,"Được chia sẻ?","decision"),new("error",0,1,"Xem thông báo lỗi\nDữ liệu không thay đổi","end"),new("save",3,2,"StudyGroupId = nhóm hiện tại\nVisibility = Group"),
         new("choose",2,3,"Thành viên mở tài nguyên\ntrong tab Tài nguyên"),new("readcheck",1,3,"Kiểm tra quyền đọc lại\nở từng request"),new("canread",1,4,"Active, đúng nhóm,\nVisibility = Group?","decision"),
         new("display",2,4,"Đọc ghi chú / tài liệu\nXem PDF / đáp án flashcard"),new("deny",0,4,"Từ chối truy cập","end"),
         new("unshare",0,6,"Gỡ chia sẻ tài nguyên của mình\nhoặc rời nhóm"),new("manage",2,6,"Quản lý gỡ chia sẻ\nhoặc kick / ban chủ sở hữu"),
         new("revokecheck",1,7,"Kiểm tra quyền và trạng thái\nNếu hợp lệ, thu hồi chia sẻ"),new("private",3,8,"Visibility = Private\nStudyGroupId = null\nGiữ nội dung và chủ sở hữu"),
         new("end",2,9,"Những lần mở tiếp theo\nkhông còn được nhóm truy cập","end")],
        [new("start","check"),new("check","valid"),new("valid","error","Không"),new("valid","save","Có"),new("save","choose"),
         new("choose","readcheck"),new("readcheck","canread"),new("canread","display","Có"),new("canread","deny","Không"),
         new("display","unshare","Có thể thu hồi sau",Dashed:true),new("display","manage",Dashed:true),new("unshare","revokecheck"),new("manage","revokecheck"),
         new("revokecheck","private"),new("private","end")],
        "Chỉ chủ sở hữu được chia sẻ. Chủ sở hữu hoặc quản lý được gỡ. Endpoint PDF kiểm tra quyền và không cache nội dung nhóm.");
}

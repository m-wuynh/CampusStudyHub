using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Playwright;

var output = Path.GetFullPath(args[0]);
var libraryPath = Path.Combine(AppContext.BaseDirectory, "mermaid.min.js");
if (!File.Exists(libraryPath))
{
    using var http = new HttpClient();
    await File.WriteAllTextAsync(libraryPath, await http.GetStringAsync("https://cdn.jsdelivr.net/npm/mermaid@11.4.1/dist/mermaid.min.js"));
}
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1800, Height = 1200 }, DeviceScaleFactor = 1.5f });
await page.SetContentAsync("<html><head><meta charset='utf-8'></head><body style='margin:0;background:white;font:16px Arial,sans-serif'><main id='diagram'></main></body></html>");
await page.AddScriptTagAsync(new() { Path = libraryPath });
await page.EvaluateAsync("""
    () => mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'base', htmlLabels: false,
      themeVariables: { fontFamily: 'Arial, sans-serif', fontSize: '16px', primaryColor: '#ffffff', primaryTextColor: '#0f172a', primaryBorderColor: '#64748b', lineColor: '#64748b', clusterBkg: '#f8fafc', clusterBorder: '#94a3b8', edgeLabelBackground: '#ffffff' },
      flowchart: { htmlLabels: false, curve: 'linear', nodeSpacing: 26, rankSpacing: 50, padding: 15, useMaxWidth: false }
    })
    """);
var titles = new Dictionary<string, (string Title, string Description)>
{
    ["01-usecase"] = ("Use case — tác nhân và chức năng", "Các quyền chung, quyền thành viên, quyền quản lý và quyền riêng của Owner. Người quản lý là vai trò chung của Owner / Moderator. Mũi tên tam giác rỗng biểu thị kế thừa vai trò; mỗi chức năng vẫn chịu các điều kiện nghiệp vụ."),
    ["02-screenflow"] = ("Screenflow — điều hướng giao diện", "Xanh dương: trang. Xanh lá: tab trong trang nhóm. Cam: hộp thoại / form. Xám: trạng thái / thao tác tại chỗ. Lưu hoặc hủy hộp thoại quay về tab hiện tại; lưu thành công tải lại dữ liệu."),
    ["03-swimlane-join"] = ("Swimlane — tham gia nhóm", "Luồng Open, Approval và lời mời. Pending chỉ xem trước nhóm; sức chứa và quyền được kiểm tra lại khi duyệt. Trạng thái được cập nhật trên giao diện sau khi tải lại."),
    ["04-swimlane-learning"] = ("Swimlane — học nhóm, nộp bài và phản hồi", "Kịch bản phối hợp một buổi học có giao bài. Bài tập cũng có thể tạo độc lập với buổi học. Nộp bài không yêu cầu đã điểm danh hoặc buổi học đã kết thúc. Khi Cần sửa, nộp lại vẫn phải thỏa hạn nộp và trạng thái bài đang mở."),
    ["05-swimlane-resources"] = ("Swimlane — chia sẻ và thu hồi tài nguyên", "Tài nguyên thuộc quyền sở hữu cá nhân. Chủ sở hữu hoặc người quản lý có thể gỡ chia sẻ; rời nhóm, kick hoặc ban đưa tài nguyên của người đó về riêng tư.")
};
var body = new StringBuilder();
var nav = new StringBuilder();
foreach (var file in Directory.GetFiles(output, "*.mmd").Order())
{
    var name = Path.GetFileNameWithoutExtension(file);
    var source = await File.ReadAllTextAsync(file);
    var vector = name switch {
        "01-usecase" => VectorDiagrams.Usecase(),
        "03-swimlane-join" => VectorDiagrams.Join(),
        "04-swimlane-learning" => VectorDiagrams.Learning(),
        "05-swimlane-resources" => VectorDiagrams.Resources(),
        _ => null
    };
    var svg = vector ?? await page.EvaluateAsync<string>("""
        async ({source,id}) => {
            const result = await mermaid.render(id, source);
            document.getElementById('diagram').innerHTML = result.svg;
            const svg = document.querySelector('#diagram svg');
            svg.style.background = 'white';
            svg.setAttribute('role', 'img');
            return svg.outerHTML;
        }
        """, new { source, id = "diagram" + name[..2] });
    if (vector is not null)
        await page.EvaluateAsync("svg => document.getElementById('diagram').innerHTML = svg", vector);
    await File.WriteAllTextAsync(Path.Combine(output, name + ".svg"), svg, new UTF8Encoding(false));
    var box = await page.Locator("#diagram svg").BoundingBoxAsync();
    if (box is not null)
    {
        await page.SetViewportSizeAsync((int)Math.Ceiling(box.Width) + 16, (int)Math.Ceiling(box.Height) + 16);
        await page.Locator("#diagram svg").ScreenshotAsync(new() { Path = Path.Combine(output, name + ".png") });
    }
    var info = titles[name];
    var title = HtmlEncoder.Default.Encode(info.Title);
    nav.Append($"<a href='#{name}'>{title}</a>");
    body.Append($"<section id='{name}'><h2>{title}</h2><p>{HtmlEncoder.Default.Encode(info.Description)}</p><div class='toolbar'><a href='{name}.svg' download>Tải SVG</a><a href='{name}.png' download>Tải PNG</a><a href='{name}.mmd' download>Mã Mermaid</a><button type='button' data-zoom='-0.2'>−</button><button type='button' data-zoom='0.2'>+</button><button type='button' data-fit>Vừa màn hình</button></div><div class='canvas'>{svg}</div></section>");
    Console.WriteLine($"Rendered {name}: {box?.Width:0} x {box?.Height:0}");
}
var html = """
    <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Study Group — Use case, Screenflow, Swimlane</title>
    <style>
    *{box-sizing:border-box}body{margin:0;background:#f1f5f9;color:#0f172a;font:15px/1.65 Arial,sans-serif}header{padding:36px 5%;background:#172554;color:white}h1{margin:0;font-size:28px}header p{color:#cbd5e1;margin-bottom:0}nav{display:flex;gap:10px;flex-wrap:wrap;padding:18px 5%;background:white;border-bottom:1px solid #cbd5e1}nav a,.toolbar a,.toolbar button{border:1px solid #cbd5e1;padding:6px 12px;border-radius:7px;text-decoration:none;color:#334155;background:white;font:inherit;cursor:pointer}main{padding:24px 3%}section{background:white;border:1px solid #e2e8f0;border-radius:14px;padding:24px;margin-bottom:28px}h2{margin:0 0 6px}section>p{color:#475569;max-width:1100px}.toolbar{display:flex;flex-wrap:wrap;gap:8px;margin:16px 0}.canvas{overflow:auto;border:1px solid #e2e8f0;border-radius:8px;padding:12px;max-height:85vh}.canvas svg{display:block;height:auto;max-width:none!important}footer{padding:12px 5% 30px;color:#64748b}@media print{nav,.toolbar{display:none}body,header{background:white;color:black}section{break-before:page;padding:0;border:0}.canvas{max-height:none;overflow:visible;border:0}.canvas svg{width:100%!important}header p{color:#475569}}
    </style></head><body><header><h1>Study Group — Bộ sơ đồ nghiệp vụ</h1><p>Theo mã nguồn CampusStudyHub · 12/10/2026 · Dùng độc lập, không cần kết nối mạng</p></header><nav>
    """ + nav + "</nav><main>" + body + """
    </main><footer>Owner không được rời nhóm trực tiếp. Moderator không được quản lý Owner hoặc Moderator khác. Gỡ cấm yêu cầu người dùng đã gửi yêu cầu và đúng người có quyền xử lý. Các mũi tên giữa các làn mô tả luồng nghiệp vụ, không biểu thị tự động gửi thông báo.</footer>
    <script>
    document.querySelectorAll('section').forEach(section=>{const canvas=section.querySelector('.canvas'),svg=canvas.querySelector('svg');const width=svg.viewBox.baseVal.width;let scale=1;const fit=()=>{scale=Math.min(1,(canvas.clientWidth-26)/width);svg.style.width=(width*scale)+'px';};fit();section.querySelectorAll('[data-zoom]').forEach(b=>b.onclick=()=>{scale=Math.max(.15,Math.min(3,scale+Number(b.dataset.zoom)));svg.style.width=(width*scale)+'px';});section.querySelector('[data-fit]').onclick=fit;});
    </script></body></html>
    """;
await File.WriteAllTextAsync(Path.Combine(output, "index.html"), html, new UTF8Encoding(false));
Console.WriteLine("Saved offline viewer.");

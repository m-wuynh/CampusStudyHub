using System.Net.Http.Headers;
using Microsoft.Playwright;
using StudyHub.DAL.Entities;
using Xunit;

namespace StudyHub.Tests;

public sealed class GroupLearningBrowserTests(GroupLearningFixture f) : IClassFixture<GroupLearningFixture>
{
    [Fact]
    public async Task Manager_and_member_can_complete_learning_workflows_in_browser()
    {
        var note = new Note { OwnerUserId = f.MemberId, Title = "Ghi chú chia sẻ qua trình duyệt", Body = "Nội dung ôn tập", BodyFormat = "PlainText", Visibility = "Private" };
        await using (var db = f.NewDb())
        {
            db.Notes.Add(note);
            await db.SaveChangesAsync();
        }
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = true });
        using var ownerClient = await f.Client(f.OwnerId);
        using var memberClient = await f.Client(f.MemberId);
        var errors = new List<string>();
        var manager = await OpenPage(browser, ownerClient, errors);
        var member = await OpenPage(browser, memberClient, errors);
        await Assertions.Expect(manager.Locator("#gl-sessions")).ToContainTextAsync("Chưa có buổi học");
        await Assertions.Expect(manager.Locator("#gl-resources")).ToContainTextAsync("Chưa có tài nguyên chung");
        await Assertions.Expect(manager.Locator("#gl-progress")).ToContainTextAsync("Tỷ lệ hoàn thành");
        await manager.Locator("[data-panel=sessions]").ClickAsync();
        await manager.Locator("[data-gl-action=session-new]").ClickAsync();
        await manager.Locator("[name=title]").FillAsync("Buổi học qua trình duyệt");
        await manager.Locator("[name=objective]").FillAsync("Hoàn thành 3 bài tập");
        await manager.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-sessions")).ToContainTextAsync("Buổi học qua trình duyệt");

        await member.Locator("[data-panel=sessions]").ClickAsync();
        await member.Locator("[data-panel-content=sessions] [data-gl-action=refresh]").ClickAsync();
        await member.Locator("[data-gl-action=session-register]").ClickAsync();
        await Assertions.Expect(member.Locator("[data-gl-action=session-register]")).ToHaveTextAsync("Hủy đăng ký");

        await manager.Locator("[data-panel=assignments]").ClickAsync();
        await manager.Locator("[data-gl-action=assignment-new]").ClickAsync();
        await manager.Locator("[name=title]").FillAsync("Bài tập qua trình duyệt");
        await manager.Locator("#gl-form [name=description]").FillAsync("Tính đạo hàm");
        await manager.Locator($"[name=assignee][value='{f.MemberId}']").CheckAsync();
        await manager.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-assignments")).ToContainTextAsync("Bài tập qua trình duyệt");
        await member.Locator("[data-panel=assignments]").ClickAsync();
        await member.Locator("[data-panel-content=assignments] [data-gl-action=refresh]").ClickAsync();
        await member.Locator("[data-gl-action=submit]").ClickAsync();
        await member.Locator("[name=content]").FillAsync("Lời giải của thành viên");
        await member.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(member.Locator("#gl-assignments")).ToContainTextAsync("Đã nộp");
        await manager.Locator("[data-panel-content=assignments] [data-gl-action=refresh]").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-assignments")).ToContainTextAsync("Lời giải của thành viên");
        await manager.Locator("#gl-assignments details summary").Last.ClickAsync();
        await manager.Locator("[data-gl-action=review]").ClickAsync();
        await manager.Locator("[name=feedback]").FillAsync("Đúng rồi");
        await manager.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-assignments")).ToContainTextAsync("Hoàn thành");

        await member.Locator("[data-panel=questions]").ClickAsync();
        await member.Locator("[data-gl-action=question-new]").ClickAsync();
        await member.Locator("[name=title]").FillAsync("Một câu hỏi mới");
        await member.Locator("[name=subject]").FillAsync("Toán");
        await member.Locator("[name=body]").FillAsync("<img src=x onerror=alert(1)> Đạo hàm là gì?");
        await member.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(member.Locator("#gl-questions")).ToContainTextAsync("Đạo hàm là gì?");
        Assert.Equal(0, await member.Locator("#gl-questions img").CountAsync());
        await member.Locator("[data-gl-action=answer]").ClickAsync();
        await member.Locator("[name=body]").FillAsync("Giải thích về đạo hàm");
        await member.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(member.Locator("#gl-questions")).ToContainTextAsync("1 câu trả lời");
        await member.Locator("#gl-questions summary").ClickAsync();
        await member.Locator("[data-gl-action=answer-accept]").ClickAsync();
        await Assertions.Expect(member.Locator("#gl-questions")).ToContainTextAsync("Đã giải quyết");
        await member.Locator("#gl-question-search").FillAsync("không tồn tại");
        await Assertions.Expect(member.Locator("#gl-questions")).ToContainTextAsync("Không tìm thấy");

        await member.Locator("[data-panel=resources]").ClickAsync();
        await member.Locator("[data-gl-action=resource-share]").ClickAsync();
        await member.Locator("[name=resource]").SelectOptionAsync($"Note/{note.NoteId}");
        await member.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(member.Locator("#gl-resources")).ToContainTextAsync(note.Title);
        await Assertions.Expect(member.Locator("#gl-resource-count")).ToHaveTextAsync("1");
        await manager.Locator("[data-panel=resources]").ClickAsync();
        await manager.Locator("#gl-resources [data-gl-action=refresh]").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-resources")).ToContainTextAsync(note.Title);
        await manager.Locator("#gl-resources a").ClickAsync();
        await Assertions.Expect(manager.Locator("article h1")).ToHaveTextAsync(note.Title);
        await Assertions.Expect(manager.Locator("article")).ToContainTextAsync("Chỉ đọc");
        await manager.GotoAsync($"https://localhost/Groups/{f.GroupId}");
        await manager.Locator("[data-panel=resources]").ClickAsync();
        await manager.Locator("[data-gl-action=resource-unshare]").ClickAsync();
        await manager.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-resources")).ToContainTextAsync("Chưa có tài nguyên chung");
        await Assertions.Expect(manager.Locator("#gl-resource-count")).ToHaveTextAsync("0");
        await member.Locator("#gl-resources [data-gl-action=refresh]").ClickAsync();
        await Assertions.Expect(member.Locator("#gl-resources")).ToContainTextAsync("Chưa có tài nguyên chung");
        await using (var db = f.NewDb())
        {
            var saved = await db.Notes.FindAsync(note.NoteId);
            Assert.Equal("Private", saved!.Visibility);
            Assert.Null(saved.StudyGroupId);
        }

        await manager.Locator("[data-panel=progress]").ClickAsync();
        await manager.Locator("[data-gl-action=goal]").ClickAsync();
        await manager.Locator("[name=title]").FillAsync("Mục tiêu tuần");
        await manager.Locator("[name=sessions]").FillAsync("2");
        await manager.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-progress")).ToContainTextAsync("Mục tiêu tuần");
        var currentWeek = await manager.Locator("#gl-week").InputValueAsync();
        var nextWeek = DateOnly.Parse(currentWeek).AddDays(7).ToString("yyyy-MM-dd");
        await manager.Locator("#gl-week").FillAsync(nextWeek);
        await manager.Locator("#gl-week").DispatchEventAsync("change");
        await Assertions.Expect(manager.Locator("#gl-progress")).ToContainTextAsync("Tuần này chưa đặt mục tiêu");
        await manager.Locator("#gl-week").FillAsync(currentWeek);
        await manager.Locator("#gl-week").DispatchEventAsync("change");
        await Assertions.Expect(manager.Locator("#gl-progress")).ToContainTextAsync("Mục tiêu tuần");
        await member.Locator("[data-panel=progress]").ClickAsync();
        await member.Locator("[data-panel-content=progress] [data-gl-action=refresh]").ClickAsync();
        await Assertions.Expect(member.Locator("#gl-progress")).ToContainTextAsync("Mục tiêu tuần");
        await Assertions.Expect(member.Locator("#gl-progress")).ToContainTextAsync("Tiến độ của bạn");
        await Assertions.Expect(member.Locator("[data-gl-action=goal]")).ToHaveCountAsync(0);
        await manager.SetViewportSizeAsync(390, 844);
        await manager.Locator("[data-panel=sessions]").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-sessions")).ToBeVisibleAsync();
        var artifact = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../artifacts/group-learning-mobile.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
        await manager.ScreenshotAsync(new PageScreenshotOptions { Path = artifact, FullPage = true });
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Failed_initial_load_can_be_retried_without_javascript_errors()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        using var client = await f.Client(f.OwnerId);
        var errors = new List<string>();
        var page = await OpenPage(browser, client, errors, failFirstLoad: true);
        foreach (var panel in new[] { "sessions", "assignments", "questions", "resources", "progress" })
            await Assertions.Expect(page.Locator($"#gl-{panel}")).ToContainTextAsync("Dữ liệu tạm thời không khả dụng");
        await page.Locator("[data-panel=sessions]").ClickAsync();
        await page.Locator("#gl-sessions [data-gl-action=refresh]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-gl-action=session-new]")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#gl-resources")).ToContainTextAsync("Chia sẻ tài nguyên");
        await Assertions.Expect(page.Locator("#gl-progress")).ToContainTextAsync("Tỷ lệ hoàn thành");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("gl-resources")]
    [InlineData("gl-resource-count")]
    public async Task Progress_loads_even_when_resource_markup_is_missing(string missingElement)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        using var client = await f.Client(f.OwnerId);
        var errors = new List<string>();
        var page = await OpenPage(browser, client, errors, missingElement: missingElement);
        await page.Locator("[data-panel=progress]").ClickAsync();
        await Assertions.Expect(page.Locator("#gl-progress")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#gl-progress")).ToContainTextAsync("Tỷ lệ hoàn thành");
        await Assertions.Expect(page.Locator("[data-gl-action=goal]")).ToBeVisibleAsync();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Progress_shows_retry_when_loading_times_out_and_recovers()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        using var client = await f.Client(f.OwnerId);
        var errors = new List<string>();
        // Simulate a request that never responds; the real loader must abort it.
        var page = await OpenPage(browser, client, errors, initScript: """
            const originalFetch = window.fetch;
            window.fetch = function(url, options) {
              if (String(url).includes('/learning')) {
                window.fetch = originalFetch;
                return new Promise((resolve, reject) => {
                  options.signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')), { once: true });
                });
              }
              return originalFetch.call(this, url, options);
            };
            """);
        await page.Locator("[data-panel=progress]").ClickAsync();
        await Assertions.Expect(page.Locator("#gl-progress")).ToContainTextAsync("Tải dữ liệu quá lâu", new() { Timeout = 20000 });
        await page.Locator("#gl-progress [data-gl-action=refresh]").ClickAsync();
        await Assertions.Expect(page.Locator("#gl-progress")).ToContainTextAsync("Tỷ lệ hoàn thành");
        Assert.Empty(errors);
    }

    private async Task<IPage> OpenPage(IBrowser browser, HttpClient client, List<string> errors, bool failFirstLoad = false,
        string? missingElement = null, string? initScript = null)
    {
        var context = await browser.NewContextAsync();
        if (initScript != null) await context.AddInitScriptAsync(initScript);
        // Proxy real browser requests to TestServer: cookies/antiforgery and production routes remain active.
        await context.RouteAsync("**/*", async route =>
        {
            if (new Uri(route.Request.Url).Host != "localhost")
            {
                // Load the app's real CSS/fonts/Bootstrap; use its built-in REST fallback for chat.
                if (route.Request.Url.Contains("signalr", StringComparison.OrdinalIgnoreCase)) await route.AbortAsync();
                else await route.ContinueAsync();
                return;
            }
            if (failFirstLoad && route.Request.Method == "GET" && new Uri(route.Request.Url).AbsolutePath == $"/api/groups/{f.GroupId}/learning")
            {
                failFirstLoad = false;
                await route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{\"message\":\"Dữ liệu tạm thời không khả dụng\"}" });
                return;
            }
            using var message = new HttpRequestMessage(new HttpMethod(route.Request.Method), route.Request.Url);
            if (route.Request.PostDataBuffer != null) message.Content = new ByteArrayContent(route.Request.PostDataBuffer);
            foreach (var header in route.Request.Headers)
            {
                if (header.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase) && message.Content != null)
                    message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(header.Value);
                else if (!header.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase)) message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            using var response = await client.SendAsync(message);
            var body = await response.Content.ReadAsByteArrayAsync();
            if (missingElement != null && response.Content.Headers.ContentType?.MediaType == "text/html")
                body = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(body)
                    .Replace($"id=\"{missingElement}\"", $"id=\"{missingElement}-missing\""));
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                BodyBytes = body
            });
        });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync($"https://localhost/Groups/{f.GroupId}");
        return page;
    }
}

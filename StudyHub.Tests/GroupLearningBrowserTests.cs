using System.Net.Http.Headers;
using Microsoft.Playwright;
using Xunit;

namespace StudyHub.Tests;

public sealed class GroupLearningBrowserTests(GroupLearningFixture f) : IClassFixture<GroupLearningFixture>
{
    [Fact]
    public async Task Manager_and_member_can_complete_learning_workflows_in_browser()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = true });
        using var ownerClient = await f.Client(f.OwnerId);
        using var memberClient = await f.Client(f.MemberId);
        var errors = new List<string>();
        var manager = await OpenPage(browser, ownerClient, errors);
        var member = await OpenPage(browser, memberClient, errors);
        await Assertions.Expect(manager.Locator("#gl-sessions")).ToContainTextAsync("Chưa có buổi học");
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

        await manager.Locator("[data-panel=progress]").ClickAsync();
        await manager.Locator("[data-gl-action=goal]").ClickAsync();
        await manager.Locator("[name=title]").FillAsync("Mục tiêu tuần");
        await manager.Locator("[name=sessions]").FillAsync("2");
        await manager.Locator("#gl-dialog-save").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-progress")).ToContainTextAsync("Mục tiêu tuần");
        await manager.SetViewportSizeAsync(390, 844);
        await manager.Locator("[data-panel=sessions]").ClickAsync();
        await Assertions.Expect(manager.Locator("#gl-sessions")).ToBeVisibleAsync();
        var artifact = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../artifacts/group-learning-mobile.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
        await manager.ScreenshotAsync(new PageScreenshotOptions { Path = artifact, FullPage = true });
        Assert.Empty(errors);
    }

    private async Task<IPage> OpenPage(IBrowser browser, HttpClient client, List<string> errors)
    {
        var context = await browser.NewContextAsync();
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
            using var message = new HttpRequestMessage(new HttpMethod(route.Request.Method), route.Request.Url);
            if (route.Request.PostDataBuffer != null) message.Content = new ByteArrayContent(route.Request.PostDataBuffer);
            foreach (var header in route.Request.Headers)
            {
                if (header.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase) && message.Content != null)
                    message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(header.Value);
                else if (!header.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase)) message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            using var response = await client.SendAsync(message);
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                BodyBytes = await response.Content.ReadAsByteArrayAsync()
            });
        });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync($"https://localhost/Groups/{f.GroupId}");
        return page;
    }
}

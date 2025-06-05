using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace TestProject1
{
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class PomodoroHappyPathTests : PageTest
    {
        [Test]
        public async Task StartPauseRecord_BasicFlow_ShowsTimerChanges()
        {
            // 1) Navigate to your Pomodoro site (local or Azure).
            //    If local: "https://localhost:44330" (adjust port).
            //    If Azure: "https://myapp.azurewebsites.net"
            await Page.GotoAsync("https://localhost:44330/");

            // 2) Optional: Check the title or heading
            //    Suppose your page title is "Pomodoro-Style Timer"
            await Expect(Page).ToHaveTitleAsync(new Regex("Timer - WebApplication1"));

            // 3) Find the timer display & verify it starts at "00:00"
            var display = Page.Locator("#timer-display");
            await Expect(display).ToHaveTextAsync("00:00");

            // 4) Click "Start"
            var startBtn = Page.Locator("#start-btn");
            await startBtn.ClickAsync();

            // 5) Wait a bit so the timer increments (e.g. 2 seconds)
            await Page.WaitForTimeoutAsync(2000);

            // 6) Check the timer is no longer "00:00" (it should have progressed)
            var currentTime = await display.InnerTextAsync();
            Assert.AreNotEqual("00:00", currentTime,
                "Expected the timer to change after starting.");

            // 7) Click "Pause"
            var pauseBtn = Page.Locator("#pause-btn");
            await pauseBtn.ClickAsync();

            // 8) Click "Record" to finalize partial usage
            var recordBtn = Page.Locator("#record-btn");
            await recordBtn.ClickAsync();

            // 9) Possibly confirm some success message or updated chart:
            //    e.g., if you show "Sessions recorded successfully."
            //    This is optional; depends on your UI.
            //    await Expect(Page.Locator("text=Sessions recorded successfully."))
            //        .ToBeVisibleAsync();

            // 10) (Optional) Wait for chart or today's volume to update
            //    E.g., check if #improvement-percentage or #today-job-volume changed
            //    This depends on your logic
            //    await Page.WaitForTimeoutAsync(1000);
            //    var jobVolume = await Page.Locator("#today-job-volume").InnerTextAsync();
            //    Assert.AreNotEqual("0", jobVolume);

            // If we reach here with no exceptions, the “happy path” scenario succeeded!
        }
    }
}

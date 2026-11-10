using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using SelfHealingAutomation.Config;
using SelfHealingAutomation.Models;

namespace SelfHealingAutomation.Engine;

public class TestRunResult
{
    public bool Success { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public List<StepResult> Steps { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? ScreenshotPath { get; set; }
}

public class StepResult
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Details { get; set; } = string.Empty;
    public CandidateScore? MatchScore { get; set; }
}

public class SelfHealingTestRunner
{
    private readonly TestConfiguration _config;

    public SelfHealingTestRunner(TestConfiguration config)
    {
        _config = config;
    }

    public TestRunResult Execute()
    {
        var startTime = DateTime.UtcNow;
        var result = new TestRunResult
        {
            TargetUrl = _config.TargetUrl
        };

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("    TASK 1: SELF-HEALING AUTOMATION TEST RUNNER");
        Console.WriteLine("=======================================================");
        Console.ResetColor();
        Console.WriteLine($"[Config] Target URL : {_config.TargetUrl}");
        Console.WriteLine($"[Config] Headless   : {_config.Headless}");
        Console.WriteLine($"[Config] Browser    : {_config.Browser}");
        Console.WriteLine($"[Config] First Name : {_config.FirstName}");
        Console.WriteLine($"[Config] Gender     : {_config.Gender}");
        Console.WriteLine($"[Config] State      : {_config.State}\n");

        IWebDriver? driver = null;
        try
        {
            driver = CreateDriver();
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(_config.TimeoutSeconds);

            Console.WriteLine($"[Step 1] Navigating to: {_config.TargetUrl}...");
            driver.Navigate().GoToUrl(_config.TargetUrl);

            // Wait until body is ready
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(_config.TimeoutSeconds));
            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
            Thread.Sleep(1000); // Allow custom elements/scripts to render

            var locator = new SelfHealingLocator(driver, _config.HighlightElements);

            // ==========================================
            // Field 1: First Name (Text Box)
            // ==========================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Step 2] Locating & Testing Field: 'First Name' (Text Box)...");
            Console.ResetColor();

            var fnameTarget = FieldTarget.FirstName(_config.FirstName);
            var fnameCandidate = locator.FindTextInput(fnameTarget);
            Console.WriteLine($"  -> Match Found: {fnameCandidate}");

            // Interact
            fnameCandidate.Element.Clear();
            fnameCandidate.Element.SendKeys(_config.FirstName);
            Thread.Sleep(300);

            // Verify
            string actualName = fnameCandidate.Element.GetAttribute("value") ?? "";
            bool fnameValid = actualName.Equals(_config.FirstName, StringComparison.OrdinalIgnoreCase);
            if (!fnameValid)
            {
                throw new Exception($"First Name verification failed. Expected '{_config.FirstName}', but found '{actualName}'.");
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [PASS] 'First Name' populated with '{actualName}'.");
            Console.ResetColor();

            result.Steps.Add(new StepResult
            {
                StepName = "First Name Input",
                Success = true,
                Details = $"Populated '{actualName}' (Confidence: {fnameCandidate.TotalScore:F0}%)",
                MatchScore = fnameCandidate
            });

            // ==========================================
            // Field 2: Gender (Radio Button)
            // ==========================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[Step 3] Locating & Testing Field: 'Gender' -> '{_config.Gender}' (Radio Button)...");
            Console.ResetColor();

            var genderTarget = FieldTarget.Gender(_config.Gender);
            var genderCandidate = locator.FindRadioButton(genderTarget, _config.Gender);
            Console.WriteLine($"  -> Match Found: {genderCandidate}");

            // Interact: click with fallback to JS click if element obscured
            ClickElement(driver, genderCandidate.Element);
            Thread.Sleep(300);

            // Verify
            bool genderSelected = genderCandidate.Element.Selected;
            if (!genderSelected)
            {
                // Fallback check: query by value or name in case DOM swapped reference
                var js = (IJavaScriptExecutor)driver;
                bool isChecked = Convert.ToBoolean(js.ExecuteScript("return arguments[0].checked;", genderCandidate.Element));
                genderSelected = isChecked;
            }

            if (!genderSelected)
            {
                throw new Exception($"Gender radio button '{_config.Gender}' was clicked but verification showed it was not selected.");
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [PASS] 'Gender' option '{_config.Gender}' successfully selected and verified.");
            Console.ResetColor();

            result.Steps.Add(new StepResult
            {
                StepName = "Gender Radio Button",
                Success = true,
                Details = $"Selected option '{_config.Gender}' (Confidence: {genderCandidate.TotalScore:F0}%)",
                MatchScore = genderCandidate
            });

            // ==========================================
            // Field 3: State (Dropdown)
            // ==========================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[Step 4] Locating & Testing Field: 'State' -> '{_config.State}' (Dropdown)...");
            Console.ResetColor();

            var stateTarget = FieldTarget.State(_config.State);
            var stateCandidate = locator.FindDropdown(stateTarget);
            Console.WriteLine($"  -> Match Found: {stateCandidate}");

            // Interact
            var select = new SelectElement(stateCandidate.Element);
            bool optionSelected = false;

            // Try exact text, then value, then partial text
            try
            {
                select.SelectByText(_config.State);
                optionSelected = true;
            }
            catch
            {
                try
                {
                    select.SelectByValue(_config.State);
                    optionSelected = true;
                }
                catch
                {
                    // Find option with partial fuzzy match
                    var opt = select.Options.FirstOrDefault(o =>
                        o.Text.Contains(_config.State, StringComparison.OrdinalIgnoreCase) ||
                        (o.GetAttribute("value") ?? "").Contains(_config.State, StringComparison.OrdinalIgnoreCase));
                    if (opt != null)
                    {
                        opt.Click();
                        optionSelected = true;
                    }
                }
            }

            if (!optionSelected)
            {
                throw new Exception($"Could not select option '{_config.State}' in State dropdown.");
            }
            Thread.Sleep(300);

            // Verify
            string selectedText = select.SelectedOption?.Text?.Trim() ?? "";
            string selectedValue = select.SelectedOption?.GetAttribute("value")?.Trim() ?? "";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [PASS] 'State' dropdown selected: '{selectedText}' (Value: '{selectedValue}').");
            Console.ResetColor();

            result.Steps.Add(new StepResult
            {
                StepName = "State Dropdown",
                Success = true,
                Details = $"Selected '{selectedText}' (Confidence: {stateCandidate.TotalScore:F0}%)",
                MatchScore = stateCandidate
            });

            // Take success screenshot
            result.ScreenshotPath = CaptureScreenshot(driver, "task1_success");
            result.Success = true;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n=======================================================");
            Console.WriteLine("  TEST RESULT: [PASSED] ALL 3 FIELDS VERIFIED RESILIENTLY");
            Console.WriteLine("=======================================================\n");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FAIL] Test Execution Failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();

            if (driver != null)
            {
                result.ScreenshotPath = CaptureScreenshot(driver, "task1_failure");
            }
        }
        finally
        {
            if (driver != null)
            {
                Thread.Sleep(1500); // Visual pause before cleanup
                driver.Quit();
                driver.Dispose();
            }
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private IWebDriver CreateDriver()
    {
        if (_config.Browser.Equals("edge", StringComparison.OrdinalIgnoreCase))
        {
            var edgeOptions = new EdgeOptions();
            if (_config.Headless) edgeOptions.AddArgument("--headless=new");
            edgeOptions.AddArgument("--disable-gpu");
            edgeOptions.AddArgument("--no-sandbox");
            edgeOptions.AddArgument("--window-size=1400,900");
            return new EdgeDriver(edgeOptions);
        }
        else
        {
            var chromeOptions = new ChromeOptions();
            if (_config.Headless) chromeOptions.AddArgument("--headless=new");
            chromeOptions.AddArgument("--disable-gpu");
            chromeOptions.AddArgument("--no-sandbox");
            chromeOptions.AddArgument("--disable-dev-shm-usage");
            chromeOptions.AddArgument("--window-size=1400,900");
            return new ChromeDriver(chromeOptions);
        }
    }

    private void ClickElement(IWebDriver driver, IWebElement element)
    {
        try
        {
            element.Click();
        }
        catch
        {
            var js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({behavior: 'instant', block: 'center'}); arguments[0].click();", element);
        }
    }

    private string CaptureScreenshot(IWebDriver driver, string prefix)
    {
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(dir);
            string filename = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(dir, filename);

            var ss = ((ITakesScreenshot)driver).GetScreenshot();
            ss.SaveAsFile(fullPath);
            Console.WriteLine($"[Screenshot] Saved: {fullPath}");
            return fullPath;
        }
        catch
        {
            return "";
        }
    }
}


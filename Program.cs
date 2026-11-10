using Microsoft.Extensions.Configuration;
using SelfHealingAutomation.Config;
using SelfHealingAutomation.Engine;

namespace SelfHealingAutomation;

public class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Build Configuration from appsettings.json and Command Line Args
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddCommandLine(args);

        var configRoot = builder.Build();
        var config = new TestConfiguration();

        // Bind Automation section if present
        var section = configRoot.GetSection("Automation");
        if (section.Exists())
        {
            section.Bind(config);
        }

        // Override with explicit command-line flags if provided
        if (!string.IsNullOrEmpty(configRoot["url"])) config.TargetUrl = configRoot["url"]!;
        if (!string.IsNullOrEmpty(configRoot["headless"])) config.Headless = bool.Parse(configRoot["headless"]!);
        if (!string.IsNullOrEmpty(configRoot["browser"])) config.Browser = configRoot["browser"]!;
        if (!string.IsNullOrEmpty(configRoot["firstName"])) config.FirstName = configRoot["firstName"]!;
        if (!string.IsNullOrEmpty(configRoot["gender"])) config.Gender = configRoot["gender"]!;
        if (!string.IsNullOrEmpty(configRoot["state"])) config.State = configRoot["state"]!;

        bool runAllVariations = args.Contains("--test-variations") || args.Contains("--demo-all");

        if (runAllVariations)
        {
            return RunVariationsSuite(config);
        }

        // Standard Single Run
        var runner = new SelfHealingTestRunner(config);
        var result = runner.Execute();

        PrintSummary(new[] { result });
        return result.Success ? 0 : 1;
    }

    private static int RunVariationsSuite(TestConfiguration baseConfig)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("\n==================================================================");
        Console.WriteLine(" RUNNING RESILIENCE & CHAOS SUITE: TESTING ALTERED DOM VARIATIONS");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        string baseDir = AppContext.BaseDirectory;
        string testPagesDir = Path.Combine(baseDir, "test_pages");

        var testTargets = new List<(string Name, string Url)>
        {
            ("Original Live CloudQA Page", baseConfig.TargetUrl),
            ("Variation 1: Reordered Fields & Altered CSS/IDs", GetFileUrl(Path.Combine(testPagesDir, "variation1_reordered_ids.html"))),
            ("Variation 2: Table-Based Layout with ARIA & No Matching Classes", GetFileUrl(Path.Combine(testPagesDir, "variation2_table_aria.html"))),
            ("Variation 3: Shadow DOM & Web Component Encapsulation", GetFileUrl(Path.Combine(testPagesDir, "variation3_shadow_dom.html")))
        };

        var results = new List<TestRunResult>();
        foreach (var target in testTargets)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n>>> Executing Test Target: {target.Name}");
            Console.ResetColor();

            var variantConfig = new TestConfiguration
            {
                TargetUrl = target.Url,
                Headless = baseConfig.Headless,
                Browser = baseConfig.Browser,
                FirstName = baseConfig.FirstName,
                Gender = baseConfig.Gender,
                State = baseConfig.State,
                TimeoutSeconds = baseConfig.TimeoutSeconds,
                HighlightElements = baseConfig.HighlightElements
            };

            var runner = new SelfHealingTestRunner(variantConfig);
            results.Add(runner.Execute());
        }

        PrintSummary(results);
        return results.All(r => r.Success) ? 0 : 1;
    }

    private static string GetFileUrl(string path)
    {
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private static void PrintSummary(IEnumerable<TestRunResult> results)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("                  OVERALL TEST REPORT                  ");
        Console.WriteLine("=======================================================");
        Console.ResetColor();

        int passed = 0;
        int failed = 0;

        foreach (var r in results)
        {
            if (r.Success)
            {
                passed++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  [PASS] ");
            }
            else
            {
                failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  [FAIL] ");
            }

            Console.ResetColor();
            Console.WriteLine($"{r.TargetUrl} ({r.Duration.TotalSeconds:F2}s)");
            foreach (var step in r.Steps)
            {
                Console.WriteLine($"         - {step.StepName}: {step.Details}");
            }
            if (!string.IsNullOrEmpty(r.ErrorMessage))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"         Error: {r.ErrorMessage}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("-------------------------------------------------------");
        Console.WriteLine($"Total: {passed + failed} | Passed: {passed} | Failed: {failed}");
        Console.WriteLine("=======================================================\n");
    }
}

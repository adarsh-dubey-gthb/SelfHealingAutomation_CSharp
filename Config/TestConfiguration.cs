namespace SelfHealingAutomation.Config;

public class TestConfiguration
{
    public string TargetUrl { get; set; } = "https://app.cloudqa.io/home/AutomationPracticeForm";
    public bool Headless { get; set; } = false;
    public string Browser { get; set; } = "chrome";
    public int TimeoutSeconds { get; set; } = 15;
    public bool HighlightElements { get; set; } = true;
    public string FirstName { get; set; } = "Adarsh";
    public string Gender { get; set; } = "Male";
    public string State { get; set; } = "United States";
}


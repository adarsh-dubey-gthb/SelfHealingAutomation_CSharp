using OpenQA.Selenium;

namespace SelfHealingAutomation.Models;

public class CandidateScore
{
    public IWebElement Element { get; set; } = null!;
    public double TotalScore { get; set; }
    public string StrategyUsed { get; set; } = string.Empty;
    public string MatchedDetails { get; set; } = string.Empty;
    public string ElementDescription { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"[Score: {TotalScore:F1}/100 | Strategy: {StrategyUsed}] -> {ElementDescription} ({MatchedDetails})";
    }
}


using System.Drawing;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SelfHealingAutomation.Models;

namespace SelfHealingAutomation.Engine;

public class SelfHealingLocator
{
    private readonly IWebDriver _driver;
    private readonly IJavaScriptExecutor _js;
    private readonly bool _highlight;

    public SelfHealingLocator(IWebDriver driver, bool highlight = true)
    {
        _driver = driver;
        _js = (IJavaScriptExecutor)driver;
        _highlight = highlight;
    }

    /// <summary>
    /// Resiliently finds a text input field for the specified target.
    /// Resists changes in ID, class name, DOM order, nesting, and label repositioning.
    /// </summary>
    public CandidateScore FindTextInput(FieldTarget target)
    {
        var candidates = GetVisibleInputs("input, textarea");
        var scores = new List<CandidateScore>();

        foreach (var el in candidates)
        {
            string type = (el.GetAttribute("type") ?? "text").ToLowerInvariant();
            if (type is "hidden" or "submit" or "button" or "radio" or "checkbox" or "image" or "reset" or "file")
                continue;

            double score = 0;
            var matchReasons = new List<string>();

            string elId = el.GetAttribute("id") ?? "";
            string elName = el.GetAttribute("name") ?? "";
            string elPlaceholder = el.GetAttribute("placeholder") ?? "";
            string elAriaLabel = el.GetAttribute("aria-label") ?? "";
            string elTitle = el.GetAttribute("title") ?? "";
            string elAutocomplete = el.GetAttribute("autocomplete") ?? "";

            // 1. Explicit Label Association: <label for="...">
            if (!string.IsNullOrEmpty(elId))
            {
                var explicitLabels = _driver.FindElements(By.CssSelector($"label[for='{elId}']"));
                foreach (var lbl in explicitLabels)
                {
                    double lblSim = ScoreKeywordMatch(lbl.Text, target.PrimaryKeywords, target.SecondaryKeywords);
                    if (lblSim > 0.5)
                    {
                        score += 50 * lblSim;
                        matchReasons.Add($"Explicit label[for='{elId}'] match ('{lbl.Text}')");
                    }
                }
            }

            // 2. Parent Container & Proximity Association
            var parentContainers = GetParentContainers(el, levels: 3);
            foreach (var container in parentContainers)
            {
                var containerLabels = container.FindElements(By.CssSelector("label, .control-label, .form-label, span.label, th, p, strong"));
                foreach (var lbl in containerLabels)
                {
                    // Avoid matching text of self
                    if (lbl.Equals(el)) continue;

                    double lblSim = ScoreKeywordMatch(lbl.Text, target.PrimaryKeywords, target.SecondaryKeywords);
                    if (lblSim > 0.5)
                    {
                        score += 40 * lblSim;
                        matchReasons.Add($"Container label match ('{lbl.Text.Trim()}')");
                        break;
                    }
                }

                // If container contains explicit negative keyword (e.g. "Last Name" when searching for "First Name")
                string containerText = container.Text.ToLowerInvariant();
                if (target.Name.Equals("First Name", StringComparison.OrdinalIgnoreCase))
                {
                    if (containerText.Contains("last name") || containerText.Contains("lname"))
                    {
                        // Penalize heavily so First Name never mistakenly picks Last Name
                        score -= 60;
                        matchReasons.Add("Penalized: Container mentions Last Name");
                    }
                }
            }

            // 3. Direct Attribute Semantic Matching (fuzzy & token)
            double attrScore = 0;
            string bestAttr = "";
            foreach (var kw in target.PrimaryKeywords)
            {
                if (HeuristicScorer.CalculateSimilarity(elName, kw) > 0.6) { attrScore = Math.Max(attrScore, 35); bestAttr = $"name='{elName}'"; }
                if (HeuristicScorer.CalculateSimilarity(elId, kw) > 0.6) { attrScore = Math.Max(attrScore, 35); bestAttr = $"id='{elId}'"; }
                if (HeuristicScorer.CalculateSimilarity(elPlaceholder, kw) > 0.6) { attrScore = Math.Max(attrScore, 30); bestAttr = $"placeholder='{elPlaceholder}'"; }
                if (HeuristicScorer.CalculateSimilarity(elAriaLabel, kw) > 0.6) { attrScore = Math.Max(attrScore, 35); bestAttr = $"aria-label='{elAriaLabel}'"; }
            }
            if (attrScore > 0)
            {
                score += attrScore;
                matchReasons.Add($"Attribute fuzzy match ({bestAttr})");
            }

            // Negative penalty if attribute explicitly indicates "last name" or "lname"
            if (target.Name.Equals("First Name", StringComparison.OrdinalIgnoreCase))
            {
                if (elName.Contains("last", StringComparison.OrdinalIgnoreCase) ||
                    elId.Contains("lname", StringComparison.OrdinalIgnoreCase) ||
                    elPlaceholder.Contains("last", StringComparison.OrdinalIgnoreCase))
                {
                    score -= 70;
                    matchReasons.Add("Penalized: Attributes indicate Last Name");
                }
            }

            // Cap score between 0 and 100
            double finalScore = Math.Clamp(score, 0, 100);
            if (finalScore > 20)
            {
                scores.Add(new CandidateScore
                {
                    Element = el,
                    TotalScore = finalScore,
                    StrategyUsed = matchReasons.FirstOrDefault() ?? "Semantic Fallback",
                    MatchedDetails = string.Join(" | ", matchReasons),
                    ElementDescription = $"<{el.TagName} id='{elId}' name='{elName}' type='{type}'>"
                });
            }
        }

        var best = scores.OrderByDescending(s => s.TotalScore).FirstOrDefault();
        if (best == null)
        {
            throw new NoSuchElementException($"SelfHealingLocator could not locate a resilient match for field '{target.Name}'.");
        }

        if (_highlight) HighlightElement(best.Element);
        return best;
    }

    /// <summary>
    /// Resiliently finds the specific radio button option belonging to a group.
    /// Handles decoupled labels, custom spans, inverted DOM structures, and dynamic IDs.
    /// </summary>
    public CandidateScore FindRadioButton(FieldTarget target, string desiredOption)
    {
        var radios = GetVisibleInputs("input[type='radio']");
        var scores = new List<CandidateScore>();

        foreach (var radio in radios)
        {
            double score = 0;
            var matchReasons = new List<string>();

            string radioId = radio.GetAttribute("id") ?? "";
            string radioName = radio.GetAttribute("name") ?? "";
            string radioValue = radio.GetAttribute("value") ?? "";

            // Check if radio matches desired option
            double optionSim = HeuristicScorer.CalculateSimilarity(radioValue, desiredOption);
            if (optionSim > 0.6)
            {
                score += 40 * optionSim;
                matchReasons.Add($"Radio value match ('{radioValue}')");
            }

            // Check adjacent label or span text: <span name="male">Male</span> or <label>Male</label>
            string nearbyText = GetAdjacentText(radio);
            double nearbySim = HeuristicScorer.CalculateSimilarity(nearbyText, desiredOption);
            if (nearbySim > 0.6)
            {
                score += 45 * nearbySim;
                matchReasons.Add($"Adjacent label/span text match ('{nearbyText}')");
            }

            // Check group header: does enclosing container or preceding heading identify the group as "Gender"?
            var containers = GetParentContainers(radio, levels: 3);
            bool groupConfirmed = false;
            foreach (var container in containers)
            {
                string text = container.Text;
                if (ScoreKeywordMatch(text, target.PrimaryKeywords, target.SecondaryKeywords) > 0.5)
                {
                    groupConfirmed = true;
                    score += 25;
                    matchReasons.Add("Parent container confirms 'Gender' group");
                    break;
                }
            }

            // Also check name attribute (e.g. name="gender")
            if (ScoreKeywordMatch(radioName, target.PrimaryKeywords, target.SecondaryKeywords) > 0.5)
            {
                groupConfirmed = true;
                score += 20;
                matchReasons.Add($"Radio group name='{radioName}' confirms target");
            }

            // If it belongs to the group and matches desired option
            if (groupConfirmed && (optionSim > 0.5 || nearbySim > 0.5 || radioId.Contains(desiredOption, StringComparison.OrdinalIgnoreCase)))
            {
                double finalScore = Math.Clamp(score, 0, 100);
                scores.Add(new CandidateScore
                {
                    Element = radio,
                    TotalScore = finalScore,
                    StrategyUsed = matchReasons.FirstOrDefault() ?? "Radio Option Match",
                    MatchedDetails = string.Join(" | ", matchReasons),
                    ElementDescription = $"<input type='radio' id='{radioId}' value='{radioValue}' label='{nearbyText}'>"
                });
            }
        }

        var best = scores.OrderByDescending(s => s.TotalScore).FirstOrDefault();
        if (best == null)
        {
            throw new NoSuchElementException($"SelfHealingLocator could not locate radio button for '{target.Name}' option '{desiredOption}'.");
        }

        if (_highlight) HighlightElement(best.Element);
        return best;
    }

    /// <summary>
    /// Resiliently finds a dropdown select element (or custom select).
    /// Handles moved labels, structural redesigns, and options inspection.
    /// </summary>
    public CandidateScore FindDropdown(FieldTarget target)
    {
        var selects = GetVisibleInputs("select, [role='combobox']");
        var scores = new List<CandidateScore>();

        foreach (var el in selects)
        {
            double score = 0;
            var matchReasons = new List<string>();

            string elId = el.GetAttribute("id") ?? "";
            string elName = el.GetAttribute("name") ?? "";
            string elAriaLabel = el.GetAttribute("aria-label") ?? "";

            // 1. Explicit label match
            if (!string.IsNullOrEmpty(elId))
            {
                var labels = _driver.FindElements(By.CssSelector($"label[for='{elId}']"));
                foreach (var lbl in labels)
                {
                    double sim = ScoreKeywordMatch(lbl.Text, target.PrimaryKeywords, target.SecondaryKeywords);
                    if (sim > 0.5)
                    {
                        score += 50 * sim;
                        matchReasons.Add($"Explicit label[for='{elId}'] match ('{lbl.Text.Trim()}')");
                    }
                }
            }

            // 2. Parent container label
            var containers = GetParentContainers(el, levels: 3);
            foreach (var container in containers)
            {
                var labels = container.FindElements(By.CssSelector("label, .control-label, span.label, p, strong"));
                foreach (var lbl in labels)
                {
                    if (lbl.Equals(el)) continue;
                    double sim = ScoreKeywordMatch(lbl.Text, target.PrimaryKeywords, target.SecondaryKeywords);
                    if (sim > 0.5)
                    {
                        score += 40 * sim;
                        matchReasons.Add($"Container label match ('{lbl.Text.Trim()}')");
                        break;
                    }
                }
            }

            // 3. Direct attribute fuzzy match
            foreach (var kw in target.PrimaryKeywords)
            {
                if (HeuristicScorer.CalculateSimilarity(elName, kw) > 0.6) { score += 30; matchReasons.Add($"name='{elName}' match"); break; }
                if (HeuristicScorer.CalculateSimilarity(elId, kw) > 0.6) { score += 30; matchReasons.Add($"id='{elId}' match"); break; }
                if (HeuristicScorer.CalculateSimilarity(elAriaLabel, kw) > 0.6) { score += 30; matchReasons.Add($"aria-label='{elAriaLabel}' match"); break; }
            }

            // 4. Option Content Verification: Does this select contain target option (e.g. "United States" or states/countries)?
            if (el.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
            {
                var selectElement = new SelectElement(el);
                bool hasTargetOption = selectElement.Options.Any(o =>
                    o.Text.Contains(target.DesiredValue, StringComparison.OrdinalIgnoreCase) ||
                    (o.GetAttribute("value") ?? "").Contains(target.DesiredValue, StringComparison.OrdinalIgnoreCase));

                if (hasTargetOption)
                {
                    score += 35;
                    matchReasons.Add($"Contains target option '{target.DesiredValue}'");
                }
            }

            double finalScore = Math.Clamp(score, 0, 100);
            if (finalScore > 20)
            {
                scores.Add(new CandidateScore
                {
                    Element = el,
                    TotalScore = finalScore,
                    StrategyUsed = matchReasons.FirstOrDefault() ?? "Dropdown Fallback",
                    MatchedDetails = string.Join(" | ", matchReasons),
                    ElementDescription = $"<{el.TagName} id='{elId}' name='{elName}'>"
                });
            }
        }

        var best = scores.OrderByDescending(s => s.TotalScore).FirstOrDefault();
        if (best == null)
        {
            throw new NoSuchElementException($"SelfHealingLocator could not locate dropdown for '{target.Name}'.");
        }

        if (_highlight) HighlightElement(best.Element);
        return best;
    }

    /// <summary>
    /// Pierces open shadow roots and retrieves all visible inputs matching selector across the entire DOM tree.
    /// </summary>
    private List<IWebElement> GetVisibleInputs(string selector)
    {
        // Execute JS to traverse standard DOM and pierced shadow roots
        string jsScript = @"
            const selector = arguments[0];
            const results = [];
            
            function search(root) {
                if (!root) return;
                const items = root.querySelectorAll(selector);
                for (let el of items) {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    if (style.display !== 'none' && style.visibility !== 'hidden' && (rect.width > 0 || rect.height > 0 || el.type === 'radio')) {
                        results.push(el);
                    }
                }
                const allElements = root.querySelectorAll('*');
                for (let el of allElements) {
                    if (el.shadowRoot) {
                        search(el.shadowRoot);
                    }
                }
            }

            search(document);
            return results;
        ";

        try
        {
            var rawList = _js.ExecuteScript(jsScript, selector) as IReadOnlyCollection<IWebElement>;
            if (rawList != null && rawList.Count > 0)
            {
                return rawList.ToList();
            }
        }
        catch
        {
            // Fallback to standard driver search if JS execution encounters restriction
        }

        return _driver.FindElements(By.CssSelector(selector)).Where(e => e.Displayed).ToList();
    }

    private List<IWebElement> GetParentContainers(IWebElement el, int levels = 3)
    {
        var containers = new List<IWebElement>();
        IWebElement current = el;
        for (int i = 0; i < levels; i++)
        {
            try
            {
                var parent = current.FindElement(By.XPath(".."));
                if (parent == null || parent.TagName.Equals("body", StringComparison.OrdinalIgnoreCase))
                    break;
                containers.Add(parent);
                current = parent;
            }
            catch
            {
                break;
            }
        }
        return containers;
    }

    private string GetAdjacentText(IWebElement element)
    {
        try
        {
            // Sibling span, label or parent text
            var parent = element.FindElement(By.XPath(".."));
            var siblings = parent.FindElements(By.XPath("./following-sibling::*[1] | ./span | ./label"));
            foreach (var s in siblings)
            {
                if (!string.IsNullOrWhiteSpace(s.Text)) return s.Text.Trim();
            }
            return parent.Text.Trim();
        }
        catch
        {
            return "";
        }
    }

    private double ScoreKeywordMatch(string text, string[] primaryKeywords, string[] secondaryKeywords)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        text = text.ToLowerInvariant();

        foreach (var pk in primaryKeywords)
        {
            if (text.Contains(pk)) return 1.0;
            double sim = HeuristicScorer.CalculateSimilarity(text, pk);
            if (sim > 0.7) return sim;
        }

        foreach (var sk in secondaryKeywords)
        {
            if (text.Contains(sk)) return 0.7;
        }

        return 0;
    }

    private void HighlightElement(IWebElement element)
    {
        try
        {
            _js.ExecuteScript(
                "arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});" +
                "const origBorder = arguments[0].style.border;" +
                "const origBoxShadow = arguments[0].style.boxShadow;" +
                "arguments[0].style.border = '3px solid #10b981';" +
                "arguments[0].style.boxShadow = '0 0 12px #10b981';" +
                "setTimeout(() => {" +
                "   try { arguments[0].style.border = origBorder; arguments[0].style.boxShadow = origBoxShadow; } catch(e){}" +
                "}, 1200);", element);
        }
        catch
        {
            // Non-critical visual effect
        }
    }
}


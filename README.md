# Task 1: Resilient Self-Healing Web Automation (C# & Selenium)

## Overview
A resilient, self-healing automated testing program built in **C# (.NET 8)** and **Selenium WebDriver**. It automates and verifies testing of three key fields:
1. **First Name** (`<input type="text">`)
2. **Gender** (`<input type="radio">`)
3. **State** (`<select>` dropdown)

### The Problem It Solves
Traditional automation relies on brittle, static CSS selectors (`#fname`) or fixed XPath (`/html/body/div[2]/form/div[1]/input`). When developers refactor the page—reordering fields, modifying classes, changing IDs, switching to web components, or updating HTML structure—traditional tests break immediately.

This solution implements a **Weighted Multi-Factor Heuristic Locator Engine** that identifies the intended form controls semantically, contextually, and spatially. It keeps working reliably even under radical DOM modifications.

---

## Key Features

- **No Brittle Selectors**: Does not rely on hard-coded static XPaths or brittle IDs.
- **Weighted Multi-Factor Scoring**: Evaluates candidate elements across 5 heuristic dimensions:
  1. *Explicit Label Association* (`<label for="id">`)
  2. *Parent Container Proximity* (enclosing `.form-group`, `fieldset`, `<tr>`, `<div>`)
  3. *Semantic Attribute Fuzzy Matching* (`placeholder`, `aria-label`, `name`, `id`, `title`, `autocomplete`) using Levenshtein distance and token intersection (Jaccard similarity).
  4. *Radio Group Resolution*: Discovers parent option grouping and pairs desired values with adjacent `<span>` or `<label>` text.
  5. *Option Content Verification*: Inspects `<select>` dropdown children to confirm state/country contents.
- **Negative Penalty System**: Intelligently avoids similar-looking distractor fields (e.g., heavily penalizes "Last Name" when searching for "First Name").
- **Shadow DOM & Web Component Support**: Uses a deep DOM crawler to pierce open `#shadow-root` trees and `<slot>` elements.
- **Live Visual Feedback**: Automatically highlights located elements in real time with an animated green outline before interaction.
- **Fully Configurable**: Target URL, credentials, browser, and headless execution are configurable via `appsettings.json` or CLI parameters.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Google Chrome or Microsoft Edge installed on the machine

---

## Quick Start & Running

### 1. Run Against the Live CloudQA Practice Page
```bash
cd Task1_SelfHealingAutomation_CSharp
dotnet run
```
By default, this opens Google Chrome, navigates to `https://app.cloudqa.io/home/AutomationPracticeForm`, locates the three fields resiliently, inputs the values, asserts validation, and takes a timestamped screenshot.

### 2. Run in Headless Mode
```bash
dotnet run -- --headless true
```

### 3. Run Against a Custom or Modified Page URL
```bash
dotnet run -- --url "https://your-custom-test-page.com/form" --firstName "Jane" --gender "Female" --state "California"
```

### 4. Run the Chaos & Resilience Verification Suite
To demonstrate resilience against altered pages, this repository includes 3 radically modified DOM variations:
- **Variation 1**: Inverted field order (State -> Gender -> First Name) + randomized IDs (`id="rnd_txt_982"`) + changed classes.
- **Variation 2**: Table-based layout (`<table>`, `<th>`, `<td>`), ARIA attributes, no form-groups, no label-for linkage.
- **Variation 3**: Encapsulated Web Components with an open `#shadow-root` where standard XPath completely fails.

Run all 4 suites in one command:
```bash
dotnet run -- --headless true --demo-all
```

---

## Configuration (`appsettings.json`)

You can modify defaults directly in `appsettings.json`:
```json
{
  "Automation": {
    "TargetUrl": "https://app.cloudqa.io/home/AutomationPracticeForm",
    "Headless": false,
    "Browser": "chrome",
    "TimeoutSeconds": 15,
    "HighlightElements": true,
    "FirstName": "Adarsh",
    "Gender": "Male",
    "State": "United States"
  }
}
```

---

## Project Structure

```
Task1_SelfHealingAutomation_CSharp/
├── Config/
│   └── TestConfiguration.cs         # Configuration model and defaults
├── Engine/
│   ├── HeuristicScorer.cs           # Levenshtein & token similarity algorithms
│   ├── SelfHealingLocator.cs        # Multi-factor resilient locator engine
│   └── SelfHealingTestRunner.cs     # Execution workflow, interaction & assertions
├── Models/
│   ├── CandidateScore.cs            # Candidate element scoring and match reasons
│   └── FieldTarget.cs               # Field targets and semantic keywords
├── test_pages/
│   ├── variation1_reordered_ids.html # Inverted order & randomized IDs
│   ├── variation2_table_aria.html    # Table layout & ARIA attributes
│   └── variation3_shadow_dom.html    # Web Component / Shadow DOM
├── appsettings.json                 # Configurable execution settings
├── Program.cs                       # CLI entry point and suite dispatcher
├── SelfHealingAutomation.csproj     # .NET 8 Project file
└── APPROACH_AND_LIMITATIONS.md     # Engineering design & limitations document
```


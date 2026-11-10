# Approach & Limitations: Task 1 (Self-Healing Automation)

**Candidate:** Adarsh Dubey  
**Role:** Automation & AI Engineering Internship  
**Language & Framework:** C# (.NET 8), Selenium WebDriver  

---

## 1. Engineering Approach

### 1.1 The Challenge
Modern web applications undergo frequent UI refactoring. When testing pages such as `https://app.cloudqa.io/home/AutomationPracticeForm`, simple locators like `By.Id("fname")` or `By.XPath("//div[2]/input")` fail when:
- IDs and class names are dynamically hashed or randomized by UI frameworks (e.g., React, Angular, Vue, Webpack).
- Fields are rearranged into a different visual or DOM sequence.
- Structural layout changes from CSS Grid/Flexbox to `<table>` or custom Web Components (`<slot>` and `#shadow-root`).
- Other input fields on the page look very similar (e.g., "Last Name" vs "First Name").

### 1.2 Multi-Factor Scoring Architecture
Rather than relying on a single selector, this solution uses a **probabilistic multi-layered candidate scoring model**:

$$\text{Total Score} = S_{\text{label}} + S_{\text{container}} + S_{\text{attributes}} + S_{\text{content}} - P_{\text{negative}}$$

1. **Explicit Label Association ($S_{\text{label}} \le 50$)**:
   - Queries `label[for='<candidate_id>']`.
   - Computes text similarity against primary keywords (`"first name"`, `"fname"`, `"given-name"`).
2. **Container & Sibling Proximity ($S_{\text{container}} \le 40$)**:
   - Climbs parent hierarchy (up to 3 levels) to inspect enclosing `.form-group`, `fieldset`, `<tr>`, or `<div>`.
   - Evaluates nearby labels, table headers (`<th>`), and preceding text nodes.
3. **Semantic Attribute Fuzzy Matching ($S_{\text{attributes}} \le 35$)**:
   - Computes tokenized Jaccard similarity and Levenshtein edit distance against `name`, `placeholder`, `aria-label`, `title`, and `autocomplete`.
4. **Option & Content Inspection ($S_{\text{content}} \le 35$)**:
   - For dropdowns, inspects `<option>` text and values to confirm state/country names exist inside the control.
   - For radio buttons, matches the candidate's `value` attribute or adjacent text (`<span>Male</span>`) to the requested gender option.
5. **Contextual Negative Penalties ($P_{\text{negative}} \ge 60$)**:
   - Prevents false-positive matches on sibling fields. If an element's container or attributes match `"last name"` or `"lname"`, it is penalized heavily to ensure `"First Name"` is never misidentified.
6. **Shadow DOM Piercing**:
   - Standard Selenium `FindElements` cannot pierce `#shadow-root`. An injected recursive JavaScript crawler traverses both standard DOM trees and open `shadowRoot` elements, feeding all candidates into the scoring pipeline.

---

## 2. Verification Against DOM Variations

The approach was validated against 4 distinct test scenarios:
1. **Live CloudQA Page**: Tested against `https://app.cloudqa.io/home/AutomationPracticeForm`. (Passed with 85–100% confidence across all 3 controls).
2. **Variation 1 (Field Inversion & Randomized IDs)**: State is at the top, Gender in the middle, First Name at the bottom; IDs are randomized (`txt_rnd_1049`). (Passed: 100% confidence).
3. **Variation 2 (Table & ARIA)**: Wrapped in `<table>`, `<th>`, `<td>` with ARIA labels and no bootstrap classes. (Passed: 55–100% confidence).
4. **Variation 3 (Shadow DOM)**: Custom element `<shadow-registration-form>` with open shadow root. (Passed: 100% confidence).

---

## 3. Known Limitations & Edge Cases

| Limitation | Impact | Mitigation Strategy |
| :--- | :--- | :--- |
| **Canvas / WebGL Controls** | Non-DOM rendered interfaces (e.g. Flutter Web canvas, Google Docs canvas) do not expose HTML inputs. | Would require Vision-based OCR / Multi-modal agent coordinates (covered in Task 2). |
| **Closed Shadow Roots (`mode: 'closed'`)** | JavaScript cannot access `.shadowRoot` when explicitly declared closed. | Standard browser security boundary; requires test-mode flag or test hook. |
| **Multi-Lingual Forms Without Translation Map** | If the page is rendered entirely in a language with no shared roots (e.g. Japanese or Arabic) and lacks English ARIA attributes. | Can integrate a light translation lookup or an LLM semantic embedding matcher. |
| **Highly Ambiguous Unlabeled Forms** | If a page contains 5 blank `<input type="text">` tags with zero labels, placeholders, or attributes. | Visual/spatial inference (e.g., element position relative to surrounding text). |

---

## 4. Future Enhancements

- **Autonomous Selector Caching & Self-Repair**: Cache the winning selector. If the page doesn't change, run at 0ms overhead; if it breaks, trigger self-healing and update the cache file.
- **Visual Spatial Coordinates**: Integrate bounding-box Euclidean distance calculation for purely visual proximity (e.g., nearest input within $\Delta y \le 40\text{px}$).


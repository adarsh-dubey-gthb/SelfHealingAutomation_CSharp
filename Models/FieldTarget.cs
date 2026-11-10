namespace SelfHealingAutomation.Models;

public enum FieldKind
{
    TextInput,
    RadioButton,
    DropdownSelect
}

public class FieldTarget
{
    public string Name { get; set; } = string.Empty;
    public FieldKind Kind { get; set; }
    public string[] PrimaryKeywords { get; set; } = Array.Empty<string>();
    public string[] SecondaryKeywords { get; set; } = Array.Empty<string>();
    public string DesiredValue { get; set; } = string.Empty;

    public static FieldTarget FirstName(string value = "Adarsh") => new()
    {
        Name = "First Name",
        Kind = FieldKind.TextInput,
        PrimaryKeywords = new[] { "first name", "fname", "first_name", "firstname", "given-name" },
        SecondaryKeywords = new[] { "name", "first" },
        DesiredValue = value
    };

    public static FieldTarget Gender(string value = "Male") => new()
    {
        Name = "Gender",
        Kind = FieldKind.RadioButton,
        PrimaryKeywords = new[] { "gender", "sex" },
        SecondaryKeywords = new[] { "male", "female", "transgender", "other" },
        DesiredValue = value
    };

    public static FieldTarget State(string value = "United States") => new()
    {
        Name = "State",
        Kind = FieldKind.DropdownSelect,
        PrimaryKeywords = new[] { "state", "province", "region", "country" },
        SecondaryKeywords = new[] { "select state", "choose state", "state/province" },
        DesiredValue = value
    };
}


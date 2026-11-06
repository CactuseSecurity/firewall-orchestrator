using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using System.Collections.ObjectModel;
using System.Text;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Variables supported by the Check Point LayerGuard section-name template.</summary>
public enum CheckPointSectionNameVariable
{
    SourceZone,
    DestinationZone
}

/// <summary>Safe failure categories for parsing and rendering a section-name template.</summary>
public enum CheckPointSectionNameTemplateFailureCategory
{
    InvalidTemplate,
    MissingValue,
    InvalidValue,
    InvalidRenderedName
}

/// <summary>Parses and renders one strict Check Point LayerGuard section-name template.</summary>
public sealed class CheckPointSectionNameTemplate
{
    private const string kDelimiter = "@@";
    private readonly IReadOnlyList<TemplatePart> parts;
    private readonly HashSet<CheckPointSectionNameVariable> requiredVariables;

    /// <summary>Variables referenced by the parsed template, independently of repetition count.</summary>
    public IReadOnlySet<CheckPointSectionNameVariable> RequiredVariables => requiredVariables;

    private CheckPointSectionNameTemplate(
        IReadOnlyList<TemplatePart> parts,
        HashSet<CheckPointSectionNameVariable> requiredVariables)
    {
        this.parts = new ReadOnlyCollection<TemplatePart>(parts.ToList());
        this.requiredVariables = new HashSet<CheckPointSectionNameVariable>(requiredVariables);
    }

    /// <summary>Parses fixed text and recognized placeholders without accepting malformed delimiters.</summary>
    public static PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> Parse(
        string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return ParseFailure();
        }

        List<TemplatePart> parts = [];
        HashSet<CheckPointSectionNameVariable> variables = [];
        int cursor = 0;
        while (cursor < template.Length)
        {
            int marker = template.IndexOf(kDelimiter, cursor, StringComparison.Ordinal);
            if (marker < 0)
            {
                parts.Add(TemplatePart.Fixed(template[cursor..]));
                break;
            }
            if (marker > cursor)
            {
                parts.Add(TemplatePart.Fixed(template[cursor..marker]));
            }

            int nameStart = marker + kDelimiter.Length;
            int closingMarker = template.IndexOf(kDelimiter, nameStart, StringComparison.Ordinal);
            if (closingMarker < 0 ||
                !TryVariable(template[nameStart..closingMarker], out CheckPointSectionNameVariable variable))
            {
                return ParseFailure();
            }

            parts.Add(TemplatePart.VariablePart(variable));
            variables.Add(variable);
            cursor = closingMarker + kDelimiter.Length;
        }

        return PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory>.Success(
            new CheckPointSectionNameTemplate(parts, variables));
    }

    /// <summary>Renders parsed parts exactly once and invariant-normalizes the complete selector name.</summary>
    public PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> Render(
        IReadOnlyDictionary<CheckPointSectionNameVariable, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        StringBuilder rendered = new();
        foreach (TemplatePart part in parts)
        {
            if (!part.Variable.HasValue)
            {
                rendered.Append(part.Text);
                continue;
            }
            if (!values.TryGetValue(part.Variable.Value, out string? value))
            {
                return RenderFailure(CheckPointSectionNameTemplateFailureCategory.MissingValue);
            }

            string normalizedValue = value?.Trim() ?? string.Empty;
            if (normalizedValue.Length == 0)
            {
                return RenderFailure(CheckPointSectionNameTemplateFailureCategory.InvalidValue);
            }
            rendered.Append(normalizedValue);
        }

        string normalized = rendered.ToString().Trim().ToUpperInvariant();
        return normalized.Length == 0
            ? RenderFailure(CheckPointSectionNameTemplateFailureCategory.InvalidRenderedName)
            : PositioningResult<string, CheckPointSectionNameTemplateFailureCategory>.Success(normalized);
    }

    private static bool TryVariable(string name, out CheckPointSectionNameVariable variable)
    {
        switch (kDelimiter + name + kDelimiter)
        {
            case CheckPointLayerGuardSettingDefaults.kSourceZonePlaceholder:
                variable = CheckPointSectionNameVariable.SourceZone;
                return true;
            case CheckPointLayerGuardSettingDefaults.kDestinationZonePlaceholder:
                variable = CheckPointSectionNameVariable.DestinationZone;
                return true;
            default:
                variable = default;
                return false;
        }
    }

    private static PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> ParseFailure() =>
        PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory>.Failure(
            CheckPointSectionNameTemplateFailureCategory.InvalidTemplate);

    private static PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> RenderFailure(
        CheckPointSectionNameTemplateFailureCategory category) =>
        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory>.Failure(category);

    private sealed record TemplatePart(string Text, CheckPointSectionNameVariable? Variable)
    {
        internal static TemplatePart Fixed(string text) => new(text, null);

        internal static TemplatePart VariablePart(CheckPointSectionNameVariable variable) => new(string.Empty, variable);
    }
}

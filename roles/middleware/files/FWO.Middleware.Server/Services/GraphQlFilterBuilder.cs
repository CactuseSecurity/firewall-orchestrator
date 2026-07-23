namespace FWO.Middleware.Server.Services;

/// <summary>
/// Builds Hasura boolean expressions from REST filter values so that every controller applies the same
/// filter semantics. Text filters accept <c>*</c> for any character sequence and <c>?</c> for a single
/// character; plain text without wildcards is matched as a case-insensitive contains search.
/// </summary>
public static class GraphQlFilterBuilder
{
    /// <summary>
    /// Adds an equality predicate for the supplied field when the value is not null.
    /// </summary>
    public static void AddEqualsPredicate(List<Dictionary<string, object>> predicates, string fieldName, object? value)
    {
        if (value is not null)
        {
            predicates.Add(new Dictionary<string, object> { [fieldName] = new Dictionary<string, object> { ["_eq"] = value } });
        }
    }

    /// <summary>
    /// Adds a case-insensitive text predicate for the supplied field when the value is not null or blank.
    /// </summary>
    public static void AddWildcardPredicate(List<Dictionary<string, object>> predicates, string fieldName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            predicates.Add(new Dictionary<string, object> { [fieldName] = new Dictionary<string, object> { ["_ilike"] = BuildLikePattern(value) } });
        }
    }

    /// <summary>
    /// Adds a set membership predicate for the supplied field.
    /// </summary>
    public static void AddInPredicate(List<Dictionary<string, object>> predicates, string fieldName, List<int> values)
    {
        predicates.Add(BuildInExpression(fieldName, values));
    }

    /// <summary>
    /// Builds a set membership expression for the supplied field.
    /// </summary>
    public static Dictionary<string, object> BuildInExpression(string fieldName, List<int> values)
    {
        return new Dictionary<string, object> { [fieldName] = new Dictionary<string, object> { ["_in"] = values } };
    }

    /// <summary>
    /// Excludes owners whose lifecycle state is inactive, while keeping owners without a lifecycle state.
    /// Applied by default unless <paramref name="showOnlyActiveState"/> is explicitly <c>false</c>.
    /// </summary>
    public static void AddOwnerActiveStatePredicate(List<Dictionary<string, object>> predicates, bool? showOnlyActiveState)
    {
        if (showOnlyActiveState == false)
        {
            return;
        }

        List<Dictionary<string, object>> activeStateAlternatives =
        [
            new() { ["owner_lifecycle_state"] = new Dictionary<string, object> { ["active_state"] = new Dictionary<string, object> { ["_eq"] = true } } },
            new() { ["owner_lifecycle_state_id"] = new Dictionary<string, object> { ["_is_null"] = true } }
        ];
        predicates.Add(new Dictionary<string, object> { ["_or"] = activeStateAlternatives });
    }

    /// <summary>
    /// Combines all predicates into a single AND-connected where expression.
    /// </summary>
    public static Dictionary<string, object> CombinePredicates(List<Dictionary<string, object>> predicates)
    {
        return predicates.Count switch
        {
            0 => [],
            1 => predicates[0],
            _ => new Dictionary<string, object> { ["_and"] = predicates }
        };
    }

    /// <summary>
    /// Builds an <c>_ilike</c> pattern from a user-supplied filter value.
    /// Literal SQL wildcards (<c>\</c>, <c>%</c>, <c>_</c>) in the input are escaped so they are matched verbatim,
    /// while the documented <c>*</c> and <c>?</c> wildcards are translated to <c>%</c> and <c>_</c>.
    /// Plain text without <c>*</c>/<c>?</c> is wrapped for a contains search.
    /// </summary>
    public static string BuildLikePattern(string value)
    {
        string trimmedValue = value.Trim();
        bool hasWildcard = trimmedValue.Contains('*') || trimmedValue.Contains('?');
        string escapedValue = trimmedValue
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        string pattern = escapedValue.Replace('*', '%').Replace('?', '_');
        return hasWildcard ? pattern : $"%{pattern}%";
    }
}

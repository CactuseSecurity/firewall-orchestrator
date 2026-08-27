using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ApiQueries = FWO.Api.Client.Queries.Queries;

namespace FWO.Test
{
    /// <summary>
    /// Guards the alignment between the API call definitions and the Hasura role permissions.
    /// Hasura builds the columns of a role specific &lt;table&gt;_bool_exp from the select permission of
    /// that role, so a role which may delete or update a table but cannot select the columns its where
    /// clause filters on has the mutation rejected while it is validated. Nothing but a runtime error
    /// inside a background job reports that mismatch, which is why it is checked here instead.
    /// The metadata and the API calls are embedded into this assembly at build time
    /// (see FWO.Test.csproj), so no file of another component is read at runtime.
    /// </summary>
    [TestFixture]
    internal partial class ApiPermissionAlignmentTest
    {
        private const string kMetadataResource = "FWO.Test.replace_metadata.json";
        private const string kApiCallPrefix = "FWO.Test.ApiCalls.";
        private const string kApiCallSuffix = ".graphql";
        private const string kPermissionSuffix = "_permissions";
        private const string kSelectOperation = "select";
        private const string kUpdateOperation = "update";
        private const string kPublicSchema = "public";
        private const string kManyRootSuffix = "_many";
        private const int kMaxFilterDepth = 100;
        private const int kRegexTimeoutMilliseconds = 1000;

        /// <summary>
        /// Operators combining several conditions. Their operands are conditions again and not columns.
        /// </summary>
        private static readonly List<string> kLogicalOperators = new() { "_and", "_or", "_not" };

        /// <summary>
        /// Mismatches which are accepted although they exist. Every entry breaks the API call named with
        /// it for the role named with it, and closing one widens the read access of that role, which is a
        /// decision for the owner of the respective workflow. The list is empty because the metadata
        /// currently holds no such mismatch; it stays here so an accepted one can be recorded with the
        /// reason for accepting it instead of being silenced by weakening the check.
        /// ApiCalls_KnownPermissionGapsStillExist removes the risk of the list outliving the mismatches.
        /// </summary>
        private static readonly List<string> kKnownPermissionGaps = new();

        /// <summary>
        /// Every column an API call filters a delete or update on has to be selectable by the roles which
        /// are allowed to run that mutation, otherwise the column is no field of their boolean expression.
        /// </summary>
        [Test]
        public void ApiCalls_FilterOnlyColumnsTheirRolesMaySelect()
        {
            Dictionary<string, TableMetadata> tables = ReadMetadata();
            List<string> mismatches = new();

            foreach (FilteredMutation mutation in ReadFilteredMutations())
            {
                if (tables.TryGetValue(mutation.TableRoot, out TableMetadata? table))
                {
                    mismatches.AddRange(FindMismatches(mutation, table)
                        .Where(gap => !kKnownPermissionGaps.Contains(gap))
                        .Select(gap => $"{mutation.Source}: {gap}"));
                }
            }

            Assert.That(mismatches, Is.Empty,
                "A role may run these mutations but cannot select the columns they filter on:"
                + Environment.NewLine + string.Join(Environment.NewLine, mismatches));
        }

        /// <summary>
        /// A mutation on a table which is not tracked in the metadata does not exist in the API schema at
        /// all, so an unknown table name is a typo or a table someone forgot to track.
        /// </summary>
        [Test]
        public void ApiCalls_AddressOnlyTrackedTables()
        {
            Dictionary<string, TableMetadata> tables = ReadMetadata();

            List<string> unknownTables = ReadFilteredMutations()
                .Where(mutation => !tables.ContainsKey(mutation.TableRoot))
                .Select(mutation => $"{mutation.Source}: {mutation.TableRoot}")
                .Distinct()
                .ToList();

            Assert.That(unknownTables, Is.Empty,
                "These mutations address tables which are not tracked in the metadata:"
                + Environment.NewLine + string.Join(Environment.NewLine, unknownTables));
        }

        /// <summary>
        /// Keeps the accepted mismatches honest: once one of them is repaired in the metadata its entry has
        /// to be removed here as well, otherwise the list would silently accept the mismatch again later.
        /// </summary>
        [Test]
        public void ApiCalls_KnownPermissionGapsStillExist()
        {
            Dictionary<string, TableMetadata> tables = ReadMetadata();
            List<string> existingGaps = new();

            foreach (FilteredMutation mutation in ReadFilteredMutations())
            {
                if (tables.TryGetValue(mutation.TableRoot, out TableMetadata? table))
                {
                    existingGaps.AddRange(FindMismatches(mutation, table));
                }
            }

            List<string> repairedGaps = kKnownPermissionGaps.Where(gap => !existingGaps.Contains(gap)).ToList();

            Assert.That(repairedGaps, Is.Empty,
                "These accepted mismatches no longer exist and have to be removed from kKnownPermissionGaps:"
                + Environment.NewLine + string.Join(Environment.NewLine, repairedGaps));
        }

        /// <summary>
        /// Collects the roles which may run the mutation but cannot select one of the filtered columns.
        /// </summary>
        /// <returns>One entry per missing permission, in the format of FormatGap.</returns>
        private static List<string> FindMismatches(FilteredMutation mutation, TableMetadata table)
        {
            List<string> mismatches = new();
            if (!table.RolesByOperation.TryGetValue(mutation.Operation, out List<string>? roles))
            {
                return mismatches;
            }

            foreach (string role in roles.Where(role => MayRunMutation(mutation, table, role)))
            {
                table.SelectableColumns.TryGetValue(role, out HashSet<string>? selectable);
                mismatches.AddRange(mutation.Columns
                    .Where(column => selectable == null || !selectable.Contains(column))
                    .Select(column => FormatGap(mutation.TableRoot, role, column)));
            }
            return mismatches;
        }

        /// <summary>
        /// Decides whether a role can run a mutation at all. An update writing a column the role may not
        /// write is rejected for that column already, so its where clause never reaches the role and
        /// demanding a select permission for it would only invite widening the read access for nothing.
        /// </summary>
        private static bool MayRunMutation(FilteredMutation mutation, TableMetadata table, string role)
        {
            if (mutation.Operation != kUpdateOperation || mutation.UpdatedColumns.Count == 0)
            {
                return true;
            }
            return table.UpdatableColumns.TryGetValue(role, out HashSet<string>? updatable)
                && mutation.UpdatedColumns.TrueForAll(updatable.Contains);
        }

        /// <summary>
        /// Identifies one missing permission independently of the API call which uncovered it, so the same
        /// gap reported by several calls is accepted by a single entry of kKnownPermissionGaps.
        /// </summary>
        private static string FormatGap(string tableRoot, string role, string column)
        {
            return $"{tableRoot}|{role}|{column}";
        }

        /// <summary>
        /// Reads the tables of the metadata by the name their mutations carry in the GraphQL schema.
        /// </summary>
        private static Dictionary<string, TableMetadata> ReadMetadata()
        {
            using JsonDocument metadata = JsonDocument.Parse(ReadResource(kMetadataResource));
            Dictionary<string, TableMetadata> tables = new();

            foreach (JsonElement source in metadata.RootElement.GetProperty("args")
                .GetProperty("metadata").GetProperty("sources").EnumerateArray())
            {
                foreach (JsonElement table in source.GetProperty("tables").EnumerateArray())
                {
                    tables.Add(ReadTableRoot(table.GetProperty("table")), ReadTablePermissions(table));
                }
            }
            return tables;
        }

        /// <summary>
        /// Builds the name the mutations of a table carry, which Hasura prefixes with the schema for every
        /// schema but public.
        /// </summary>
        private static string ReadTableRoot(JsonElement table)
        {
            string schema = table.GetProperty("schema").GetString() ?? "";
            string name = table.GetProperty("name").GetString() ?? "";
            return schema == kPublicSchema ? name : $"{schema}_{name}";
        }

        /// <summary>
        /// Reads which roles hold which permission on a table and which columns each role may select.
        /// </summary>
        private static TableMetadata ReadTablePermissions(JsonElement table)
        {
            TableMetadata metadata = new();
            foreach (JsonProperty property in table.EnumerateObject()
                .Where(property => property.Name.EndsWith(kPermissionSuffix)))
            {
                string operation = property.Name[..^kPermissionSuffix.Length];
                foreach (JsonElement entry in property.Value.EnumerateArray())
                {
                    string role = entry.GetProperty("role").GetString() ?? "";
                    AddRole(metadata.RolesByOperation, operation, role);
                    if (operation == kSelectOperation)
                    {
                        metadata.SelectableColumns[role] = ReadPermittedColumns(entry);
                    }
                    else if (operation == kUpdateOperation)
                    {
                        metadata.UpdatableColumns[role] = ReadPermittedColumns(entry);
                    }
                }
            }
            return metadata;
        }

        /// <summary>
        /// Adds a role to the roles holding one permission on a table.
        /// </summary>
        private static void AddRole(Dictionary<string, List<string>> rolesByOperation, string operation, string role)
        {
            if (!rolesByOperation.TryGetValue(operation, out List<string>? roles))
            {
                roles = new();
                rolesByOperation.Add(operation, roles);
            }
            roles.Add(role);
        }

        /// <summary>
        /// Reads the columns of one permission entry. A delete permission carries no columns at all.
        /// </summary>
        private static HashSet<string> ReadPermittedColumns(JsonElement permissionEntry)
        {
            HashSet<string> columns = new();
            if (permissionEntry.GetProperty("permission").TryGetProperty("columns", out JsonElement declared)
                && declared.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement column in declared.EnumerateArray())
                {
                    columns.Add(column.GetString() ?? "");
                }
            }
            return columns;
        }

        /// <summary>
        /// Reads every delete and update mutation of the embedded API calls which carries a where clause.
        /// </summary>
        private static List<FilteredMutation> ReadFilteredMutations()
        {
            List<FilteredMutation> mutations = new();
            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(name => name.StartsWith(kApiCallPrefix) && name.EndsWith(kApiCallSuffix)))
            {
                mutations.AddRange(ReadFilteredMutations(FormatSource(resource), ApiQueries.Compact(ReadResource(resource))));
            }
            return mutations;
        }

        /// <summary>
        /// Reads the filtered mutations of a single API call. The call is compacted the same way it is
        /// before it is sent, so the comments of the file cannot be mistaken for a mutation.
        /// </summary>
        private static List<FilteredMutation> ReadFilteredMutations(string source, string apiCall)
        {
            List<FilteredMutation> mutations = new();
            foreach (Match mutation in MutationRegex().Matches(apiCall).Cast<Match>())
            {
                string? arguments = ReadArguments(apiCall, mutation.Index + mutation.Length - 1);
                string? filter = arguments == null ? null : ReadObjectArgument(arguments, FilterRegex());
                if (filter == null)
                {
                    continue;
                }

                HashSet<string> columns = new();
                CollectFilterColumns(filter, columns, 0);
                mutations.Add(new FilteredMutation(source, mutation.Groups[1].Value,
                    ReadMutationRoot(mutation.Groups[2].Value), columns.ToList(),
                    ReadUpdatedColumns(arguments!)));
            }
            return mutations;
        }

        /// <summary>
        /// Reads the table a mutation addresses. Hasura appends _many to the mutation updating several
        /// filters at once and _by_pk to the one addressing a single row without a where clause.
        /// </summary>
        private static string ReadMutationRoot(string mutationName)
        {
            return mutationName.EndsWith(kManyRootSuffix) ? mutationName[..^kManyRootSuffix.Length] : mutationName;
        }

        /// <summary>
        /// Reads the argument list of a mutation, starting at its opening bracket.
        /// </summary>
        /// <returns>The arguments without the enclosing brackets, or null when they are not closed.</returns>
        private static string? ReadArguments(string apiCall, int argumentStart)
        {
            int argumentEnd = IndexOfMatchingBracket(apiCall, argumentStart);
            return argumentEnd < 0 ? null : apiCall[(argumentStart + 1)..argumentEnd];
        }

        /// <summary>
        /// Reads the body of the object argument the given expression introduces.
        /// </summary>
        /// <returns>The body of the argument, or null when the mutation does not carry it.</returns>
        private static string? ReadObjectArgument(string arguments, Regex argumentRegex)
        {
            Match argument = argumentRegex.Match(arguments);
            if (!argument.Success)
            {
                return null;
            }

            int bodyStart = argument.Index + argument.Length - 1;
            int bodyEnd = IndexOfMatchingBracket(arguments, bodyStart);
            return bodyEnd < 0 ? null : arguments[(bodyStart + 1)..bodyEnd];
        }

        /// <summary>
        /// Reads the columns a mutation writes, which is empty for a delete.
        /// </summary>
        private static List<string> ReadUpdatedColumns(string arguments)
        {
            string? assignment = ReadObjectArgument(arguments, AssignmentRegex());
            return assignment == null
                ? new()
                : EnumerateMembers(assignment).Select(member => member.Name).ToList();
        }

        /// <summary>
        /// Collects the columns a filter compares. A member comparing a column carries operators only,
        /// every other member walks a relationship into another table and is left to the API call of it.
        /// </summary>
        private static void CollectFilterColumns(string filter, ISet<string> columns, int depth)
        {
            if (depth >= kMaxFilterDepth)
            {
                throw new InvalidOperationException($"Filter is nested deeper than {kMaxFilterDepth} levels.");
            }

            foreach ((string name, string? body) in EnumerateMembers(filter))
            {
                if (body == null)
                {
                    continue;
                }
                if (kLogicalOperators.Contains(name))
                {
                    CollectFilterColumns(body, columns, depth + 1);
                }
                else if (!name.StartsWith('_') && ComparesAColumn(body))
                {
                    columns.Add(name);
                }
            }
        }

        /// <summary>
        /// Decides whether the members of a filter member are comparison operators, which makes the member
        /// itself a column and not a relationship.
        /// </summary>
        private static bool ComparesAColumn(string memberBody)
        {
            bool hasMember = false;
            foreach ((string name, string? _) in EnumerateMembers(memberBody))
            {
                hasMember = true;
                if (!name.StartsWith('_'))
                {
                    return false;
                }
            }
            return hasMember;
        }

        /// <summary>
        /// Walks the members of a GraphQL object.
        /// </summary>
        /// <returns>The name of every member with its body, which is null for a member holding a value.</returns>
        private static IEnumerable<(string Name, string? Body)> EnumerateMembers(string objectBody)
        {
            int position = 0;
            while (position < objectBody.Length)
            {
                Match member = MemberRegex().Match(objectBody, position);
                if (!member.Success)
                {
                    yield break;
                }

                int valueStart = member.Index + member.Length;
                int valueEnd = IsBracket(objectBody, valueStart) ? IndexOfMatchingBracket(objectBody, valueStart) : -1;
                if (valueEnd < 0)
                {
                    yield return (member.Groups[1].Value, null);
                    position = valueStart;
                    continue;
                }

                yield return (member.Groups[1].Value, objectBody[(valueStart + 1)..valueEnd]);
                position = valueEnd + 1;
            }
        }

        /// <summary>
        /// Decides whether an object or a list starts at the given position.
        /// </summary>
        private static bool IsBracket(string text, int position)
        {
            return position < text.Length && (text[position] == '{' || text[position] == '[');
        }

        /// <summary>
        /// Finds the bracket closing the one at the given position. Brackets of another kind in between do
        /// not change the nesting of the searched one and are therefore not counted.
        /// </summary>
        /// <returns>The position of the closing bracket, or -1 when it is missing.</returns>
        private static int IndexOfMatchingBracket(string text, int openingPosition)
        {
            char opening = text[openingPosition];
            char closing = ClosingBracketOf(opening);
            int depth = 0;

            for (int position = openingPosition; position < text.Length; position++)
            {
                if (text[position] == opening)
                {
                    depth++;
                }
                else if (text[position] == closing && --depth == 0)
                {
                    return position;
                }
            }
            return -1;
        }

        /// <summary>
        /// Reads the bracket closing an opening one.
        /// </summary>
        private static char ClosingBracketOf(char opening)
        {
            return opening switch
            {
                '{' => '}',
                '[' => ']',
                '(' => ')',
                _ => throw new ArgumentException($"{opening} is no opening bracket.", nameof(opening))
            };
        }

        /// <summary>
        /// Names an API call by its path below fwo-api-calls, so a failure points at the file to repair.
        /// The directories of the path are separators of the resource name and are restored here.
        /// </summary>
        private static string FormatSource(string resourceName)
        {
            string path = resourceName[kApiCallPrefix.Length..^kApiCallSuffix.Length];
            return path.Replace('.', Path.AltDirectorySeparatorChar) + kApiCallSuffix;
        }

        /// <summary>
        /// Reads an embedded resource of this assembly.
        /// </summary>
        private static string ReadResource(string resourceName)
        {
            using Stream? resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource {resourceName} is missing.");
            using StreamReader reader = new(resource);
            return reader.ReadToEnd();
        }

        [GeneratedRegex(@"\b(delete|update)_([A-Za-z0-9_]+)\s*\(", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex MutationRegex();

        [GeneratedRegex(@"\bwhere\s*:\s*\{", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex FilterRegex();

        [GeneratedRegex(@"\b_set\s*:\s*\{", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex AssignmentRegex();

        [GeneratedRegex(@"([A-Za-z_][A-Za-z0-9_]*)\s*:\s*", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex MemberRegex();

        /// <summary>
        /// A delete or update mutation of an API call together with the columns its where clause filters on.
        /// </summary>
        private sealed record FilteredMutation(string Source, string Operation, string TableRoot,
            List<string> Columns, List<string> UpdatedColumns);

        /// <summary>
        /// The permissions of one table: which roles hold which permission and which columns they may select.
        /// </summary>
        private sealed class TableMetadata
        {
            public Dictionary<string, List<string>> RolesByOperation { get; } = new();
            public Dictionary<string, HashSet<string>> SelectableColumns { get; } = new();
            public Dictionary<string, HashSet<string>> UpdatableColumns { get; } = new();
        }
    }
}

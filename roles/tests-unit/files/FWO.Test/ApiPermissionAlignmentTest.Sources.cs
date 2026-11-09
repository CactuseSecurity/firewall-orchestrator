using System.Text.RegularExpressions;
using NUnit.Framework;
using ApiQueries = FWO.Api.Client.Queries.Queries;

namespace FWO.Test
{
    /// <summary>
    /// Locating and reading the sources the permission alignment is checked against: the API call
    /// definitions and the places the repository and an installed system keep them. The checks
    /// themselves and the reading of the Hasura metadata live in the other part of this class.
    /// </summary>
    internal partial class ApiPermissionAlignmentTest
    {
        /// <summary>
        /// Reads every root field of the embedded API calls together with what it filters on and returns.
        /// </summary>
        private static ApiCallSurvey ReadApiCalls()
        {
            DirectoryInfo apiCalls = kApiCallDirectory.Value
                ?? throw new InvalidOperationException(kApiCallsOutOfReach);

            ApiCallSurvey survey = new();
            foreach (FileInfo apiCall in apiCalls.EnumerateFiles($"*{kApiCallSuffix}", SearchOption.AllDirectories)
                .OrderBy(apiCall => apiCall.FullName, StringComparer.Ordinal))
            {
                ReadApiCall(survey, FormatSource(apiCalls, apiCall),
                    ApiQueries.Compact(File.ReadAllText(apiCall.FullName)));
            }
            return survey;
        }

        /// <summary>
        /// Reads the root fields of a single API call. The call is compacted the same way it is before it
        /// is sent, so the comments of the file cannot be mistaken for part of it.
        /// </summary>
        private static void ReadApiCall(ApiCallSurvey survey, string source, string apiCall)
        {
            string operationType = ReadOperationType(apiCall);
            string? selectionSet = ReadSelectionSet(apiCall);
            if (selectionSet == null)
            {
                survey.Uncheckable.Add($"{source}: the operation carries no selection set.");
                return;
            }

            foreach (ApiField field in EnumerateFields(selectionSet))
            {
                if (field.IsFragmentSpread)
                {
                    survey.Uncheckable.Add($"{source}: a fragment spread stands where a root field is expected.");
                }
                else
                {
                    ReadRootField(survey, source, operationType, field);
                }
            }
        }

        /// <summary>
        /// Turns one root field into the operation it performs on a table.
        /// </summary>
        private static void ReadRootField(ApiCallSurvey survey, string source, string operationType, ApiField field)
        {
            (string operation, string tableRoot, bool addressesOneRow) = ResolveRootField(field.Name, operationType);
            if (operation.Length == 0)
            {
                return;
            }

            List<string> filterColumns = ReadFilterColumns(survey, source, field);
            (List<string> returned, bool opaque) = ReadReturnedColumns(survey, source, field.SubSelection);
            survey.Operations.Add(new ApiOperation(source, operation, tableRoot, filterColumns, returned,
                ReadWrittenColumns(survey, source, field, operation),
                addressesOneRow || opaque || returned.Count > 0));
        }

        /// <summary>
        /// Reads the operation type of an API call, which is a query where the shorthand form omits it.
        /// </summary>
        private static string ReadOperationType(string apiCall)
        {
            Match keyword = OperationKeywordRegex().Match(apiCall);
            return keyword.Success ? keyword.Groups[1].Value : kQueryOperationType;
        }

        /// <summary>
        /// Reads the outermost selection set of an API call, skipping the name and the variable
        /// definitions of the operation.
        /// </summary>
        /// <returns>The body of the selection set, or null when the call carries none.</returns>
        private static string? ReadSelectionSet(string apiCall)
        {
            Match keyword = OperationKeywordRegex().Match(apiCall);
            int position = keyword.Success
                ? SkipOperationHeader(apiCall, keyword.Index + keyword.Length)
                : 0;

            int start = apiCall.IndexOf('{', position);
            if (start < 0)
            {
                return null;
            }
            int end = IndexOfMatchingBracket(apiCall, start);
            return end < 0 ? null : apiCall[(start + 1)..end];
        }

        /// <summary>
        /// Skips the name and the variable definitions following the operation keyword.
        /// </summary>
        private static int SkipOperationHeader(string apiCall, int position)
        {
            position = SkipName(apiCall, SkipSpaces(apiCall, position));
            position = SkipSpaces(apiCall, position);
            if (position < apiCall.Length && apiCall[position] == '(')
            {
                int end = IndexOfMatchingBracket(apiCall, position);
                position = end < 0 ? position : end + 1;
            }
            return position;
        }

        /// <summary>
        /// Walks the fields of a selection set, resolving the alias of a field to the field it renames.
        /// </summary>
        private static IEnumerable<ApiField> EnumerateFields(string selectionSet)
        {
            int position = 0;
            while (position < selectionSet.Length)
            {
                position = SkipSpaces(selectionSet, position);
                if (position >= selectionSet.Length)
                {
                    yield break;
                }
                if (string.CompareOrdinal(selectionSet, position, kFragmentSpread, 0, kFragmentSpread.Length) == 0)
                {
                    position = SkipName(selectionSet, position + kFragmentSpread.Length);
                    yield return ApiField.FragmentSpread;
                    continue;
                }

                (string name, int afterName) = ReadFieldName(selectionSet, position);
                if (name.Length == 0)
                {
                    position++;
                    continue;
                }
                (string? arguments, int afterArguments) = ReadBracketed(selectionSet, afterName, '(');
                (string? subSelection, int afterSubSelection) = ReadBracketed(selectionSet, afterArguments, '{');
                position = afterSubSelection;
                yield return new ApiField(name, arguments, subSelection, false);
            }
        }

        /// <summary>
        /// Reads the name of a field, which is the one behind the colon where the field carries an alias.
        /// </summary>
        /// <returns>The name and the position behind it.</returns>
        private static (string Name, int Position) ReadFieldName(string selectionSet, int position)
        {
            int nameEnd = SkipName(selectionSet, position);
            string name = selectionSet[position..nameEnd];
            int afterName = SkipSpaces(selectionSet, nameEnd);
            if (name.Length == 0 || afterName >= selectionSet.Length || selectionSet[afterName] != ':')
            {
                return (name, afterName);
            }

            int aliasedStart = SkipSpaces(selectionSet, afterName + 1);
            int aliasedEnd = SkipName(selectionSet, aliasedStart);
            return (selectionSet[aliasedStart..aliasedEnd], SkipSpaces(selectionSet, aliasedEnd));
        }

        /// <summary>
        /// Reads the body enclosed in the given bracket where one opens at the given position.
        /// </summary>
        /// <returns>The body without the brackets, or null where none opens, and the position behind it.</returns>
        private static (string? Body, int Position) ReadBracketed(string text, int position, char opening)
        {
            if (position >= text.Length || text[position] != opening)
            {
                return (null, position);
            }
            int end = IndexOfMatchingBracket(text, position);
            return end < 0 ? (null, text.Length) : (text[(position + 1)..end], SkipSpaces(text, end + 1));
        }

        /// <summary>
        /// Resolves a root field to the operation it performs and the table it addresses.
        /// </summary>
        /// <returns>An empty operation where the root field addresses no table.</returns>
        private static (string Operation, string TableRoot, bool AddressesOneRow) ResolveRootField(
            string rootField, string operationType)
        {
            string name = rootField;
            bool addressesOneRow = name.EndsWith(kByPkRootSuffix);
            if (addressesOneRow)
            {
                name = name[..^kByPkRootSuffix.Length];
            }

            if (operationType != kMutationOperationType)
            {
                return (kSelectOperation, TrimRootFieldSuffix(name), addressesOneRow);
            }
            Match mutation = MutationRootFieldRegex().Match(name);
            return mutation.Success
                ? (mutation.Groups[1].Value, TrimRootFieldSuffix(mutation.Groups[2].Value), addressesOneRow)
                : ("", "", addressesOneRow);
        }

        /// <summary>
        /// Cuts off the suffix Hasura appends to the name of a table for a root field addressing it in
        /// another way, which leaves the name of the table itself.
        /// </summary>
        private static string TrimRootFieldSuffix(string rootField)
        {
            string suffix = kRootFieldSuffixes.Find(rootField.EndsWith) ?? "";
            return rootField[..^suffix.Length];
        }

        /// <summary>
        /// Reads the columns the where clause of a root field compares. A filter handed over as a variable
        /// is only known at run time and is recorded as uncheckable instead.
        /// </summary>
        private static List<string> ReadFilterColumns(ApiCallSurvey survey, string source, ApiField field)
        {
            HashSet<string> columns = new();
            if (field.Arguments == null)
            {
                return columns.ToList();
            }

            Match filter = FilterArgumentRegex().Match(field.Arguments);
            if (!filter.Success)
            {
                ReportVariableArgument(survey, source, field, VariableFilterRegex(), "filter");
                return columns.ToList();
            }
            int bodyStart = filter.Index + filter.Length - 1;
            int bodyEnd = IndexOfMatchingBracket(field.Arguments, bodyStart);
            if (bodyEnd >= 0)
            {
                CollectFilterColumns(field.Arguments[(bodyStart + 1)..bodyEnd], columns, 0);
            }
            return columns.ToList();
        }

        /// <summary>
        /// Reads the columns a mutation writes, which decides whether a role can run it at all. An object
        /// list handed over as a variable is only known at run time and is recorded as uncheckable.
        /// </summary>
        private static List<string> ReadWrittenColumns(ApiCallSurvey survey, string source, ApiField field,
            string operation)
        {
            if (field.Arguments == null || operation == kDeleteOperation || operation == kSelectOperation)
            {
                return new();
            }

            Regex bodyRegex = operation == kUpdateOperation ? AssignmentRegex() : ObjectsRegex();
            Match written = bodyRegex.Match(field.Arguments);
            if (!written.Success)
            {
                ReportVariableArgument(survey, source, field, VariableWrittenRegex(), "written object");
                return new();
            }
            int bodyStart = written.Index + written.Length - 1;
            int bodyEnd = IndexOfMatchingBracket(field.Arguments, bodyStart);
            return bodyEnd < 0
                ? new()
                : EnumerateMembers(field.Arguments[(bodyStart + 1)..bodyEnd]).Select(member => member.Name).ToList();
        }

        /// <summary>
        /// Records an argument handed over as a variable, whose content no static check can read.
        /// </summary>
        private static void ReportVariableArgument(ApiCallSurvey survey, string source, ApiField field,
            Regex variableRegex, string subject)
        {
            if (field.Arguments != null && variableRegex.IsMatch(field.Arguments))
            {
                survey.Uncheckable.Add($"{source}: {field.Name} takes its {subject} from a variable.");
            }
        }

        /// <summary>
        /// Reads the columns a mutation returns, which are the direct scalar fields of its response.
        /// A nested object addresses another table and is left to the permissions of that table.
        /// </summary>
        /// <returns>The returned columns, and whether a fragment spread hides further ones.</returns>
        private static (List<string> Columns, bool Opaque) ReadReturnedColumns(ApiCallSurvey survey, string source,
            string? selectionSet)
        {
            HashSet<string> columns = new();
            bool opaque = false;
            if (selectionSet == null)
            {
                return (columns.ToList(), false);
            }

            foreach (ApiField field in EnumerateFields(selectionSet))
            {
                if (field.IsFragmentSpread)
                {
                    opaque = true;
                    survey.Uncheckable.Add($"{source}: a fragment spread hides part of the returned columns.");
                }
                else if (field.Name == kReturningField)
                {
                    (List<string> nested, bool nestedOpaque) = ReadReturnedColumns(survey, source, field.SubSelection);
                    columns.UnionWith(nested);
                    opaque = opaque || nestedOpaque;
                }
                else if (field.SubSelection == null && !kResponseMetaFields.Contains(field.Name))
                {
                    columns.Add(field.Name);
                }
            }
            return (columns.ToList(), opaque);
        }

        /// <summary>
        /// Collects the columns a filter compares. A member comparing a column carries comparison operators
        /// only; a member carrying a combining operator or a named condition walks a relationship into
        /// another table and is left to the API call of it.
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
        /// itself a column and not a relationship. A combining operator is no comparison: it introduces a
        /// condition over another table and appears in a boolean expression only.
        /// </summary>
        private static bool ComparesAColumn(string memberBody)
        {
            bool hasMember = false;
            foreach ((string name, string? _) in EnumerateMembers(memberBody))
            {
                hasMember = true;
                if (!name.StartsWith('_') || kLogicalOperators.Contains(name))
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
        /// Skips the spaces at the given position, of which a compacted call holds at most one in a row.
        /// </summary>
        private static int SkipSpaces(string text, int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
            return position;
        }

        /// <summary>
        /// Skips the name at the given position.
        /// </summary>
        private static int SkipName(string text, int position)
        {
            while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
            {
                position++;
            }
            return position;
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
        /// </summary>
        private static string FormatSource(DirectoryInfo apiCalls, FileInfo apiCall)
        {
            return Path.GetRelativePath(apiCalls.FullName, apiCall.FullName)
                .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Locates the Hasura metadata in the repository or in the directory the installer copies it to
        /// next to the tests.
        /// </summary>
        /// <returns>The metadata file, or null when it is out of reach.</returns>
        private static FileInfo? LocateMetadata()
        {
            return LocateBesideOrAbove(directory =>
            {
                FileInfo repository = new(Path.Combine(directory.FullName, "roles", "api", "files", kMetadataFile));
                FileInfo installed = new(Path.Combine(directory.FullName, kMetadataFile));
                return repository.Exists ? repository : installed.Exists ? installed : null;
            });
        }

        /// <summary>
        /// Locates the API call definitions in the repository or where the installer deploys them for the
        /// UI, middleware and importer.
        /// </summary>
        /// <returns>The directory holding the API calls, or null when it is out of reach.</returns>
        private static DirectoryInfo? LocateApiCalls()
        {
            return LocateBesideOrAbove(directory =>
            {
                DirectoryInfo repository = new(Path.Combine(directory.FullName, "roles", "common", "files", kApiCallDirectoryName));
                DirectoryInfo installed = new(Path.Combine(directory.FullName, kApiCallDirectoryName));
                return repository.Exists ? repository : installed.Exists ? installed : null;
            });
        }

        /// <summary>
        /// Walks from the directory of the test assembly up to the root and returns what the given lookup
        /// finds first. The tests run from a repository checkout as well as from an installed system, where
        /// the sources of the other components are not laid out the same way.
        /// </summary>
        private static TFound? LocateBesideOrAbove<TFound>(Func<DirectoryInfo, TFound?> lookup) where TFound : class
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                TFound? found = lookup(directory);
                if (found is not null)
                {
                    return found;
                }
                directory = directory.Parent;
            }
            return null;
        }

        /// <summary>
        /// Skips a test when the metadata or the API calls cannot be read, which happens wherever only
        /// part of the sources is deployed. Reporting that plainly is better than passing on no input.
        /// </summary>
        private static void SkipWithoutSources()
        {
            if (kMetadataFileInfo.Value is null)
            {
                Assert.Ignore(kMetadataOutOfReach);
            }
            if (kApiCallDirectory.Value is null)
            {
                Assert.Ignore(kApiCallsOutOfReach);
            }
        }

        [GeneratedRegex(@"^\s*(query|mutation|subscription)\b", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex OperationKeywordRegex();

        [GeneratedRegex(@"^(insert|update|delete)_(.+)$", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex MutationRootFieldRegex();

        [GeneratedRegex(@"\bwhere\s*:\s*\{", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex FilterArgumentRegex();

        [GeneratedRegex(@"\bwhere\s*:\s*\$", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex VariableFilterRegex();

        [GeneratedRegex(@"\b_set\s*:\s*\{", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex AssignmentRegex();

        [GeneratedRegex(@"\bobjects?\s*:\s*[\{\[]", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex ObjectsRegex();

        [GeneratedRegex(@"\b(_set|objects?|updates)\s*:\s*\$", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex VariableWrittenRegex();

        [GeneratedRegex(@"([A-Za-z_][A-Za-z0-9_]*)\s*:\s*", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex MemberRegex();
    }
}

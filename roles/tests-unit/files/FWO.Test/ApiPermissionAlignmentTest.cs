using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the alignment between the API call definitions and the Hasura role permissions.
    /// Hasura derives what a role sees of a table from the select permission of that role, which makes
    /// a missing select permission break calls that never read anything on purpose:
    /// <list type="bullet">
    /// <item>the columns of a role specific &lt;table&gt;_bool_exp are the selectable ones, so a role which
    /// may delete or update a table but cannot select the columns its where clause filters on has the
    /// mutation rejected while it is validated,</item>
    /// <item>a mutation response passes through the select permission as well, so a role which cannot
    /// select the columns behind a returning block cannot run the mutation either, and a _by_pk root
    /// field is not even part of the schema of a role without select permission on the table.</item>
    /// </list>
    /// Nothing but a runtime error inside a background job or a page reports either mismatch, which is why
    /// they are checked here instead.
    /// The metadata and the API calls are read from the repository, or from the places the installer
    /// deploys them to, because an installed system holds only the sources of the tests themselves.
    /// Where neither is reachable the checks are skipped instead of passing on no input at all.
    /// What the checks cannot see is listed by ReportUncheckableCalls instead of passing silently:
    /// filters and object lists handed over as a variable are only known at run time, and a selection set
    /// behind a fragment spread lives in another file, so of it only the fact that something is returned
    /// is used. Queries are covered by ApiCalls_QueriesAreRunnableByAtLeastOneRole alone, because the
    /// metadata does not record which role issues which query and demanding the filter of a query from
    /// every role holding select permission would report far more roles than ever run it.
    /// </summary>
    [TestFixture]
    internal partial class ApiPermissionAlignmentTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kApiCallDirectoryName = "fwo-api-calls";
        private const string kApiCallSuffix = ".graphql";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the permission alignment cannot be checked.";
        private const string kApiCallsOutOfReach =
            "The API call definitions are not reachable in this environment, so the permission alignment cannot be checked.";
        private const string kAmbiguousTableRoot =
            "Two tracked tables of the metadata reach the same root field name in the GraphQL schema, "
            + "so the permissions of one of them would be checked against the calls of the other: ";
        private const string kPermissionSuffix = "_permissions";
        private const string kSelectOperation = "select";
        private const string kInsertOperation = "insert";
        private const string kUpdateOperation = "update";
        private const string kDeleteOperation = "delete";
        private const string kQueryOperationType = "query";
        private const string kMutationOperationType = "mutation";
        private const string kPublicSchema = "public";
        private const string kByPkRootSuffix = "_by_pk";
        private const string kFragmentSpread = "...";
        private const string kAffectedRowsField = "affected_rows";
        private const string kReturningField = "returning";
        private const string kTypeNameField = "__typename";
        private const string kAllColumnsMarker = "*";

        /// <summary>
        /// Stands for "the role needs select permission on this table at all" in a gap, which is what a
        /// _by_pk root field and a returning block behind a fragment spread require without naming a
        /// single column.
        /// </summary>
        private const string kAnyColumn = "<any column>";

        /// <summary>
        /// Stands for "no role at all" in a gap reported for a query, whose issuing role is not recorded
        /// anywhere in the metadata.
        /// </summary>
        private const string kAnyRole = "<any role>";

        private const int kMaxFilterDepth = 100;
        private const int kRegexTimeoutMilliseconds = 1000;

        /// <summary>
        /// Operators combining several conditions. Their operands are conditions again and not columns,
        /// and they appear in a boolean expression only, never inside the comparison of a single column,
        /// which is what tells a relationship apart from a column.
        /// </summary>
        private static readonly List<string> kLogicalOperators = new() { "_and", "_or", "_not" };

        /// <summary>
        /// Suffixes Hasura appends to the name of a table for a root field addressing it in another way.
        /// They are cut off to arrive at the table the root field belongs to.
        /// </summary>
        private static readonly List<string> kRootFieldSuffixes =
            new() { "_many", "_one", "_aggregate", "_stream" };

        /// <summary>
        /// The kinds of relationship a table declares, which are read the same way.
        /// </summary>
        private static readonly List<string> kRelationshipKinds =
            new() { "object_relationships", "array_relationships" };

        /// <summary>
        /// Fields of a mutation response which are no columns of the table.
        /// </summary>
        private static readonly List<string> kResponseMetaFields = new() { kAffectedRowsField, kTypeNameField };

        /// <summary>
        /// Marks a role which may select every column of a table, which Hasura writes as "*" instead of
        /// listing the columns. Compared by reference, so it cannot collide with a real column set.
        /// </summary>
        private static readonly HashSet<string> kAllColumns = new() { kAllColumnsMarker };

        /// <summary>
        /// Root field names two tables of kMetadataWithDistinctTableRoots reach, where the table of a
        /// schema other than public carries its schema and the one of public does not.
        /// </summary>
        private static readonly List<string> kDistinctTableRoots = new() { "alert", "modelling_connection" };

        /// <summary>
        /// A metadata document tracking two tables which reach two root field names, as the tracked tables
        /// of replace_metadata.json do.
        /// </summary>
        private const string kMetadataWithDistinctTableRoots = @"{""args"": {""metadata"": {""sources"": [
            {""tables"": [
                {""table"": {""schema"": ""public"", ""name"": ""alert""}},
                {""table"": {""schema"": ""modelling"", ""name"": ""connection""}}
            ]}
        ]}}}";

        /// <summary>
        /// A metadata document tracking two tables which reach one root field name, because the schema a
        /// table outside public is prefixed with is part of the name of the other one.
        /// </summary>
        private const string kMetadataWithAmbiguousTableRoot = @"{""args"": {""metadata"": {""sources"": [
            {""tables"": [
                {""table"": {""schema"": ""modelling"", ""name"": ""connection""}},
                {""table"": {""schema"": ""public"", ""name"": ""modelling_connection""}}
            ]}
        ]}}}";

        /// <summary>
        /// The sources under test. They are looked up once and are read from wherever the environment
        /// keeps them, because the tests run from a repository checkout as well as from an installed
        /// system, where the components do not share the layout of the repository.
        /// </summary>
        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(LocateMetadata);
        private static readonly Lazy<DirectoryInfo?> kApiCallDirectory = new(LocateApiCalls);
        private static readonly Lazy<ApiCallSurvey> kApiCallSurvey = new(ReadApiCalls);

        /// <summary>
        /// Mismatches which are accepted although they exist. Every entry breaks the API call named with
        /// it for the role named with it, and closing one widens the read access of that role, which is a
        /// decision for the owner of the respective workflow rather than one to take while repairing an
        /// unrelated call. ApiCalls_KnownPermissionGapsStillExist removes the risk of an entry outliving
        /// the mismatch it accepts.
        /// </summary>
        private static readonly List<string> kKnownPermissionGaps = new()
        {
            // The importer role holds insert permission on recertification but none of the three calls is
            // issued by the importer: FWO.Recert sends them under the role of the logged in user
            // (RecertRefresh.cs, RecertHandler.cs). Granting the importer select on recertification would
            // widen its read access for a call it never makes. Dropping its insert permission instead is
            // the cleaner repair and belongs to the owner of the recertification workflow. Tracked as
            // issue #5220.
            "recertification|importer|<any column>",

            // The middleware-server role holds insert permission on report but report/addGeneratedReport
            // is sent over the user context connection of the scheduling user (ReportJob.cs), never under
            // the technical role. Granting it select on report would widen its read access to every
            // generated report for a call it never makes; dropping the insert permission is the cleaner
            // repair and belongs to the owner of the report scheduling workflow. Tracked as issue #5220.
            "report|middleware-server|<any column>",

            // v_rule_with_rule_owner_1 is tracked with no permission at all, so owner/getRuleOwnerships
            // exists in the schema of no role of this metadata. /settings/owners is open to admin and
            // auditor: admin is the built in role of Hasura and passes every permission, auditor is not
            // and its EditOwner.razor fails to load the rules of an owner. Which role should read the view
            // is a decision for the owner of the ownership workflow. Tracked as issue #5210.
            "v_rule_with_rule_owner_1|<any role>|owner_id"
        };

        /// <summary>
        /// Every column an API call filters a delete or update on has to be selectable by the roles which
        /// are allowed to run that mutation, otherwise the column is no field of their boolean expression.
        /// </summary>
        [Test]
        public void ApiCalls_FilterOnlyColumnsTheirRolesMaySelect()
        {
            AssertNoGaps(CollectGaps(FindFilterGaps),
                "A role may run these mutations but cannot select the columns they filter on:");
        }

        /// <summary>
        /// Every column a mutation returns has to be selectable by the roles which are allowed to run it,
        /// because a mutation response is read through the select permission of the role.
        /// </summary>
        [Test]
        public void ApiCalls_ReturnOnlyColumnsTheirRolesMaySelect()
        {
            AssertNoGaps(CollectGaps(FindReturnGaps),
                "A role may run these mutations but cannot select the columns they return:");
        }

        /// <summary>
        /// A query filtering on columns no role at all may select cannot be issued by anybody, so it is
        /// broken however it is called. Which role issues a query is recorded nowhere, which is why only
        /// this direction of the check is decidable.
        /// </summary>
        [Test]
        public void ApiCalls_QueriesAreRunnableByAtLeastOneRole()
        {
            AssertNoGaps(CollectGaps(FindUnrunnableQueryGaps),
                "No role can select all the columns these queries filter on:");
        }

        /// <summary>
        /// A mutation on a table which is not tracked in the metadata does not exist in the API schema at
        /// all, so an unknown table name is a typo or a table someone forgot to track.
        /// </summary>
        [Test]
        public void ApiCalls_AddressOnlyTrackedTables()
        {
            SkipWithoutSources();

            Dictionary<string, TableMetadata> tables = ReadMetadata();
            List<string> unknownTables = kApiCallSurvey.Value.Operations
                .Where(operation => operation.Operation != kSelectOperation)
                .Where(operation => !tables.ContainsKey(operation.TableRoot))
                .Select(operation => $"{operation.Source}: {operation.TableRoot}")
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
            SkipWithoutSources();

            HashSet<string> existingGaps = new(CollectGaps(FindFilterGaps, false)
                .Concat(CollectGaps(FindReturnGaps, false))
                .Concat(CollectGaps(FindUnrunnableQueryGaps, false))
                .Select(gap => gap.Gap));
            List<string> repairedGaps = kKnownPermissionGaps.Where(gap => !existingGaps.Contains(gap)).ToList();

            Assert.That(repairedGaps, Is.Empty,
                "These accepted mismatches no longer exist and have to be removed from kKnownPermissionGaps:"
                + Environment.NewLine + string.Join(Environment.NewLine, repairedGaps));
        }

        /// <summary>
        /// Tables outside the public schema carry their schema in the root field name Hasura gives them,
        /// which is the name the API calls address them by and the name they are read under here.
        /// </summary>
        [Test]
        public void Metadata_ReadsTablesByTheirRootFieldName()
        {
            Dictionary<string, TableMetadata> tables = ReadMetadata(kMetadataWithDistinctTableRoots);

            Assert.That(tables.Keys, Is.EquivalentTo(kDistinctTableRoots));
        }

        /// <summary>
        /// Two tracked tables reaching one root field name are refused, because the calls of one of them
        /// would otherwise be checked against the permissions of the other and pass on them.
        /// </summary>
        [Test]
        public void Metadata_RefusesTwoTablesReachingOneRootFieldName()
        {
            InvalidOperationException? refusal = Assert.Throws<InvalidOperationException>(
                () => ReadMetadata(kMetadataWithAmbiguousTableRoot));

            Assert.That(refusal?.Message, Does.Contain("modelling_connection"));
        }

        /// <summary>
        /// Fails with the gaps a check found, naming the API call which uncovered each of them.
        /// </summary>
        private static void AssertNoGaps(List<ReportedGap> gaps, string message)
        {
            List<string> reported = gaps.Select(gap => $"{gap.Source}: {gap.Gap}").ToList();
            Assert.That(reported, Is.Empty,
                message + Environment.NewLine + string.Join(Environment.NewLine, reported));
        }

        /// <summary>
        /// Runs one check over every API call operation addressing a tracked table.
        /// </summary>
        /// <param name="findGaps">The check, returning one entry per gap in the format of FormatGap.</param>
        /// <param name="skipKnownGaps">Whether the accepted mismatches are filtered out.</param>
        private static List<ReportedGap> CollectGaps(
            Func<ApiOperation, TableMetadata, IEnumerable<string>> findGaps, bool skipKnownGaps = true)
        {
            SkipWithoutSources();
            ReportUncheckableCalls();

            Dictionary<string, TableMetadata> tables = ReadMetadata();
            List<ReportedGap> gaps = new();
            foreach (ApiOperation operation in kApiCallSurvey.Value.Operations)
            {
                if (tables.TryGetValue(operation.TableRoot, out TableMetadata? table))
                {
                    gaps.AddRange(findGaps(operation, table)
                        .Where(gap => !skipKnownGaps || !kKnownPermissionGaps.Contains(gap))
                        .Select(gap => new ReportedGap(operation.Source, gap)));
                }
            }
            return gaps;
        }

        /// <summary>
        /// Collects the roles which may run the mutation but cannot select one of the filtered columns.
        /// </summary>
        private static IEnumerable<string> FindFilterGaps(ApiOperation operation, TableMetadata table)
        {
            if (operation.Operation == kSelectOperation)
            {
                yield break;
            }

            foreach (string role in RolesRunning(operation, table))
            {
                foreach (string column in operation.FilterColumns.Where(column => !table.MaySelect(role, column)))
                {
                    yield return FormatGap(operation.TableRoot, role, column);
                }
            }
        }

        /// <summary>
        /// Collects the roles which may run the mutation but cannot select what it returns. A mutation
        /// returning nothing but affected_rows reads nothing and needs no select permission at all.
        /// </summary>
        private static IEnumerable<string> FindReturnGaps(ApiOperation operation, TableMetadata table)
        {
            if (operation.Operation == kSelectOperation || !operation.ReadsItsResult)
            {
                yield break;
            }

            foreach (string role in RolesRunning(operation, table))
            {
                if (!table.MaySelectAnything(role))
                {
                    yield return FormatGap(operation.TableRoot, role, kAnyColumn);
                    continue;
                }
                foreach (string column in operation.ReturnedColumns.Where(column => !table.MaySelect(role, column)))
                {
                    yield return FormatGap(operation.TableRoot, role, column);
                }
            }
        }

        /// <summary>
        /// Reports a query whose filter no role can satisfy, which makes it unusable for everybody.
        /// </summary>
        private static IEnumerable<string> FindUnrunnableQueryGaps(ApiOperation operation, TableMetadata table)
        {
            if (operation.Operation != kSelectOperation || operation.FilterColumns.Count == 0
                || table.AnyRoleMaySelectAll(operation.FilterColumns))
            {
                yield break;
            }

            foreach (string column in operation.FilterColumns)
            {
                yield return FormatGap(operation.TableRoot, kAnyRole, column);
            }
        }

        /// <summary>
        /// The roles which can run a mutation. A mutation writing a column the role may not write is
        /// rejected for that column already, so its where clause and its response never reach the role and
        /// demanding a select permission for it would only invite widening the read access for nothing.
        /// </summary>
        private static IEnumerable<string> RolesRunning(ApiOperation operation, TableMetadata table)
        {
            if (!table.RolesByOperation.TryGetValue(operation.Operation, out List<string>? roles))
            {
                return new List<string>();
            }
            return roles.Where(role => table.MayWrite(operation, role));
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
        /// Writes what the checks could not look at to the test output, so a call growing beyond what the
        /// reader understands is visible instead of counting as checked.
        /// </summary>
        private static void ReportUncheckableCalls()
        {
            List<string> uncheckable = kApiCallSurvey.Value.Uncheckable;
            TestContext.Out.WriteLine($"{uncheckable.Count} part(s) of the API calls cannot be checked statically:");
            foreach (string note in uncheckable)
            {
                TestContext.Out.WriteLine($"  {note}");
            }
        }

        /// <summary>
        /// Reads the tables of the metadata file by the name their root fields carry in the GraphQL schema.
        /// </summary>
        private static Dictionary<string, TableMetadata> ReadMetadata()
        {
            FileInfo metadataFile = kMetadataFileInfo.Value
                ?? throw new InvalidOperationException(kMetadataOutOfReach);
            return ReadMetadata(File.ReadAllText(metadataFile.FullName));
        }

        /// <summary>
        /// Reads the tables of a metadata document by the name their root fields carry in the GraphQL
        /// schema. Two tracked tables reaching the same root name are refused instead of one of them
        /// replacing the other, because the calls of one table would then be checked against the
        /// permissions of the other and pass on them, which is the one thing this guard must not do.
        /// </summary>
        /// <param name="metadataJson">The content of the metadata file.</param>
        private static Dictionary<string, TableMetadata> ReadMetadata(string metadataJson)
        {
            using JsonDocument metadata = JsonDocument.Parse(metadataJson);
            Dictionary<string, TableMetadata> tables = new();

            foreach (JsonElement source in metadata.RootElement.GetProperty("args")
                .GetProperty("metadata").GetProperty("sources").EnumerateArray())
            {
                foreach (JsonElement table in source.GetProperty("tables").EnumerateArray())
                {
                    string tableRoot = ReadTableRoot(table.GetProperty("table"));
                    if (!tables.TryAdd(tableRoot, ReadTablePermissions(table)))
                    {
                        throw new InvalidOperationException(kAmbiguousTableRoot + tableRoot);
                    }
                }
            }
            return tables;
        }

        /// <summary>
        /// Builds the name the root fields of a table carry, which Hasura prefixes with the schema for
        /// every schema but public.
        /// </summary>
        private static string ReadTableRoot(JsonElement table)
        {
            string schema = table.GetProperty("schema").GetString() ?? "";
            string name = table.GetProperty("name").GetString() ?? "";
            return schema == kPublicSchema ? name : $"{schema}_{name}";
        }

        /// <summary>
        /// Reads which roles hold which permission on a table, which columns each role may select or
        /// write, and the relationships of the table, which are no columns of it.
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
                    AddPermittedColumns(metadata, operation, role, entry);
                }
            }
            ReadRelationships(table, metadata.Relationships);
            return metadata;
        }

        /// <summary>
        /// Records the columns one permission entry grants, for the operations whose columns are checked.
        /// </summary>
        private static void AddPermittedColumns(TableMetadata metadata, string operation, string role,
            JsonElement entry)
        {
            if (operation == kSelectOperation)
            {
                metadata.SelectableColumns[role] = ReadPermittedColumns(entry);
            }
            else if (operation == kUpdateOperation || operation == kInsertOperation)
            {
                metadata.WritableColumns[(operation, role)] = ReadPermittedColumns(entry);
            }
        }

        /// <summary>
        /// Reads the names of the relationships of a table, which appear in a filter and in a selection set
        /// like a column but address another table.
        /// </summary>
        private static void ReadRelationships(JsonElement table, HashSet<string> relationships)
        {
            foreach (string kind in kRelationshipKinds)
            {
                if (table.TryGetProperty(kind, out JsonElement declared))
                {
                    foreach (JsonElement relationship in declared.EnumerateArray())
                    {
                        relationships.Add(relationship.GetProperty("name").GetString() ?? "");
                    }
                }
            }
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
        /// Reads the columns of one permission entry. A delete permission carries no columns at all, and
        /// Hasura writes a permission covering every column as "*" instead of listing them.
        /// </summary>
        private static HashSet<string> ReadPermittedColumns(JsonElement permissionEntry)
        {
            if (!permissionEntry.GetProperty("permission").TryGetProperty("columns", out JsonElement declared))
            {
                return new();
            }
            if (declared.ValueKind == JsonValueKind.String)
            {
                return declared.GetString() == kAllColumnsMarker ? kAllColumns : new();
            }

            HashSet<string> columns = new();
            if (declared.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement column in declared.EnumerateArray())
                {
                    columns.Add(column.GetString() ?? "");
                }
            }
            return columns;
        }


        /// <summary>
        /// One root field of an API call: what it does to which table, what it filters on, what it returns
        /// and what it writes.
        /// </summary>
        /// <param name="ReadsItsResult">Whether the call reads anything of the rows it touches, which a
        /// mutation returning nothing but affected_rows does not.</param>
        private sealed record ApiOperation(string Source, string Operation, string TableRoot,
            List<string> FilterColumns, List<string> ReturnedColumns, List<string> WrittenColumns,
            bool ReadsItsResult);

        /// <summary>
        /// One field of a selection set, or the placeholder for a fragment spread standing in for fields
        /// which are defined in another file.
        /// </summary>
        private sealed record ApiField(string Name, string? Arguments, string? SubSelection, bool IsFragmentSpread)
        {
            public static ApiField FragmentSpread { get; } = new("", null, null, true);
        }

        /// <summary>
        /// One missing permission together with the API call which uncovered it.
        /// </summary>
        private sealed record ReportedGap(string Source, string Gap);

        /// <summary>
        /// What the API call definitions hold: the operations to check, and the parts of them no static
        /// check can read.
        /// </summary>
        private sealed class ApiCallSurvey
        {
            public List<ApiOperation> Operations { get; } = new();
            public List<string> Uncheckable { get; } = new();
        }

        /// <summary>
        /// The permissions of one table: which roles hold which permission, which columns they may select
        /// or write, and the relationships which are no columns of the table.
        /// </summary>
        private sealed class TableMetadata
        {
            public Dictionary<string, List<string>> RolesByOperation { get; } = new();
            public Dictionary<string, HashSet<string>> SelectableColumns { get; } = new();
            public Dictionary<(string Operation, string Role), HashSet<string>> WritableColumns { get; } = new();
            public HashSet<string> Relationships { get; } = new();

            /// <summary>
            /// Whether a role may select a column, which a relationship never is.
            /// </summary>
            public bool MaySelect(string role, string column)
            {
                if (Relationships.Contains(column))
                {
                    return true;
                }
                return SelectableColumns.TryGetValue(role, out HashSet<string>? columns)
                    && (ReferenceEquals(columns, kAllColumns) || columns.Contains(column));
            }

            /// <summary>
            /// Whether a role holds select permission on the table at all, which a _by_pk root field and a
            /// returning block need before any single column matters.
            /// </summary>
            public bool MaySelectAnything(string role)
            {
                return SelectableColumns.ContainsKey(role);
            }

            /// <summary>
            /// Whether any role at all may select every one of the given columns.
            /// </summary>
            public bool AnyRoleMaySelectAll(List<string> columns)
            {
                return SelectableColumns.Keys.Any(role => columns.TrueForAll(column => MaySelect(role, column)));
            }

            /// <summary>
            /// Whether a role may write every column a mutation writes. A mutation writing a column the
            /// role may not write is rejected for that column already and never runs for that role.
            /// </summary>
            public bool MayWrite(ApiOperation operation, string role)
            {
                if (operation.WrittenColumns.Count == 0)
                {
                    return true;
                }
                return WritableColumns.TryGetValue((operation.Operation, role), out HashSet<string>? columns)
                    && (ReferenceEquals(columns, kAllColumns) || operation.WrittenColumns.TrueForAll(columns.Contains));
            }
        }
    }
}

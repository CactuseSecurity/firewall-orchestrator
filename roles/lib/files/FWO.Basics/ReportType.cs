namespace FWO.Basics
{
    public enum ReportType
    {
        Undefined = 0,
        Rules = 1,
        Changes = 2,
        Statistics = 3,
        NatRules = 4,
        ResolvedRules = 5,
        ResolvedRulesTech = 6,
        Recertification = 7,
        ResolvedChanges = 8,
        ResolvedChangesTech = 9,
        UnusedRules = 10,

        Connections = 21,
        AppRules = 22,
        VarianceAnalysis = 23,
        OwnerRecertification = 24,
        RecertificationEvent = 25,
        RecertEventReport = 26,

        ComplianceReport = 31,
        ComplianceDiffReport = 32,

        TicketReport = 41,
        TicketChangeReport = 42,

        Owners = 51
    }

    /// <summary>
    /// Per-role override for whether a report type is shown/usable. "Inherited" falls back to the
    /// standard role-category and global-availability rules; "Visible"/"NotVisible" override them.
    /// </summary>
    public enum ReportTypeVisibilityOption
    {
        Inherited = 0,
        Visible = 1,
        NotVisible = 2
    }

    /// <summary>
    /// Parses and serializes the per-role report-type visibility overrides stored in
    /// <c>ConfigData.ReportTypeVisibilityByRole</c>. Report types are keyed by their underlying
    /// int value on the wire (not by enum name) to keep JSON dictionary-key serialization simple
    /// and stable.
    /// </summary>
    public static class ReportTypeRoleVisibilityConfig
    {
        public static Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> Parse(string? json)
        {
            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> result = [];
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            Dictionary<string, Dictionary<int, ReportTypeVisibilityOption>>? raw;
            try
            {
                raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, ReportTypeVisibilityOption>>>(json);
            }
            catch (System.Text.Json.JsonException)
            {
                return result;
            }

            if (raw == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, Dictionary<int, ReportTypeVisibilityOption>> roleEntry in raw)
            {
                Dictionary<ReportType, ReportTypeVisibilityOption> byType = [];
                foreach (KeyValuePair<int, ReportTypeVisibilityOption> typeEntry in roleEntry.Value)
                {
                    byType[(ReportType)typeEntry.Key] = typeEntry.Value;
                }
                result[roleEntry.Key] = byType;
            }
            return result;
        }

        public static string Serialize(Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> data)
        {
            Dictionary<string, Dictionary<int, ReportTypeVisibilityOption>> raw = [];
            foreach (KeyValuePair<string, Dictionary<ReportType, ReportTypeVisibilityOption>> roleEntry in data)
            {
                Dictionary<int, ReportTypeVisibilityOption> byType = [];
                foreach (KeyValuePair<ReportType, ReportTypeVisibilityOption> typeEntry in roleEntry.Value)
                {
                    // Only persist explicit overrides - "Inherited" is the implicit default and need not be stored.
                    if (typeEntry.Value != ReportTypeVisibilityOption.Inherited)
                    {
                        byType[(int)typeEntry.Key] = typeEntry.Value;
                    }
                }
                if (byType.Count > 0)
                {
                    raw[roleEntry.Key] = byType;
                }
            }
            return System.Text.Json.JsonSerializer.Serialize(raw);
        }

        public static ReportTypeVisibilityOption GetOption(
            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> data, string role, ReportType reportType)
        {
            // Role names come from LDAP both when the setting is saved and when it is looked up,
            // but compare case-insensitively to stay consistent with ExecutionModeHelper's role matching.
            KeyValuePair<string, Dictionary<ReportType, ReportTypeVisibilityOption>> roleEntry = data.FirstOrDefault(
                entry => entry.Key.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (roleEntry.Value != null && roleEntry.Value.TryGetValue(reportType, out ReportTypeVisibilityOption option))
            {
                return option;
            }
            return ReportTypeVisibilityOption.Inherited;
        }
    }

    public static class ReportTypeGroups
    {
        public static bool IsRuleReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Rules or
                ReportType.ResolvedRules or
                ReportType.ResolvedRulesTech or
                ReportType.NatRules or
                ReportType.Recertification or
                ReportType.UnusedRules or
                ReportType.AppRules or
                ReportType.RecertEventReport => true,
                _ => false
            };
        }

        public static bool IsChangeReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Changes or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech => true,
                _ => false
            };
        }

        public static bool IsResolvedReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.ResolvedRules or
                ReportType.ResolvedRulesTech or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech or
                ReportType.ComplianceReport or
                ReportType.ComplianceDiffReport => true,
                _ => false,
            };
        }

        public static bool IsTechReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.ResolvedRulesTech or
                ReportType.ResolvedChangesTech => true,
                _ => false
            };
        }

        public static bool IsDeviceRelatedReport(this ReportType reportType)
        {
            return reportType.IsRuleReport() || reportType.IsChangeReport() || reportType == ReportType.Statistics;
        }

        public static bool IsConnectionRelatedReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Connections or
                ReportType.VarianceAnalysis or
                ReportType.RecertificationEvent => true,
                _ => false
            };
        }

        public static bool IsModellingReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Connections or
                ReportType.AppRules or
                ReportType.VarianceAnalysis or
                ReportType.OwnerRecertification or
                ReportType.RecertificationEvent or
                ReportType.RecertEventReport => true,
                _ => false
            };
        }

        public static bool IsOwnerReport(this ReportType reportType)
        {
            return reportType == ReportType.Owners || reportType == ReportType.OwnerRecertification;
        }

        public static bool IsComplianceReport(this ReportType reportType)
        {
            return reportType == ReportType.ComplianceReport || reportType == ReportType.ComplianceDiffReport;
        }

        public static bool IsRulebaseReport(this ReportType reportType)
        {
            return reportType == ReportType.Recertification || reportType == ReportType.AppRules;
        }

        public static bool IsWorkflowReport(this ReportType reportType)
        {
            return reportType == ReportType.TicketReport || reportType == ReportType.TicketChangeReport;
        }

        public static bool IsArchiveOnlyReport(this ReportType reportType)
        {
            return reportType == ReportType.RecertificationEvent;
        }

        public static bool HasTimeFilter(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Rules or
                ReportType.ResolvedRules or
                ReportType.ResolvedRulesTech or
                ReportType.NatRules or
                ReportType.Statistics or
                ReportType.Changes or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech or
                ReportType.TicketChangeReport => true,
                _ => false
            };
        }

        public static bool SupportsCsvExport(this ReportType reportType, bool detailedView = false)
        {
            return reportType.IsResolvedReport()
                || reportType.IsComplianceReport()
                || reportType.IsOwnerReport()
                || reportType.IsWorkflowReport() && !detailedView;
        }

        /// <summary>
        /// Determines whether a report type supports HTML export.
        /// </summary>
        public static bool SupportsHtmlExport(this ReportType reportType)
        {
            return !reportType.IsComplianceReport();
        }

        /// <summary>
        /// Determines whether a report type supports PDF export.
        /// </summary>
        public static bool SupportsPdfExport(this ReportType reportType)
        {
            return reportType.SupportsHtmlExport();
        }

        public static List<ReportType> AllReportTypes()
        {
            return [.. Enum.GetValues(typeof(ReportType)).Cast<ReportType>().Where(r => r != ReportType.Undefined)];
        }

        public static List<ReportType> ReportTypeSelection(ReportVisibility? visibility = null)
        {
            return CustomSortReportType([.. Enum.GetValues(typeof(ReportType)).Cast<ReportType>()], visibility ?? new(true, true, true, true, true));
        }

        public static bool IsVisibleTemplateType(this ReportType reportType, ReportVisibility visibility, bool modellingOwnerAllowed = true)
        {
            return !reportType.IsArchiveOnlyReport() && (
                visibility.RuleRelated && reportType.IsDeviceRelatedReport() && !reportType.IsModellingReport()
                || visibility.ModellingRelated && reportType.IsModellingReport() && (modellingOwnerAllowed || reportType.IsOwnerReport())
                || visibility.OwnerRelated && reportType == ReportType.Owners
                || visibility.ComplianceRelated && reportType.IsComplianceReport()
                || visibility.WorkflowRelated && reportType.IsWorkflowReport());
        }

        /// <summary>
        /// Sorts <paramref name="ListIn"/> into the canonical report-type display order.
        /// </summary>
        /// <param name="filterByVisibility">
        /// When true (the default), entries are additionally required to pass <see cref="IsVisibleTemplateType"/>
        /// against <paramref name="visibility"/> - suitable when <paramref name="ListIn"/> has not yet been
        /// filtered for visibility (e.g. <see cref="ReportTypeSelection"/>). Pass false when the caller has
        /// already computed the visible set per item (e.g. via <c>UserConfig.CanUseReportType</c>, which also
        /// honours per-role visibility overrides), so this call only sorts without discarding entries that the
        /// coarse-grained <paramref name="visibility"/> alone would not have allowed.
        /// </param>
        public static List<ReportType> CustomSortReportType(List<ReportType> ListIn, ReportVisibility visibility, bool filterByVisibility = true)
        {
            List<ReportType> ListOut = [];
            List<ReportType> orderedReportTypeList =
            [
                ReportType.Undefined,
                ReportType.RecertificationEvent,
                ReportType.Rules, ReportType.ResolvedRules, ReportType.ResolvedRulesTech, ReportType.UnusedRules, ReportType.NatRules,
                ReportType.Changes, ReportType.ResolvedChanges, ReportType.ResolvedChangesTech,
                ReportType.Statistics,
                ReportType.Connections,
                ReportType.AppRules,
                ReportType.VarianceAnalysis,
                ReportType.Recertification,
                ReportType.OwnerRecertification,
                ReportType.RecertEventReport,
                ReportType.Owners,
                ReportType.TicketReport,
                ReportType.TicketChangeReport
            ];
            foreach (var reportType in orderedReportTypeList.Where(r => ListIn.Contains(r)))
            {
                if (reportType == ReportType.Undefined || !filterByVisibility || reportType.IsVisibleTemplateType(visibility))
                {
                    ListOut.Add(reportType);
                }
                ListIn.Remove(reportType);
            }
            // Finally add remaining report types, filtering by visibility unless the caller already did so.
            ListOut.AddRange(filterByVisibility ? ListIn.Where(reportType => reportType.IsVisibleTemplateType(visibility)) : ListIn);
            return ListOut;
        }
    }
}

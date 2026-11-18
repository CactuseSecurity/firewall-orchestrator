using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Shipped Akamai Guardicore Segmentation calls, all of REST API v4.0 except the login, for which v3.0 is the only
    /// version. Groups are written as labels with IP criteria (dynamic IP labels): a label that does not exist yet is
    /// created with the template of <see cref="FirewallWriteOperation.LabelWrite"/>; the criteria of an existing label are
    /// replaced by the client through the bulk update (override, not append), and a label value is deleted by its id,
    /// which the client looks up. Label keys are never deleted. Rules are not written yet.
    /// </summary>
    public static class GuardicoreWriteDefaults
    {
        /// <summary>Path of the label list, filtered by key and value to find a label.</summary>
        public const string kLabelsPath = "/api/v4.0/labels";

        /// <summary>Path of the bulk label creation.</summary>
        public const string kLabelsBulkPath = "/api/v4.0/labels/bulk";

        /// <summary>Path of the login; Guardicore offers the login in REST API v3.0 only.</summary>
        public const string kAuthenticatePath = "/api/v3.0/authenticate";

        private const string kLabelCreateBody = """[{"key":"@@LABEL_KEY@@","value":"@@LABEL_VALUE@@","criteria":@@LABEL_CRITERIA@@}]""";

        /// <summary>Shipped calls per operation.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.LabelWrite, new($"POST {kLabelsBulkPath}", kLabelCreateBody) },
                { FirewallWriteOperation.LabelDelete, new($"DELETE {kLabelsPath}/@@LABEL_ID@@", "") }
            };
    }
}

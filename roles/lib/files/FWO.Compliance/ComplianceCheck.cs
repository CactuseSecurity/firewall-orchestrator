using FWO.Api.Client;
using FWO.Basics.Interfaces;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services;
using System.Collections.Concurrent;

namespace FWO.Compliance
{
    /// <summary>
    /// Provides the state and methods required to evaluate how well
    /// firewall management rules comply with the defined compliance policy.
    ///
    /// The <c>ComplianceCheck</c> class encapsulates the logic used to analyze
    /// rule configurations, identify deviations from policy requirements,
    /// and deliver a structured assessment of compliance status.
    /// </summary>
    public partial class ComplianceCheck
    {
        /// <summary>
        /// Active policy that defines the compliance criteria.
        /// </summary>
        public CompliancePolicy? Policy = null;

        /// <summary>
        /// Network zones to use for matrix compliance check.
        /// </summary>
        public List<ComplianceNetworkZone> NetworkZones { get; set; } = [];

        /// <summary>
        /// Wraps the static class FWO.Logging.Log to make it accessible for unit tests.
        /// </summary>
        public ILogger Logger { get; set; } = new Logger();

        /// <summary>
        /// Violations found in the last run of CheckAll.
        /// </summary>
        public List<ComplianceViolation> CurrentViolationsInCheck { get; private set; } = [];

        /// <summary>
        /// Rules that are to be evaluated in the next run of CheckAll.
        /// </summary>
        public List<Rule>? RulesInCheck { get; set; } = [];

        /// <summary>
        /// Managements that are the subjects of the check.
        /// </summary>
        public List<Management>? Managements { get; set; } = [];

        private readonly ApiConnection _apiConnection;
        private readonly UserConfig _userConfig;
        private bool _treatDomainAndDynamicObjectsAsInternet = false;
        private bool _autoCalculatedInternetZoneActive = false;
        private int _complianceCheckPolicyId = 0;
        private int _elementsPerFetch;
        private int _maxDegreeOfParallelism;
        private readonly ConcurrentBag<ComplianceViolationBase> _violationsToAdd = new();
        private readonly ConcurrentBag<ComplianceViolation> _violationsToRemove = new();
        private readonly ConcurrentBag<ComplianceViolation> _currentViolations = new();
        private readonly ParallelProcessor _parallelProcessor;

        /// <summary>
        /// Constructor for compliance check
        /// </summary>
        /// <param name="userConfig">User configuration</param>
        /// <param name="apiConnection">Api connection</param>
        /// <param name="logger">Log</param>
        public ComplianceCheck(UserConfig userConfig, ApiConnection apiConnection, ILogger? logger = null)
        {
            _apiConnection = apiConnection;
            _userConfig = userConfig;

            if (logger != null)
            {
                Logger = logger;
            }

            _parallelProcessor = new(apiConnection, Logger);

            if (_userConfig.GlobalConfig == null)
            {
                Logger.TryWriteInfo("Compliance Check", "Global config not found.", _userConfig.GlobalConfig == null);
            }
        }
    }
}

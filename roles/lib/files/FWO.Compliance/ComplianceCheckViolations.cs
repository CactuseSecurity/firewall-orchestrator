using FWO.Basics.Enums;
using FWO.Data;
using FWO.Data.Extensions;
using FWO.Services.Triviality;
using FWO.Ui.Display;

namespace FWO.Compliance
{
    /// <summary>
    /// Creates the violations recorded by a compliance check run.
    /// </summary>
    public partial class ComplianceCheck
    {
        /// <summary>
        /// Records a not-assessable violation for every object that could not be assigned to a network zone.
        /// </summary>
        /// <param name="rule">Rule under test.</param>
        /// <param name="criterion">Matrix criterion.</param>
        /// <param name="notAssessableSources">Source objects without an assignable zone.</param>
        /// <param name="notAssessableDestinations">Destination objects without an assignable zone.</param>
        /// <returns>True when every object could be assigned to a zone.</returns>
        private bool CreateZoneAssessabilityViolations(
            Rule rule,
            ComplianceCriterion criterion,
            List<NetworkObject> notAssessableSources,
            List<NetworkObject> notAssessableDestinations)
        {
            // Flattening groups can yield the same object more than once, which would record the same
            // violation twice.
            foreach (NetworkObject source in notAssessableSources.Distinct())
            {
                CreateZoneAssessabilityViolation(rule, criterion, source, true);
            }

            foreach (NetworkObject destination in notAssessableDestinations.Distinct())
            {
                CreateZoneAssessabilityViolation(rule, criterion, destination, false);
            }

            return notAssessableSources.Count == 0 && notAssessableDestinations.Count == 0;
        }

        /// <summary>
        /// Records a not-assessable violation for a criterion whose own type does not describe it. The type is
        /// stated explicitly because everything that reads a violation back derives the type from the criterion
        /// as long as none was recorded.
        /// </summary>
        /// <param name="rule">Impacted rule.</param>
        /// <param name="complianceCheckResult">Details assembled during the check.</param>
        /// <param name="detailsOverride">Optional string used if details need to be customized.</param>
        private void CreateNotAssessableViolation(Rule rule, ComplianceCheckResult complianceCheckResult, string? detailsOverride = null)
        {
            ComplianceViolation? violation = CreateViolation(ComplianceViolationType.NotAssessable, rule, complianceCheckResult, detailsOverride);

            if (violation != null)
            {
                violation.Type = ComplianceViolationType.NotAssessable;
            }
        }

        /// <summary>
        /// Records a single not-assessable violation for an object without an assignable network zone.
        /// </summary>
        /// <param name="rule">Rule under test.</param>
        /// <param name="criterion">Matrix criterion.</param>
        /// <param name="networkObject">Object that could not be assigned to a zone.</param>
        /// <param name="isSource">Whether the object is used as source of the rule.</param>
        private void CreateZoneAssessabilityViolation(Rule rule, ComplianceCriterion criterion, NetworkObject networkObject, bool isSource)
        {
            ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.NotAssessable)
            {
                Criterion = criterion,
                AssessabilityIssue = AssessabilityIssue.NoMatchingZone
            };

            if (isSource)
            {
                complianceCheckResult.Source = networkObject;
            }
            else
            {
                complianceCheckResult.Destination = networkObject;
            }

            CreateNotAssessableViolation(rule, complianceCheckResult);
        }

        /// <summary>
        /// Records the violations described by a triviality result.
        /// </summary>
        /// <param name="rule">Rule that triggered the violations.</param>
        /// <param name="criterion">Triggered criterion.</param>
        /// <param name="result">Triviality evaluator result.</param>
        /// <param name="violationType">Violation type of the criterion itself.</param>
        private void CreateTrivialityViolations(Rule rule, ComplianceCriterion criterion, TrivialityCheckResult result, ComplianceViolationType violationType)
        {
            if (RuleTrivialityEvaluator.IsNotAssessableReason(result.Reason))
            {
                // The criterion could not be evaluated for any object of the rule, so the rule is reported as
                // not assessable instead of as a violation of a criterion that never ran.
                CreateTrivialityViolation(rule, criterion, ComplianceViolationType.NotAssessable, result.Reason);
                return;
            }

            CreateTrivialityViolation(rule, criterion, violationType, result.Reason);

            if (result.NotAssessableReason != null)
            {
                // The criterion found a violation among the objects it could assess. The remaining objects are
                // reported separately, so that neither of the two facts hides the other.
                CreateTrivialityViolation(rule, criterion, ComplianceViolationType.NotAssessable, result.NotAssessableReason);
            }
        }

        /// <summary>
        /// Records a single violation of a criterion that is modeled as a triviality check.
        /// </summary>
        /// <param name="rule">Rule that triggered the violation.</param>
        /// <param name="criterion">Triggered criterion.</param>
        /// <param name="violationType">Type of violation to record.</param>
        /// <param name="reason">Reason reported by the triviality evaluator.</param>
        private void CreateTrivialityViolation(Rule rule, ComplianceCriterion criterion, ComplianceViolationType violationType, string reason)
        {
            ComplianceCheckResult complianceCheckResult = new(rule, violationType)
            {
                Criterion = criterion
            };

            string details = GetTrivialityViolationDetails(rule, criterion, reason);

            if (violationType == ComplianceViolationType.NotAssessable)
            {
                CreateNotAssessableViolation(rule, complianceCheckResult, details);
                return;
            }

            CreateViolation(violationType, rule, complianceCheckResult, details);
        }

        /// <summary>
        /// Builds localized detail strings for triviality-backed criteria.
        /// </summary>
        /// <param name="rule">Rule that triggered the violation.</param>
        /// <param name="criterion">Triggered criterion.</param>
        /// <param name="reason">Reason reported by the triviality evaluator.</param>
        private string GetTrivialityViolationDetails(Rule rule, ComplianceCriterion criterion, string reason)
        {
            return reason switch
            {
                RuleTrivialityEvaluator.MinimumCIDRLengthReason =>
                    $"{_userConfig.GetText("minimum_cidr_length_violation")}: {_userConfig.GetText("criterion_value")} {criterion.Content}",
                RuleTrivialityEvaluator.ForbidZonesAsSourceReason =>
                    $"{_userConfig.GetText("zone_object_source_violation")}: {rule.Uid}; {_userConfig.GetText("criterion_value")} {criterion.Content}",
                RuleTrivialityEvaluator.ForbidZonesAsDestinationReason =>
                    $"{_userConfig.GetText("zone_object_destination_violation")}: {rule.Uid}; {_userConfig.GetText("criterion_value")} {criterion.Content}",
                RuleTrivialityEvaluator.ForbidBidirectionalDuplicateReason =>
                    $"{_userConfig.GetText("bidirectional_duplicate_violation")}: {rule.Uid}",
                RuleTrivialityEvaluator.Ipv6NotSupportedReason =>
                    $"{_userConfig.GetText("criterion_ipv6_not_supported")}: {criterion.Name}",
                RuleTrivialityEvaluator.AddressNotAssessableReason =>
                    $"{_userConfig.GetText("assess_ip_null")}: {criterion.Name}",
                _ => reason
            };
        }

        /// <summary>
        /// Creates a violation entry from a compliance check result and stores it in the current run buffer.
        /// </summary>
        /// <param name="violationType">Type of violation to record.</param>
        /// <param name="rule">Impacted rule.</param>
        /// <param name="complianceCheckResult">Details assembled during the check.</param>
        /// <param name="detailsOverride">Optional string used if details need to be customized.</param>
        /// <returns>The recorded violation, or null when the violation type is not recorded.</returns>
        private ComplianceViolation? CreateViolation(ComplianceViolationType violationType, Rule rule, ComplianceCheckResult complianceCheckResult, string? detailsOverride = null)
        {
            if (!IsRecordedViolationType(violationType))
            {
                return null;
            }

            ComplianceViolation violation = new()
            {
                RuleId = (int)rule.Id,
                RuleUid = rule.Uid ?? "",
                MgmtUid = Managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Uid ?? "",
                PolicyId = Policy?.Id ?? 0,
                CriterionId = complianceCheckResult.Criterion!.Id,
                Details = string.IsNullOrEmpty(detailsOverride)
                    ? GetViolationDetails(violationType, complianceCheckResult)
                    : detailsOverride
            };

            _currentViolations.Add(violation);
            return violation;
        }

        /// <summary>
        /// Reports whether violations of the given type are recorded at all.
        /// </summary>
        /// <param name="violationType">Type of violation to record.</param>
        private static bool IsRecordedViolationType(ComplianceViolationType violationType)
        {
            return violationType is ComplianceViolationType.MatrixViolation
                or ComplianceViolationType.ServiceViolation
                or ComplianceViolationType.NotAssessable
                or ComplianceViolationType.MinimumCIDRLengthViolation
                or ComplianceViolationType.ZoneObjectSourceViolation
                or ComplianceViolationType.ZoneObjectDestinationViolation
                or ComplianceViolationType.BidirectionalDuplicateViolation;
        }

        /// <summary>
        /// Builds the localized details of a violation that comes without customized details.
        /// </summary>
        /// <param name="violationType">Type of violation to record.</param>
        /// <param name="complianceCheckResult">Details assembled during the check.</param>
        private string GetViolationDetails(ComplianceViolationType violationType, ComplianceCheckResult complianceCheckResult)
        {
            return violationType switch
            {
                ComplianceViolationType.MatrixViolation => GetMatrixViolationDetails(complianceCheckResult),
                ComplianceViolationType.ServiceViolation => GetServiceViolationDetails(complianceCheckResult),
                ComplianceViolationType.NotAssessable => GetAssessabilityViolationDetails(complianceCheckResult),
                _ => complianceCheckResult.Criterion?.Name ?? ""
            };
        }

        /// <summary>
        /// Builds the details of a matrix violation from the zones of the impacted objects.
        /// </summary>
        /// <param name="complianceCheckResult">Details assembled during the check.</param>
        private string GetMatrixViolationDetails(ComplianceCheckResult complianceCheckResult)
        {
            if (complianceCheckResult.Source is not NetworkObject source || complianceCheckResult.Destination is not NetworkObject destination)
            {
                return "";
            }

            string sourceString = GetNwObjectString(source);
            string destinationString = GetNwObjectString(destination);

            return $"{_userConfig.GetText("H5839")}: {sourceString} (Zone: {complianceCheckResult.SourceZone?.Name ?? ""}) -> {destinationString} (Zone: {complianceCheckResult.DestinationZone?.Name ?? ""})";
        }

        /// <summary>
        /// Builds the details of a forbidden service violation.
        /// </summary>
        /// <param name="complianceCheckResult">Details assembled during the check.</param>
        private string GetServiceViolationDetails(ComplianceCheckResult complianceCheckResult)
        {
            if (complianceCheckResult.Service is not NetworkService service)
            {
                throw new ArgumentNullException(paramName: "complianceCheckResult.Service", message: "The service argument must be non-null when creating a service violation.");
            }

            string serviceDisplay = DisplayBase.DisplayService(service, false).ToString();

            return $"{_userConfig.GetText("H5840")}: {serviceDisplay}";
        }

        /// <summary>
        /// Builds the details of a not-assessable violation from the reported assessability issue.
        /// </summary>
        /// <param name="complianceCheckResult">Details assembled during the check.</param>
        private string GetAssessabilityViolationDetails(ComplianceCheckResult complianceCheckResult)
        {
            if (complianceCheckResult.AssessabilityIssue == null)
            {
                return "";
            }

            string networkObject = "";

            if (complianceCheckResult.Source != null)
            {
                networkObject = GetNwObjectString(complianceCheckResult.Source);
            }
            else if (complianceCheckResult.Destination != null)
            {
                networkObject = GetNwObjectString(complianceCheckResult.Destination);
            }

            string assessabilityIssueType = complianceCheckResult.AssessabilityIssue.Value.ToAssessabilityIssueString();

            return $"{_userConfig.GetText("H5841")}: {_userConfig.GetText(assessabilityIssueType)}({networkObject})";
        }
    }
}

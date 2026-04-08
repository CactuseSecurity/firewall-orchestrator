using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterWorkflow : AstNodeFilter
    {
        private static readonly HashSet<string> AllowedPhases =
        [
            "closed",
            "request",
            "approval",
            "planning",
            "verification",
            "implementation",
            "review",
            "recertification"
        ];

        private List<WfTaskType> semanticTaskTypes = [];
        private List<int> semanticStates = [];
        private string semanticPhase = "";
        private WorkflowReferenceDate semanticReferenceDate = WorkflowReferenceDate.AnyActivity;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ);

            switch (Name.Kind)
            {
                case TokenKind.TaskType:
                    semanticTaskTypes = ParseTaskTypes(Value.Text, Value.Position);
                    break;
                case TokenKind.States:
                    semanticStates = ParseStates(Value.Text, Value.Position);
                    break;
                case TokenKind.Phase:
                    semanticPhase = ParsePhase(Value.Text, Value.Position);
                    break;
                case TokenKind.ReferenceDate:
                    semanticReferenceDate = ParseReferenceDate(Value.Text, Value.Position);
                    break;
                default:
                    throw new SemanticException($"Unexpected workflow filter token: {Name.Kind}", Name.Position);
            }
        }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            ConvertToSemanticType();

            switch (Name.Kind)
            {
                case TokenKind.TaskType:
                    query.WorkflowTaskTypes = [.. semanticTaskTypes];
                    break;
                case TokenKind.States:
                    query.WorkflowStateIds = [.. semanticStates];
                    break;
                case TokenKind.Phase:
                    query.WorkflowPhase = semanticPhase;
                    break;
                case TokenKind.ReferenceDate:
                    query.WorkflowReferenceDateFilter = semanticReferenceDate;
                    break;
            }
        }

        private static List<WfTaskType> ParseTaskTypes(string valueText, Range position)
        {
            List<WfTaskType> taskTypes = [];
            foreach (string rawTaskType in SplitCsv(valueText))
            {
                if (!Enum.TryParse(rawTaskType, true, out WfTaskType taskType) || taskType == WfTaskType.master)
                {
                    throw new SemanticException($"Unexpected workflow task type found: {rawTaskType}", position);
                }
                if (!taskTypes.Contains(taskType))
                {
                    taskTypes.Add(taskType);
                }
            }
            return taskTypes;
        }

        private static List<int> ParseStates(string valueText, Range position)
        {
            List<int> stateIds = [];
            foreach (string rawStateId in SplitCsv(valueText))
            {
                if (!int.TryParse(rawStateId, out int stateId))
                {
                    throw new SemanticException($"Unexpected workflow state found: {rawStateId}", position);
                }
                if (!stateIds.Contains(stateId))
                {
                    stateIds.Add(stateId);
                }
            }
            return stateIds;
        }

        private static string ParsePhase(string valueText, Range position)
        {
            string normalizedPhase = valueText.Trim().ToLowerInvariant();
            if (!AllowedPhases.Contains(normalizedPhase))
            {
                throw new SemanticException($"Unexpected workflow phase found: {valueText}", position);
            }
            return normalizedPhase;
        }

        private static WorkflowReferenceDate ParseReferenceDate(string valueText, Range position)
        {
            if (!Enum.TryParse(valueText.Trim(), true, out WorkflowReferenceDate referenceDate))
            {
                throw new SemanticException($"Unexpected workflow reference date found: {valueText}", position);
            }
            return referenceDate;
        }

        private static IEnumerable<string> SplitCsv(string valueText)
        {
            return valueText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}

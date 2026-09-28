using FWO.Data.Workflow;
using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

internal static class WorkflowTicketElementValidation
{
    public static void AppendReferencedElements(List<WfReqElement> elements, IEnumerable<long> references,
        Dictionary<long, WorkflowTicketEntity> entities, ElemFieldType field, WorkflowTicketEntityKind expectedKind,
        string path, HashSet<long> invalidEntityIds, List<RequestValidationError> validationErrors,
        Func<long, WfReqElement> buildElement)
    {
        List<long> referenceList = references.ToList();
        for (int index = 0; index < referenceList.Count; index++)
        {
            long reference = referenceList[index];
            if (invalidEntityIds.Contains(reference))
            {
                continue;
            }

            try
            {
                elements.Add(buildElement(reference));
            }
            catch (ArgumentException exception)
            {
                AddValidationError(validationErrors, $"{path}[{index}]", exception);
            }
        }
    }

    public static List<WfReqElement> BuildGroupMemberElements(IEnumerable<long> memberIds,
        Dictionary<long, WorkflowTicketEntity> entities, ElemFieldType field, string path,
        HashSet<long> invalidEntityIds, List<RequestValidationError> validationErrors,
        Func<long, WfReqElement> buildElement)
    {
        List<WfReqElement> elements = [];
        List<long> ids = memberIds.ToList();
        for (int index = 0; index < ids.Count; index++)
        {
            long memberId = ids[index];
            if (invalidEntityIds.Contains(memberId))
            {
                continue;
            }

            try
            {
                elements.Add(buildElement(memberId));
            }
            catch (ArgumentException exception)
            {
                AddValidationError(validationErrors, $"{path}[{index}]", exception);
            }
        }

        return elements;
    }

    private static void AddValidationError(List<RequestValidationError> validationErrors, string path,
        ArgumentException exception)
    {
        validationErrors.Add(new RequestValidationError { Path = path, Message = exception.Message });
    }
}

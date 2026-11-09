using FWO.Data.Workflow;
using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

internal static class WorkflowTicketElementValidation
{
    public static void AppendReferencedElements(List<WfReqElement> elements, IEnumerable<long> references,
        WorkflowTicketElementContext context)
    {
        List<long> referenceList = references.ToList();
        for (int index = 0; index < referenceList.Count; index++)
        {
            long reference = referenceList[index];
            if (context.InvalidEntityIds.Contains(reference))
            {
                continue;
            }

            try
            {
                elements.Add(context.BuildElement(reference));
            }
            catch (ArgumentException exception)
            {
                AddValidationError(context.ValidationErrors, $"{context.Path}[{index}]", exception);
            }
        }
    }

    public static List<WfReqElement> BuildGroupMemberElements(IEnumerable<long> memberIds,
        WorkflowTicketElementContext context)
    {
        List<WfReqElement> elements = [];
        List<long> ids = memberIds.ToList();
        for (int index = 0; index < ids.Count; index++)
        {
            long memberId = ids[index];
            if (context.InvalidEntityIds.Contains(memberId))
            {
                continue;
            }

            try
            {
                elements.Add(context.BuildElement(memberId));
            }
            catch (ArgumentException exception)
            {
                AddValidationError(context.ValidationErrors, $"{context.Path}[{index}]", exception);
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

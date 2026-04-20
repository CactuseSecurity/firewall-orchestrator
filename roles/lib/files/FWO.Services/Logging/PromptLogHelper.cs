using System.Globalization;
using FWO.Data;
using FWO.Data.Logging;
using FWO.Logging;

namespace FWO.Services.Logging
{
    public static class PromptLogHelper
    {
        public static Task LogPrompt(PromptLogRequest request)
        {
            string title = ComposePromptLogTitle(request.PromptEvent, request.Object, request.Operation);
            string text =
                $"User ID: {request.UserId}; Date/Time: {request.Timestamp.ToString(CultureInfo.InvariantCulture)}; Origin: {ChangeLogHelper.GetOriginName(request.Origin)}; " +
                ChangeLogHelper.FormatFields(request.Fields);
            Log.WriteInfo(title, text);
            return Task.CompletedTask;
        }

        public static Task LogManagementPrompt(ManagementPromptLogRequest request)
        {
            return LogPrompt(request.ToPromptLogRequest());
        }

        public static Task LogGatewayPrompt(GatewayPromptLogRequest request)
        {
            return LogPrompt(request.ToPromptLogRequest());
        }

        private static string GetPromptEventName(PromptLogEvent logEvent)
        {
            return logEvent switch
            {
                PromptLogEvent.Completed => "Completed",
                PromptLogEvent.Created => "Created",
                PromptLogEvent.Dismissed => "Dismissed",
                _ => logEvent.ToString()
            };
        }

        private static string ComposePromptLogTitle(PromptLogEvent promptEvent, ChangeLogObject obj,
            ChangeLogOperation operation)
        {
            return $"{ChangeLogHelper.GetObjectName(obj)} {ChangeLogHelper.GetOperationName(operation)} Prompt {GetPromptEventName(promptEvent)}";
        }
    }
}

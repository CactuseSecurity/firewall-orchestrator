using System.Globalization;
using FWO.Data;
using FWO.Logging;

namespace FWO.Services
{
    public static class PromptLogHelper
    {
        public static Task LogPrompt(PromptLogEvent promptEvent, ChangeLogObject obj, ChangeLogOperation operation,
            string userId, DateTime dateTime, ChangeLogOrigin origin)
        {
            string title = ComposePromptLogTitle(promptEvent, obj, operation);
            string text =
                $"User ID: {userId}; Date/Time: {dateTime.ToString(CultureInfo.InvariantCulture)}; Origin: {ChangeLogHelper.GetOriginName(origin)}; ";
            Log.WriteInfo(title, text);
            return Task.CompletedTask;
        }

        public static Task LogManagementPrompt(PromptLogEvent promptEvent, ChangeLogOperation operation,
            ChangeLogOrigin origin, string userId)
        {
            return LogPrompt(promptEvent, ChangeLogObject.Management, operation, userId, DateTime.Now, origin);
        }

        public static Task LogGatewayPrompt(PromptLogEvent promptEvent, ChangeLogOperation operation,
            ChangeLogOrigin origin, string userId)
        {
            return LogPrompt(promptEvent, ChangeLogObject.Gateway, operation, userId, DateTime.Now, origin);
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

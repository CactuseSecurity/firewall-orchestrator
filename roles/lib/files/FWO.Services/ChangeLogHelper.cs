using System.Text;
using FWO.Logging;

namespace FWO.Services
{
    public static class ChangeLogHelper
    {
        public static Task LogMatrixChange(
            string action,
            int? matrixId = null,
            string? matrixName = null,
            int? userId = null,
            string? origin = null)
        {
            WriteLog(action, BuildText(
                ("matrix_id", matrixId),
                ("matrix_name", matrixName),
                ("user_id", userId),
                ("origin", origin)));
            return Task.CompletedTask;
        }

        public static Task LogManagerChange(
            string action,
            int? managementId = null,
            string? managementName = null,
            int? userId = null,
            string? origin = null)
        {
            WriteLog(action, BuildText(
                ("management_id", managementId),
                ("management_name", managementName),
                ("user_id", userId),
                ("origin", origin)));
            return Task.CompletedTask;
        }

        public static Task LogGatewayChange(
            string action,
            int? deviceId = null,
            string? deviceName = null,
            int? managementId = null,
            int? userId = null,
            string? origin = null)
        {
            WriteLog(action, BuildText(
                ("device_id", deviceId),
                ("device_name", deviceName),
                ("management_id", managementId),
                ("user_id", userId),
                ("origin", origin)));
            return Task.CompletedTask;
        }

        private static void WriteLog(string action, string text)
        {
            string title = DescribeAction(action);
            if (action.StartsWith("manual_")
                || action.StartsWith("prompted_")
                || action.StartsWith("prompt_dismissed_"))
            {
                Log.WriteAudit(title, text);
                return;
            }

            if (action.StartsWith("autodiscovery_prompt_")
                || action.StartsWith("middleware_matrix_import_"))
            {
                Log.WriteInfo(title, text);
                return;
            }

            Log.WriteWarning(title, $"Unmapped change-log action family. {text}");
        }

        private static string DescribeAction(string action)
        {
            return action switch
            {
                "manual_matrix_create" => "Matrix Created Manually",
                "manual_matrix_soft_delete" => "Matrix Soft Deleted Manually",
                "manual_management_create" => "Management Created Manually",
                "manual_management_update" => "Management Updated Manually",
                "manual_management_delete" => "Management Deleted Manually",
                "autodiscovery_prompt_management_create" => "Autodiscovery Prompt Created For Management Creation",
                "autodiscovery_prompt_management_delete" => "Autodiscovery Prompt Created For Management Deletion",
                "autodiscovery_prompt_management_reactivate" => "Autodiscovery Prompt Created For Management Reactivation",
                "autodiscovery_prompt_gateway_create" => "Autodiscovery Prompt Created For Gateway Creation",
                "autodiscovery_prompt_gateway_delete" => "Autodiscovery Prompt Created For Gateway Deletion",
                "autodiscovery_prompt_gateway_reactivate" => "Autodiscovery Prompt Created For Gateway Reactivation",
                "prompted_management_create" => "Management Created After Autodiscovery Prompt",
                "prompted_management_delete" => "Management Deleted After Autodiscovery Prompt",
                "prompted_management_disable" => "Management Disabled After Autodiscovery Prompt",
                "prompted_management_reactivate" => "Management Reactivated After Autodiscovery Prompt",
                "prompted_gateway_create" => "Gateway Created After Autodiscovery Prompt",
                "prompted_gateway_delete" => "Gateway Deleted After Autodiscovery Prompt",
                "prompted_gateway_disable" => "Gateway Disabled After Autodiscovery Prompt",
                "prompted_gateway_reactivate" => "Gateway Reactivated After Autodiscovery Prompt",
                "prompt_dismissed_management_create" => "Management Creation Prompt Dismissed",
                "prompt_dismissed_management_delete" => "Management Deletion Prompt Dismissed",
                "prompt_dismissed_management_reactivate" => "Management Reactivation Prompt Dismissed",
                "prompt_dismissed_gateway_create" => "Gateway Creation Prompt Dismissed",
                "prompt_dismissed_gateway_delete" => "Gateway Deletion Prompt Dismissed",
                "prompt_dismissed_gateway_reactivate" => "Gateway Reactivation Prompt Dismissed",
                "middleware_matrix_import_create" => "Matrix Created During Middleware Import",
                _ => action.Replace('_', ' ')
            };
        }

        private static string BuildText(params (string Key, object? Value)[] fields)
        {
            StringBuilder sb = new();

            foreach ((string key, object? value) in fields)
            {
                if (value == null)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append("; ");
                }

                sb.Append(GetLabel(key)).Append(": ").Append(value);
            }

            return sb.Length > 0 ? sb.ToString() : "No additional data.";
        }

        private static string GetLabel(string key)
        {
            return key switch
            {
                "matrix_id" => "Matrix ID",
                "matrix_name" => "Matrix Name",
                "management_id" => "Management ID",
                "management_name" => "Management Name",
                "device_id" => "Gateway ID",
                "device_name" => "Gateway Name",
                "user_id" => "User ID",
                "origin" => "Origin",
                _ => key.Replace('_', ' ')
            };
        }
    }
}

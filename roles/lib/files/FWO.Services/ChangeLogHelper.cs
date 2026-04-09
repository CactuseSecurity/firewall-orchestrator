using System.Text;
using FWO.Data;
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
            if (action.StartsWith(ChangeLogActions.ManualFamily)
                || action.StartsWith(ChangeLogActions.PromptedFamily)
                || action.StartsWith(ChangeLogActions.PromptDismissedFamily))
            {
                Log.WriteAudit(title, text);
                return;
            }

            if (action.StartsWith(ChangeLogActions.AutodiscoveryPromptFamily)
                || action.StartsWith(ChangeLogActions.MiddlewareMatrixImportFamily))
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
                ChangeLogActions.ManualMatrixCreate => "Matrix Created Manually",
                ChangeLogActions.ManualMatrixSoftDelete => "Matrix Soft Deleted Manually",
                ChangeLogActions.ManualManagementCreate => "Management Created Manually",
                ChangeLogActions.ManualManagementUpdate => "Management Updated Manually",
                ChangeLogActions.ManualManagementDelete => "Management Deleted Manually",
                ChangeLogActions.AutodiscoveryPromptManagementCreate => "Autodiscovery Prompt Created For Management Creation",
                ChangeLogActions.AutodiscoveryPromptManagementDelete => "Autodiscovery Prompt Created For Management Deletion",
                ChangeLogActions.AutodiscoveryPromptManagementReactivate => "Autodiscovery Prompt Created For Management Reactivation",
                ChangeLogActions.AutodiscoveryPromptGatewayCreate => "Autodiscovery Prompt Created For Gateway Creation",
                ChangeLogActions.AutodiscoveryPromptGatewayDelete => "Autodiscovery Prompt Created For Gateway Deletion",
                ChangeLogActions.AutodiscoveryPromptGatewayReactivate => "Autodiscovery Prompt Created For Gateway Reactivation",
                ChangeLogActions.PromptedManagementCreate => "Management Created After Autodiscovery Prompt",
                ChangeLogActions.PromptedManagementDelete => "Management Deleted After Autodiscovery Prompt",
                ChangeLogActions.PromptedManagementDisable => "Management Disabled After Autodiscovery Prompt",
                ChangeLogActions.PromptedManagementReactivate => "Management Reactivated After Autodiscovery Prompt",
                ChangeLogActions.PromptedGatewayCreate => "Gateway Created After Autodiscovery Prompt",
                ChangeLogActions.PromptedGatewayDelete => "Gateway Deleted After Autodiscovery Prompt",
                ChangeLogActions.PromptedGatewayDisable => "Gateway Disabled After Autodiscovery Prompt",
                ChangeLogActions.PromptedGatewayReactivate => "Gateway Reactivated After Autodiscovery Prompt",
                ChangeLogActions.PromptDismissedManagementCreate => "Management Creation Prompt Dismissed",
                ChangeLogActions.PromptDismissedManagementDelete => "Management Deletion Prompt Dismissed",
                ChangeLogActions.PromptDismissedManagementReactivate => "Management Reactivation Prompt Dismissed",
                ChangeLogActions.PromptDismissedGatewayCreate => "Gateway Creation Prompt Dismissed",
                ChangeLogActions.PromptDismissedGatewayDelete => "Gateway Deletion Prompt Dismissed",
                ChangeLogActions.PromptDismissedGatewayReactivate => "Gateway Reactivation Prompt Dismissed",
                ChangeLogActions.MiddlewareMatrixImportCreate => "Matrix Created During Middleware Import",
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

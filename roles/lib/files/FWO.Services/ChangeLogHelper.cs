using System.Text;
using FWO.Logging;

namespace FWO.Services
{
    public static class ChangeLogHelper
    {
        private const string MatrixTitle = "Matrix Change";
        private const string ManagementTitle = "Management Change";
        private const string GatewayTitle = "Gateway Change";

        public static Task LogMatrixChange(
            string action,
            int? matrixId = null,
            string? matrixName = null,
            int? userId = null,
            string? origin = null)
        {
            WriteLog(MatrixTitle, action, BuildText(
                action,
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
            WriteLog(ManagementTitle, action, BuildText(
                action,
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
            WriteLog(GatewayTitle, action, BuildText(
                action,
                ("device_id", deviceId),
                ("device_name", deviceName),
                ("management_id", managementId),
                ("user_id", userId),
                ("origin", origin)));
            return Task.CompletedTask;
        }

        private static void WriteLog(string title, string action, string text)
        {
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

        private static string BuildText(string action, params (string Key, object? Value)[] fields)
        {
            StringBuilder sb = new();
            sb.Append("action=").Append(action);

            foreach ((string key, object? value) in fields)
            {
                if (value == null)
                {
                    continue;
                }
                sb.Append(", ").Append(key).Append('=').Append(value);
            }

            return sb.ToString();
        }
    }
}

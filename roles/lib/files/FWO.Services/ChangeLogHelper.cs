using System.Globalization;
using System.Text;
using FWO.Data;
using FWO.Logging;

namespace FWO.Services
{
    public static class ChangeLogHelper
    {
        public static Task LogChange(ChangeLogFamily family, ChangeLogObject obj, ChangeLogOperation operation,
            string userId, DateTime dateTime, ChangeLogOrigin origin, params (string Key, object? Value)[] fields)
        {
            string title = ComposeChangeLogTitle(family, obj, operation);
            string text =
                $"User ID: {userId}; Date/Time: {dateTime.ToString(CultureInfo.InvariantCulture)}; Origin: {GetOriginName(origin)}; " +
                FormatFields(fields);

            switch (family)
            {
                case ChangeLogFamily.Manual:
                    Log.WriteAudit(title, text);
                    break;
                case ChangeLogFamily.Import:
                    Log.WriteInfo(title, text);
                    break;
                default:
                    Log.WriteWarning(title, $"Unmapped change-log family. {text}");
                    break;
            }

            return Task.CompletedTask;
        }

        public static Task LogMatrixChange(ChangeLogFamily family, ChangeLogOperation operation, ChangeLogOrigin origin,
            string userId, int? matrixId = null, string? matrixName = null
        )
        {
            return LogChange(
                family,
                ChangeLogObject.Matrix,
                operation,
                userId,
                DateTime.Now,
                origin,
                ("Matrix ID", matrixId),
                ("Matrix Name", matrixName));
        }

        public static Task LogManagerChange(ChangeLogFamily family, ChangeLogOperation operation,
            ChangeLogOrigin origin, string userId, int? managementId = null, string? managementName = null)
        {
            return LogChange(
                family,
                ChangeLogObject.Management,
                operation,
                userId,
                DateTime.Now,
                origin,
                ("Management ID", managementId),
                ("Management Name", managementName));
        }

        public static Task LogGatewayChange(ChangeLogFamily family, ChangeLogOperation operation,
            ChangeLogOrigin origin, string userId, int? deviceId = null, string? deviceName = null,
            int? managementId = null)
        {
            return LogChange(
                family,
                ChangeLogObject.Gateway,
                operation,
                userId,
                DateTime.Now,
                origin,
                ("Device ID", deviceId),
                ("Device Name", deviceName),
                ("Management ID", managementId));
        }

        private static string ComposeChangeLogTitle(ChangeLogFamily family, ChangeLogObject obj, ChangeLogOperation operation)
        {
            return $"{GetFamilyName(family)} {GetObjectName(obj)} {GetOperationName(operation)}";
        }

        public static string GetOriginName(ChangeLogOrigin origin)
        {
            return origin switch
            {
                ChangeLogOrigin.UiSettings => "UI",
                ChangeLogOrigin.Autodiscovery => "Autodiscovery",
                ChangeLogOrigin.Import => "Import",
                _ => origin.ToString()
            };
        }

        private static string GetFamilyName(ChangeLogFamily family)
        {
            return family switch
            {
                ChangeLogFamily.Manual => "Manual",
                ChangeLogFamily.Import => "Import",
                _ => family.ToString()
            };
        }
        
        public static string GetObjectName(ChangeLogObject obj)
        {
            return obj switch
            {
                ChangeLogObject.Matrix => "Matrix",
                ChangeLogObject.Management => "Management",
                ChangeLogObject.Gateway => "Gateway",
                _ => obj.ToString()
            };
        }

        public static string GetOperationName(ChangeLogOperation operation)
        {
            return operation switch
            {
                ChangeLogOperation.Create => "Create",
                ChangeLogOperation.Update => "Update",
                ChangeLogOperation.Delete => "Delete",
                ChangeLogOperation.SetRemoved => "Set to removed",
                ChangeLogOperation.Disable => "Disable",
                ChangeLogOperation.Activate => "Activate",
                _ => operation.ToString()
            };
        }

        private static string FormatFields(params (string Key, object? Value)[] fields)
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

                sb.Append(key).Append(": ").Append(value);
            }

            return sb.Length > 0 ? sb.ToString() : string.Empty;
        }
    }
}

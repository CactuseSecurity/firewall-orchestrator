using System.Globalization;
using System.Text;
using FWO.Data;
using FWO.Data.Logging;
using FWO.Logging;

namespace FWO.Services.Logging
{
    public static class ChangeLogHelper
    {
        public static Task LogChange(ChangeLogRequest request)
        {
            string title = ComposeChangeLogTitle(request.Family, request.Object, request.Operation);
            string text =
                $"User ID: {request.UserId}; Date/Time: {request.Timestamp.ToString(CultureInfo.InvariantCulture)}; Origin: {GetOriginName(request.Origin)}; " +
                FormatFields(request.Fields);

            switch (request.Family)
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

        public static Task LogMatrixChange(MatrixChangeLogRequest request)
        {
            return LogChange(request.ToChangeLogRequest());
        }

        public static Task LogManagerChange(ManagementChangeLogRequest request)
        {
            return LogChange(request.ToChangeLogRequest());
        }

        public static Task LogGatewayChange(GatewayChangeLogRequest request)
        {
            return LogChange(request.ToChangeLogRequest());
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

        public static string FormatFields(params (string Key, object? Value)[] fields)
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

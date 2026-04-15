using System.Text.Json;
using FWO.Data;

namespace FWO.Services.Logging;

public static class AutodiscoveryLogMapper
{
    public sealed class PromptLogData
    {
        public required ChangeLogObject Object { get; init; }
        public required ChangeLogOperation Operation { get; init; }
        public (string Key, object? Value)[] Fields { get; init; } = [];
    }

    public static bool TryMapPromptAction(ActionItem action, out PromptLogData? logData)
    {
        logData = action.ActionType switch
        {
            nameof(ActionCode.AddManagement) => CreateManagementAddLogData(action),
            nameof(ActionCode.DeleteManagement) => new PromptLogData
            {
                Object = ChangeLogObject.Management,
                Operation = ChangeLogOperation.Delete,
                Fields =
                [
                    ("Management ID", action.ManagementId)
                ]
            },
            nameof(ActionCode.ReactivateManagement) => new PromptLogData
            {
                Object = ChangeLogObject.Management,
                Operation = ChangeLogOperation.Activate,
                Fields =
                [
                    ("Management ID", action.ManagementId)
                ]
            },
            nameof(ActionCode.DeleteGateway) => new PromptLogData
            {
                Object = ChangeLogObject.Gateway,
                Operation = ChangeLogOperation.Delete,
                Fields =
                [
                    ("Device ID", action.DeviceId),
                    ("Management ID", action.ManagementId)
                ]
            },
            nameof(ActionCode.AddGatewayToNewManagement) => CreateGatewayAddLogData(action, false),
            nameof(ActionCode.AddGatewayToExistingManagement) => CreateGatewayAddLogData(action, true),
            nameof(ActionCode.ReactivateGateway) => new PromptLogData
            {
                Object = ChangeLogObject.Gateway,
                Operation = ChangeLogOperation.Activate,
                Fields =
                [
                    ("Device ID", action.DeviceId),
                    ("Management ID", action.ManagementId)
                ]
            },
            _ => null
        };

        return logData != null;
    }

    private static PromptLogData CreateManagementAddLogData(ActionItem action)
    {
        Management? management = DeserializeJsonData<Management>(action.JsonData);
        return new PromptLogData
        {
            Object = ChangeLogObject.Management,
            Operation = ChangeLogOperation.Create,
            Fields =
            [
                ("Management ID", action.ManagementId),
                ("Management Name", management?.Name),
                ("Management Hostname", management?.Hostname)
            ]
        };
    }

    private static PromptLogData? CreateGatewayAddLogData(ActionItem action, bool includeExistingManagementId)
    {
        Device? device = DeserializeJsonData<Device>(action.JsonData);
        return new PromptLogData
        {
            Object = ChangeLogObject.Gateway,
            Operation = ChangeLogOperation.Create,
            Fields =
            [
                ("Device ID", action.DeviceId),
                ("Device Name", device?.Name),
                ("Management ID", includeExistingManagementId ? action.ManagementId : device?.Management?.Id),
                ("Management Name", device?.Management?.Name)
            ]
        };
    }

    private static T? DeserializeJsonData<T>(object? jsonData)
    {
        if (jsonData == null)
        {
            return default;
        }

        try
        {
            return jsonData switch
            {
                string json => JsonSerializer.Deserialize<T>(json),
                JsonElement element => element.Deserialize<T>(),
                _ => JsonSerializer.Deserialize<T>(jsonData.ToString() ?? string.Empty)
            };
        }
        catch
        {
            return default;
        }
    }
}

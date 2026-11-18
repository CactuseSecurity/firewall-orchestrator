using FWO.Data.DeviceSpecificSettings;
using FWO.Middleware.Server;
using FWO.Services.Workflow;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Test helpers for the external request handler.
    /// </summary>
    internal static class ExternalRequestHandlerTestExtensions
    {
        /// <summary>
        /// Lets the handler build its provisioning work lists with the given device specific settings instead of the
        /// stored ones.
        /// </summary>
        internal static ExternalRequestHandler WithProvisioningSettings(this ExternalRequestHandler handler, GlobalDeviceSpecificSettings settings)
        {
            FieldInfo field = typeof(ExternalRequestHandler).GetField("wfHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(ExternalRequestHandler), "wfHandler");
            ((WfHandler)field.GetValue(handler)!).AutoPlanSettingsOverride = settings;
            return handler;
        }
    }
}

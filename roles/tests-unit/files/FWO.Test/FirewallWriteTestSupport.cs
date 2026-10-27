using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using RestSharp;
using System.Net;

namespace FWO.Test
{
    /// <summary>A request captured by a simulated firewall write client.</summary>
    internal sealed record RecordedFirewallRequest(Method Method, string Resource, string? Body, Dictionary<string, string> Headers,
        Dictionary<string, string> FormParameters, Dictionary<string, string> QueryParameters);

    /// <summary>Simulates the firewall: records every request and answers with the configured handler.</summary>
    internal sealed class FirewallSimulation
    {
        public List<RecordedFirewallRequest> Requests { get; } = [];

        public Func<RecordedFirewallRequest, RestResponse> Handler { get; set; } = _ => FirewallWriteTestSupport.Ok("{}");

        public RestResponse Answer(RestRequest request)
        {
            RecordedFirewallRequest recorded = new(
                request.Method,
                request.Resource,
                request.Parameters.OfType<BodyParameter>().FirstOrDefault()?.Value as string,
                request.Parameters.Where(parameter => parameter.Type == ParameterType.HttpHeader)
                    .ToDictionary(parameter => parameter.Name!, parameter => parameter.Value?.ToString() ?? ""),
                request.Parameters.Where(parameter => parameter.Type == ParameterType.GetOrPost)
                    .ToDictionary(parameter => parameter.Name!, parameter => parameter.Value?.ToString() ?? ""),
                request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString)
                    .ToDictionary(parameter => parameter.Name!, parameter => parameter.Value?.ToString() ?? ""));
            Requests.Add(recorded);
            return Handler(recorded);
        }
    }

    internal sealed class SimulatedCheckPointWriteClient(Management management, FirewallSimulation simulation) : CheckPointWriteClient(management, true)
    {
        protected override Task<RestResponse> Send(RestRequest request) => Task.FromResult(simulation.Answer(request));
    }

    internal sealed class SimulatedFortiManagerWriteClient(Management management, FirewallSimulation simulation) : FortiManagerWriteClient(management, true)
    {
        protected override Task<RestResponse> Send(RestRequest request) => Task.FromResult(simulation.Answer(request));
    }

    internal sealed class SimulatedFortiOsWriteClient(Management management, FirewallSimulation simulation) : FortiOsWriteClient(management, true)
    {
        protected override Task<RestResponse> Send(RestRequest request) => Task.FromResult(simulation.Answer(request));
    }

    internal sealed class SimulatedOpnSenseWriteClient(Management management, FirewallSimulation simulation) : OpnSenseWriteClient(management, true)
    {
        protected override Task<RestResponse> Send(RestRequest request) => Task.FromResult(simulation.Answer(request));
    }

    internal sealed class SimulatedAzureWriteClient(Management management, FirewallSimulation simulation) : AzureWriteClient(management, true)
    {
        protected override Task<RestResponse> Send(RestRequest request) => Task.FromResult(simulation.Answer(request));
    }

    /// <summary>Creates simulated clients for every vendor, all answering through one simulation.</summary>
    internal sealed class SimulatedFirewallWriteClientFactory(FirewallSimulation simulation) : IFirewallWriteClientFactory
    {
        public int CreatedClients { get; private set; }

        public FirewallWriteClient Create(FirewallWriteContext context)
        {
            CreatedClients++;
            return context.Vendor switch
            {
                FirewallVendor.CheckPoint => new SimulatedCheckPointWriteClient(context.Management, simulation),
                FirewallVendor.FortiManager => new SimulatedFortiManagerWriteClient(context.Management, simulation),
                FirewallVendor.FortiOs => new SimulatedFortiOsWriteClient(context.Management, simulation),
                FirewallVendor.OpnSense => new SimulatedOpnSenseWriteClient(context.Management, simulation),
                FirewallVendor.Azure => new SimulatedAzureWriteClient(context.Management, simulation),
                _ => throw new NotSupportedException()
            };
        }
    }

    /// <summary>Returns a fixed context for every task.</summary>
    internal sealed class FixedFirewallWriteDataSource(FirewallWriteContext context) : IFirewallWriteDataSource
    {
        public Task<FirewallWriteContext> LoadContext(WfImplTask implTask) => Task.FromResult(context);
    }

    /// <summary>Builders for management, gateway, context, tasks and responses used by the firewall write tests.</summary>
    internal static class FirewallWriteTestSupport
    {
        public const int kGatewayId = 200;
        public const int kManagementId = 100;

        public static RestResponse Ok(string content) => Response(HttpStatusCode.OK, content);

        public static RestResponse Response(HttpStatusCode statusCode, string content) => new(new RestRequest())
        {
            StatusCode = statusCode,
            IsSuccessStatusCode = (int)statusCode >= 200 && (int)statusCode < 300,
            ResponseStatus = ResponseStatus.Completed,
            Content = content
        };

        public static Management Management(int deviceTypeId, string configPath = "")
        {
            Management management = new()
            {
                Id = kManagementId,
                Name = "mgmt",
                Hostname = "fw.example.test",
                Port = 443,
                ConfigPath = configPath,
                CloudSubscriptionId = "sub-1",
                CloudTenantId = "tenant-1",
                DeviceType = new DeviceType { Id = deviceTypeId, Name = "type" },
                ExportCredential = new Credential { Name = "export", ImportUser = "writer", Secret = "secret" }
            };
            Device gateway = new() { Id = kGatewayId, Name = "gw1_vd1", LocalRulebase = "Network", Package = "Standard", Management = management };
            management.Devices = [gateway];
            return management;
        }

        public static FirewallWriteContext Context(int deviceTypeId, GlobalDeviceSpecificSettings? settings = null, string configPath = "")
        {
            Management management = Management(deviceTypeId, configPath);
            return new FirewallWriteContext { Management = management, Gateway = management.Devices[0], Settings = settings ?? new GatewayDeviceSpecificSettings() };
        }

        public static WfImplTask Task(WfTaskType taskType, params WfImplElement[] elements) => new()
        {
            Id = 7,
            TicketId = 3,
            TaskNumber = 1,
            Title = "rule-1",
            FreeText = "comment",
            TaskType = taskType.ToString(),
            ManagementId = kManagementId,
            DeviceId = kGatewayId,
            ImplElements = [.. elements]
        };

        public static WfImplElement Network(string? name, string? ip, string? ipEnd = null, ElemFieldType field = ElemFieldType.source,
            RequestAction action = RequestAction.create) => new()
            {
                Name = name,
                IpString = ip,
                IpEnd = ipEnd,
                Field = field.ToString(),
                ImplAction = action.ToString()
            };

        public static WfImplElement Service(string? name, int protoId, int port, int? portEnd = null, RequestAction action = RequestAction.create) => new()
        {
            Name = name,
            ProtoId = protoId,
            Port = port,
            PortEnd = portEnd,
            Field = ElemFieldType.service.ToString(),
            ImplAction = action.ToString()
        };

        public static WfImplElement Rule(string ruleUid) => new()
        {
            RuleUid = ruleUid,
            Field = ElemFieldType.rule.ToString(),
            ImplAction = RequestAction.delete.ToString()
        };
    }
}

using FWO.Data;
using FWO.ExternalSystems.CheckPoint;
using FWO.Services;
using RestSharp;
using System.Net;

namespace FWO.Test
{
    public class SimulatedCheckPointClient : CheckPointClient
    {
        public List<string> CalledEndpoints { get; } = [];
        public List<string> RequestBodies { get; } = [];
        public int LogoutCalls { get; private set; }
        private readonly Dictionary<string, Queue<RestResponse<int>>> queuedResponses = new();

        public SimulatedCheckPointClient(ExternalTicketSystem checkPointSystem) : base(checkPointSystem)
        { }

        public void EnqueueResponse(string restEndPoint, RestResponse<int> response)
        {
            if (!queuedResponses.TryGetValue(restEndPoint, out Queue<RestResponse<int>>? responses))
            {
                responses = new Queue<RestResponse<int>>();
                queuedResponses[restEndPoint] = responses;
            }
            responses.Enqueue(response);
        }

        public override async Task<RestResponse<int>> RestCall(RestRequest request, string restEndPoint)
        {
            CalledEndpoints.Add(restEndPoint);
            RequestBodies.Add(request.Parameters.FirstOrDefault(parameter => parameter.Name == "")?.Value?.ToString() ?? "");
            await DefaultInit.DoNothing();
            if (queuedResponses.TryGetValue(restEndPoint, out Queue<RestResponse<int>>? responses) && responses.Count > 0)
            {
                return responses.Dequeue();
            }
            return new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"task-id\":\"cp-task-1\"}" };
        }

        public override async Task Logout()
        {
            LogoutCalls++;
            await DefaultInit.DoNothing();
        }
    }
}

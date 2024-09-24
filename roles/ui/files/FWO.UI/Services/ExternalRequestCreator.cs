using FWO.Logging;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Client;
using FWO.Tufin.SecureChange;
using System.Text.Json.Serialization;
using Newtonsoft.Json;


namespace FWO.Ui.Services
{
	public class ExternalRequestCreator
	{
		private readonly FwoOwner Owner;
		private readonly WfTicket InternalTicket;
		private readonly ApiConnection ApiConnection;
		private readonly ExtStateHandler extStateHandler;
		private readonly WfHandler wfHandler;
		private readonly UserConfig UserConfig;
		private readonly System.Security.Claims.ClaimsPrincipal AuthUser;
		private readonly Dictionary<long, GraphQlApiSubscription<List<ExternalRequest>>> extTicketSubscriptions = [];
		private ExternalTicketSystemType extSystemType = ExternalTicketSystemType.Generic;

		public ExternalRequestCreator(FwoOwner owner, WfTicket ticket, UserConfig userConfig, System.Security.Claims.ClaimsPrincipal authUser, ApiConnection apiConnection, MiddlewareClient middlewareClient)
		{
			Owner = owner;
			InternalTicket = ticket;
			ApiConnection = apiConnection;
			UserConfig = userConfig;
			AuthUser = authUser;
			extStateHandler = new(apiConnection);
			wfHandler = new (LogMessage, userConfig, authUser, apiConnection, middlewareClient, WorkflowPhases.request);
		}

		public async Task Run()
		{
			try
			{
            	await extStateHandler.Init();
				if(UserConfig.ModRolloutBundleTasks)
				{
					// todo: bundle
				}
				else
				{
					WfReqTask firstTask = InternalTicket.Tasks.FirstOrDefault(ta => ta.TaskNumber == 0) ?? throw new Exception("No task found.");
					await CreateExtRequest([firstTask]);
				}
			}
			catch(Exception exception)
			{
				Log.WriteError("External Request Creation", $"Runs into exception: ", exception);
			}
		}

		public async Task CreateExtRequest(List<WfReqTask> tasks)
		{
			var Variables = new
			{
				ownerId = Owner.Id,
  				ticketId = InternalTicket.Id,
				taskNumber = tasks.First()?.TaskNumber ?? 0,
				extTicketSystem = GetExtSystemFromConfig(),
				extTaskType = GetTaskType(tasks),
				extTaskContent = ConstructContent(tasks),
				extQueryVariables = "", // todo ??
				extRequestState = ExtStates.ExtReqInitialized.ToString()
			};
			ReturnId[]? reqIds = (await ApiConnection.SendQueryAsync<NewReturning>(ExtRequestQueries.addExtRequest, Variables)).ReturnIds;
			if(reqIds != null)
			{
				extTicketSubscriptions.Add(reqIds[0].NewId, ApiConnection.GetSubscription<List<ExternalRequest>>(HandleSubscriptionError, OnStateUpdate, ExtRequestQueries.subscribeExtRequestStateUpdate, new{ id = reqIds[0].NewId }));
			}
		}

		public void OnStateUpdate(List<ExternalRequest> extRequests)
		{
			if(extRequests.Count > 0)
			{
				Task.Run(async () =>
				{
					try
					{
						await extStateHandler.Init();
						await wfHandler.Init([extRequests.First().OwnerId]);
            			ApiConnection.SetProperRole(AuthUser, [ Roles.Requester, Roles.Admin ]);
            			WfTicket? intTicket = await wfHandler.ResolveTicket(extRequests.First().TicketId);
						if(intTicket != null)
						{
							wfHandler.SetTicketEnv(intTicket);
							await UpdateTicket(intTicket, extRequests.First());
            				ApiConnection.SetProperRole(AuthUser, [ Roles.Modeller, Roles.Admin, Roles.Auditor ]);
							if(extStateHandler.GetInternalStateId(extRequests.First().ExtRequestState) >= wfHandler.ActStateMatrix.LowestEndState)
							{
								Dispose(extRequests.First().Id);
								await Acknowledge(extRequests.First());
								await SendNextRequest(intTicket, extRequests.First().TaskNumber);
								// push (some) state changes to modelling pages?
							}
						}
						else
						{
            				ApiConnection.SetProperRole(AuthUser, [ Roles.Modeller, Roles.Admin, Roles.Auditor ]);
						}
					}
					catch(Exception exception)
					{
						Log.WriteError("External Request Update", $"Runs into exception: ", exception);
					}
				});
			}
		}

		private string GetExtSystemFromConfig()
		{
			List<ExternalTicketSystem> extTicketSystems = System.Text.Json.JsonSerializer.Deserialize<List<ExternalTicketSystem>>(UserConfig.ExtTicketSystems) ?? [];
			if(extTicketSystems.Count > 0)
			{
				extSystemType = extTicketSystems.First().Type;
				return System.Text.Json.JsonSerializer.Serialize(extTicketSystems.First());
			}
			throw new Exception("No external ticket system defined.");
		}

		private string GetTaskType(List<WfReqTask> tasks)
		{
			if(extSystemType == ExternalTicketSystemType.TufinSecureChange)
			{
				switch(tasks.First().TaskType)
				{
					case nameof(WfTaskType.access):
						return ExternalTaskType.AccessRequest.ToString();
					case nameof(WfTaskType.group_create):
						if(IsNetworkFlavor(tasks.First()))
						{
							return ExternalTaskType.NetworkObjectCreate.ToString();
						}
						else
						{
							return ExternalTaskType.NetworkServiceCreate.ToString();
						}
					default: return "";
				}
			}
			return "";
		}

		private static bool IsNetworkFlavor(WfReqTask task)
		{
			return task.Elements.FirstOrDefault(e => e.IpString != null) != null;
		}

		private string ConstructContent(List<WfReqTask> tasks)
		{
			if(extSystemType == ExternalTicketSystemType.TufinSecureChange)
			{
				SCTicket ticket = new(tasks, "test ticket 1", ExternalTicketPriority.High); // todo
				return System.Text.Json.JsonSerializer.Serialize(ticket);
			}
			return "";
		}

		private async Task UpdateTicket(WfTicket ticket, ExternalRequest extReq)
		{
			WfReqTask? updatedTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == extReq.TaskNumber);
			if(updatedTask != null)
			{
				if(updatedTask.StateId != extStateHandler.GetInternalStateId(extReq.ExtRequestState))
				{
					wfHandler.SetReqTaskEnv(updatedTask);
					updatedTask.StateId = extStateHandler.GetInternalStateId(extReq.ExtRequestState) ?? throw new Exception("No translation defined for external state.");
					await wfHandler.PromoteReqTask(updatedTask);
				}
			}
		}

		private async Task Acknowledge(ExternalRequest extRequest)
		{
			try
			{
				var Variables = new
				{
					id = extRequest.Id,
					extRequestState = ExtStates.ExtReqAcknowledged.ToString()
				};
				await ApiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestState, Variables);
			}
			catch(Exception exception)
			{
				Log.WriteError("Acknowledge External Request", $"Runs into exception: ", exception);
			}
		}

		private async Task SendNextRequest(WfTicket ticket, int oldTaskNumber)
		{
			if(UserConfig.ModRolloutBundleTasks)
			{
				// todo: bundle
			}
			else
			{
				WfReqTask? nextTask = InternalTicket.Tasks.FirstOrDefault(ta => ta.TaskNumber == oldTaskNumber + 1);
				if(nextTask != null)
				{
					await CreateExtRequest([nextTask]);
				}
			}
		}

		private async void HandleSubscriptionError(Exception exception)
		{
			Log.WriteError("External Request Subscription", $"Runs into exception: ", exception);
		}

		private void Dispose(long id)
		{
			extTicketSubscriptions[id]?.Dispose();
		}

		private void LogMessage(Exception? exception = null, string title = "", string message = "", bool ErrorFlag = false)
        {
            if (exception == null)
            {
                if(ErrorFlag)
                {
                    Log.WriteWarning(title, message);
                }
                else
                {
                    Log.WriteInfo(title, message);
                }
            }
            else
            {
                Log.WriteError(title, message, exception);
            }
        }
	}
}

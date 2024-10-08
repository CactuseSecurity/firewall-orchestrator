using FWO.Logging;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Client;
using FWO.Tufin.SecureChange;
using System.Text.Json;


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
		private ExternalTicketSystem actSystem = new();
		private string actTaskType = "";

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
				GetExtSystemFromConfig();
            	await extStateHandler.Init();
				if(UserConfig.ModRolloutBundleTasks)
				{
					// todo: bundle
					// If the API is called to open a ticket for a SecureApp application with more than 100 ARs,
					// it must be split into multiple tickets of up to 100 ARs each.
					// The count parameter specifies the number of tickets to be opened.
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
			string taskContent = ConstructContent(tasks);
			var Variables = new
			{
				ownerId = Owner.Id,
  				ticketId = InternalTicket.Id,
				taskNumber = tasks.First()?.TaskNumber ?? 0,
				extTicketSystem = JsonSerializer.Serialize(actSystem),
				extTaskType = actTaskType,
				extTaskContent = taskContent,
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
								if(extRequests.First().ExtRequestState != ExtStates.ExtReqRejected.ToString())
								{
									await SendNextRequest(intTicket, extRequests.First().TaskNumber);
								}
								else
								{
									// todo: Reject handling: set all following tasks to rejected?
								}
								// todo: push (some) state changes to modelling pages?
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

		private void GetExtSystemFromConfig()
		{
			// Todo: logic for multiple systems
			List<ExternalTicketSystem> extTicketSystems = JsonSerializer.Deserialize<List<ExternalTicketSystem>>(UserConfig.ExtTicketSystems) ?? [];
			if(extTicketSystems.Count > 0)
			{
				extSystemType = extTicketSystems.First().Type;
				actSystem = extTicketSystems.First();
			}
			else
			{
				throw new Exception("No external ticket system defined.");
			}
		}

		private string ConstructContent(List<WfReqTask> reqTasks)
		{
			ExternalTicket? ticket;
			if(extSystemType == ExternalTicketSystemType.TufinSecureChange)
			{
				ticket = new SCTicket(actSystem)
				{
					Subject = ConstructSubject(reqTasks.First()),
					Priority = SCTicketPriority.Low.ToString(), // todo: handling for manually handled requests (e.g. access)
					Requester = UserConfig.User.Name
				};
			}
			else
			{
				throw new Exception("Ticket system not supported yet");
			}
			if(ticket != null)
			{
				ticket.CreateRequestString(reqTasks);
				actTaskType = ticket.GetTaskTypeAsString(reqTasks.First());
				return JsonSerializer.Serialize(ticket);
			}
			return "";
		}

		private static string ConstructSubject(WfReqTask reqTask)
		{
			string onMgt = " on " + reqTask.OnManagement?.Name + "(" + reqTask.OnManagement?.Id + ")";
			string grpName = reqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName);
            return reqTask.TaskType switch
            {
                nameof(WfTaskType.access) => "Create rule on " + onMgt,
                nameof(WfTaskType.group_create) => "Create group " + grpName + onMgt,
                nameof(WfTaskType.group_modify) => "Modify group " + grpName + onMgt,
                nameof(WfTaskType.group_delete) => "Delete group " + grpName + onMgt,
                _ => "Request something",
            };
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
					extRequestState = extRequest.ExtRequestState == ExtStates.ExtReqRejected.ToString() ?
						ExtStates.ExtReqAckRejected.ToString() :
						ExtStates.ExtReqAcknowledged.ToString(),
					finishDate = DateTime.Now
				};
				await ApiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestFinal, Variables);
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

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
				await SendNextRequest(InternalTicket, 0);
			}
			catch(Exception exception)
			{
				Log.WriteError("External Request Creation", $"Runs into exception: ", exception);
			}
		}

		public void OnStateUpdate(List<ExternalRequest> extRequests)
		{
			if(extRequests.Count > 0)
			{
				ExternalRequest actExtRequest = extRequests.First();
				Task.Run(async () =>
				{
					try
					{
						await extStateHandler.Init();
						await wfHandler.Init([actExtRequest.OwnerId]);
            			ApiConnection.SetProperRole(AuthUser, [ Roles.Requester, Roles.Admin ]);
            			WfTicket? intTicket = await wfHandler.ResolveTicket(actExtRequest.TicketId);
						if(intTicket != null)
						{
							wfHandler.SetTicketEnv(intTicket);
							await UpdateTicket(intTicket, actExtRequest);
            				ApiConnection.SetProperRole(AuthUser, [ Roles.Modeller, Roles.Admin, Roles.Auditor ]);
							if(extStateHandler.GetInternalStateId(actExtRequest.ExtRequestState) >= wfHandler.ActStateMatrix.LowestEndState)
							{
								Dispose(actExtRequest.Id);
								await Acknowledge(actExtRequest);
								if(actExtRequest.ExtRequestState != ExtStates.ExtReqRejected.ToString())
								{
									await SendNextRequest(intTicket, actExtRequest.TaskNumber);
								}
								else
								{
									await RejectFollowingTasks(intTicket, actExtRequest.TaskNumber);
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
						ApiConnection.SetProperRole(AuthUser, [ Roles.Modeller, Roles.Admin, Roles.Auditor ]);
					}
				});
			}
		}

		private async Task SendNextRequest(WfTicket ticket, int oldTaskNumber)
		{
			WfReqTask? nextTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == oldTaskNumber + 1);
			if(nextTask != null)
			{
				if(UserConfig.ModRolloutBundleTasks && nextTask.TaskType == WfTaskType.access.ToString())
				{
					// todo: bundle also other task types?
					// If the API is called to open a ticket for a SecureApp application with more than 100 ARs,
					// it must be split into multiple tickets of up to 100 ARs each.
					// The count parameter specifies the number of tickets to be opened.

					List<WfReqTask> bundledTasks = [nextTask];
					int actTaskNumber = oldTaskNumber + 2;
					bool taskFound = true;
					while(taskFound)
					{
						WfReqTask? furtherTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == actTaskNumber);
						if(furtherTask != null && furtherTask.TaskType == WfTaskType.access.ToString())
						{
							bundledTasks.Add(furtherTask);
							actTaskNumber++;
						}
						else
						{
							taskFound = false;
						}
					}
					await CreateExtRequest(bundledTasks);
				}
				else
				{
					await CreateExtRequest([nextTask]);
				}
			}
		}

		private async Task CreateExtRequest(List<WfReqTask> tasks)
		{
			string taskContent = ConstructContent(tasks);
			Dictionary<string, List<int>>? bundledTasks;
			string? extQueryVars = null;
			if(tasks.Count > 1)
			{
				bundledTasks = new() { {ExternalVarKeys.BundledTasks, tasks.ConvertAll(t => t.TaskNumber)} };
				extQueryVars = JsonSerializer.Serialize(bundledTasks);
			}
			
			var Variables = new
			{
				ownerId = Owner.Id,
  				ticketId = InternalTicket.Id,
				taskNumber = tasks.First()?.TaskNumber ?? 0,
				extTicketSystem = JsonSerializer.Serialize(actSystem),
				extTaskType = actTaskType,
				extTaskContent = taskContent,
				extQueryVariables = extQueryVars,
				extRequestState = ExtStates.ExtReqInitialized.ToString()
			};
			ReturnId[]? reqIds = (await ApiConnection.SendQueryAsync<NewReturning>(ExtRequestQueries.addExtRequest, Variables)).ReturnIds;
			if(reqIds != null)
			{
				extTicketSubscriptions.Add(reqIds[0].NewId, ApiConnection.GetSubscription<List<ExternalRequest>>(HandleSubscriptionError, OnStateUpdate, ExtRequestQueries.subscribeExtRequestStateUpdate, new{ id = reqIds[0].NewId }));
			}
		}

		private async Task RejectFollowingTasks(WfTicket ticket, int lastTaskNumber)
		{
			ApiConnection.SetProperRole(AuthUser, [ Roles.Requester, Roles.Admin ]);
			int actTaskNumber = lastTaskNumber + 1;
			bool taskFound = true;
			while(taskFound)
			{
				WfReqTask? furtherTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == actTaskNumber);
				if(furtherTask != null)
				{
					await UpdateTaskState(furtherTask, ExtStates.ExtReqRejected.ToString());
					actTaskNumber++;
				}
				else
				{
					taskFound = false;
				}
			}
			ApiConnection.SetProperRole(AuthUser, [ Roles.Modeller, Roles.Admin, Roles.Auditor ]);
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
			List<int>? taskNumbers = null;
			if(extReq.ExtQueryVariables != null)
			{
				Dictionary<string, List<int>>? extQueryVars = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(extReq.ExtQueryVariables);
				extQueryVars?.TryGetValue(ExternalVarKeys.BundledTasks, out taskNumbers);
			}
			taskNumbers ??= [extReq.TaskNumber];
			foreach(var taskNumber in taskNumbers)
			{
				WfReqTask? updatedTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == taskNumber);
				if(updatedTask != null)
				{
					string? extTicketIdInTask = updatedTask.GetAddInfoValue(AdditionalInfoKeys.ExtIcketId);
					if(extReq.ExtTicketId != null && extReq.ExtTicketId != extTicketIdInTask)
					{
						await wfHandler.SetAddInfoInReqTask(updatedTask, AdditionalInfoKeys.ExtIcketId, extReq.ExtTicketId);
					}
					await UpdateTaskState(updatedTask, extReq.ExtRequestState);
				}
			}
		}

		private async Task UpdateTaskState(WfReqTask reqTask, string extReqState)
		{
			if(reqTask.StateId != extStateHandler.GetInternalStateId(extReqState))
			{
				wfHandler.SetReqTaskEnv(reqTask);
				reqTask.StateId = extStateHandler.GetInternalStateId(extReqState) ?? throw new Exception("No translation defined for external state.");
				await wfHandler.PromoteReqTask(reqTask);
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

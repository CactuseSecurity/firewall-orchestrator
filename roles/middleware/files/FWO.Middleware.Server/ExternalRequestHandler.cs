using FWO.Logging;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Tufin.SecureChange;
using System.Text.Json;
using FWO.Services;


namespace FWO.Middleware.Server
{
	public class ExternalRequestHandler
	{
		private readonly ApiConnection ApiConnection;
		private readonly ExtStateHandler extStateHandler;
		private readonly WfHandler wfHandler;
		private readonly UserConfig UserConfig;
		private ExternalTicketSystemType extSystemType = ExternalTicketSystemType.Generic;
		private ExternalTicketSystem actSystem = new();
		private string actTaskType = "";


		/// <summary>
		/// constructor for object with all data necessary for request handling
		/// </summary>
		public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection)
		{
			ApiConnection = apiConnection;
			UserConfig = userConfig;
			extStateHandler = new(apiConnection);
			wfHandler = new (LogMessage, userConfig, apiConnection, WorkflowPhases.request);
		}

		/// <summary>
		/// send the first request from ticket (called by UI via middleware client)
		/// </summary>
		public async Task<bool> SendFirstRequest(long ticketId)
		{
			try
			{
				WfTicket? intTicket = await InitAndResolve(ticketId);
				if(intTicket == null || intTicket.Tasks.Count == 0)
				{
					return false;
				}
				await SendNextRequest(intTicket, 0);
				return true;
			}
			catch(Exception exception)
			{
				Log.WriteError("External Request Creation", $"Runs into exception: ", exception);
				return false;
			}
		}

		/// <summary>
		/// send the next request from ticket if last is done and not rejected
		/// (called by scheduler after state change)
		/// </summary>
		public async Task HandleStateChange(ExternalRequest externalRequest)
		{
			try
			{
				WfTicket? intTicket = await InitAndResolve(externalRequest.TicketId);
				if(intTicket == null)
				{
					Log.WriteError("External Request Update", $"Ticket not found.");
				}
				else
				{
					wfHandler.SetTicketEnv(intTicket);
					await UpdateTicket(intTicket, externalRequest);
					if(extStateHandler.GetInternalStateId(externalRequest.ExtRequestState) >= wfHandler.ActStateMatrix.LowestEndState)
					{
						// Dispose(externalRequest.Id);
						await Acknowledge(externalRequest);
						if(externalRequest.ExtRequestState != ExtStates.ExtReqRejected.ToString())
						{
							await SendNextRequest(intTicket, externalRequest.TaskNumber);
						}
						else
						{
							await RejectFollowingTasks(intTicket, externalRequest.TaskNumber);
						}
					}
				}
			}
			catch(Exception exception)
			{
				Log.WriteError("External Request Update", $"Runs into exception: ", exception);
			}
		}

		private async Task<WfTicket?> InitAndResolve(long ticketId)
		{
			GetExtSystemFromConfig();
			await extStateHandler.Init();
			await wfHandler.Init([], false, true);
			return await wfHandler.ResolveTicket(ticketId);
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
					await CreateExtRequest(ticket, bundledTasks);
				}
				else
				{
					await CreateExtRequest(ticket, [nextTask]);
				}
			}
		}

		private async Task CreateExtRequest( WfTicket ticket, List<WfReqTask> tasks)
		{
			string taskContent = ConstructContent(tasks, ticket.Requester);
			Dictionary<string, List<int>>? bundledTasks;
			string? extQueryVars = null;
			if(tasks.Count > 1)
			{
				bundledTasks = new() { {ExternalVarKeys.BundledTasks, tasks.ConvertAll(t => t.TaskNumber)} };
				extQueryVars = JsonSerializer.Serialize(bundledTasks);
			}
			
			var Variables = new
			{
				ownerId = ticket.Tasks.First()?.Owners.First()?.Owner.Id,
  				ticketId = ticket.Id,
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
				//extTicketSubscriptions.Add(reqIds[0].NewId, ApiConnection.GetSubscription<List<ExternalRequest>>(HandleSubscriptionError, OnStateUpdate, ExtRequestQueries.subscribeExtRequestStateUpdate, new{ id = reqIds[0].NewId }));
			}
		}

		private async Task RejectFollowingTasks(WfTicket ticket, int lastTaskNumber)
		{
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

		private string ConstructContent(List<WfReqTask> reqTasks, UiUser? requester)
		{
			ExternalTicket? ticket;
			if(extSystemType == ExternalTicketSystemType.TufinSecureChange)
			{
				ticket = new SCTicket(actSystem)
				{
					Subject = ConstructSubject(reqTasks.First()),
					Priority = SCTicketPriority.Low.ToString(), // todo: handling for manually handled requests (e.g. access)
					Requester = requester?.Name ?? ""
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
			if(!string.IsNullOrEmpty(extReq.ExtQueryVariables))
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

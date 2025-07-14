using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services;
using FWO.ExternalSystems;
using FWO.ExternalSystems.Tufin.SecureChange;
using System.Text.Json;


namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class to execute handling of external requests
	/// </summary>
	public class ExternalRequestHandler
	{
		private readonly ApiConnection ApiConnection;
		private readonly ExtStateHandler? extStateHandler;
		private readonly WfHandler wfHandler;
		private readonly UserConfig UserConfig;
		private ExternalTicketSystemType extSystemType = ExternalTicketSystemType.Generic;
		private ExternalTicketSystem actSystem = new();
		private string actTaskType = "";
		private List<IpProtocol> ipProtos = [];
		private List<UserGroup>? ownerGroups = [];


		/// <summary>
		/// constructor for object with all data necessary for request handling
		/// </summary>
		public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection)
		{
			ApiConnection = apiConnection;
			UserConfig = userConfig;
			extStateHandler = new(apiConnection);
			Task.Run(GetInternalGroups).Wait();
			wfHandler = new (LogMessage, userConfig, apiConnection, WorkflowPhases.request, ownerGroups);
		}

		/// <summary>
		/// constructor only for unit testing
		/// </summary>
		public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection, List<UserGroup>? userGroups)
		{
			ApiConnection = apiConnection;
			UserConfig = userConfig;
			extStateHandler = new(apiConnection);
			wfHandler = new (LogMessage, userConfig, apiConnection, WorkflowPhases.request, userGroups);
		}

		/// <summary>
		/// send the first request from ticket (called by UI via middleware client)
		/// may also be a higher task number in case of a reinit
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
				int lastFinishedTask = 0;
				foreach(var task in intTicket.Tasks.OrderBy(t => t.TaskNumber))
				{
					if(task.StateId > wfHandler.StateMatrix(task.TaskType).LowestEndState)
					{
						lastFinishedTask = task.TaskNumber;
					}
				}
				return await CreateNextRequest(intTicket, lastFinishedTask);
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
			WfTicket? intTicket = await InitAndResolve(externalRequest.TicketId);
			if(intTicket == null)
			{
				Log.WriteError("External Request Update", $"Ticket not found.");
			}
			else
			{
				wfHandler.SetTicketEnv(intTicket);
				await UpdateTicket(intTicket, externalRequest);
				if(extStateHandler != null && extStateHandler.GetInternalStateId(externalRequest.ExtRequestState) >= wfHandler.ActStateMatrix.LowestEndState)
				{
					await Acknowledge(externalRequest);
					if(externalRequest.ExtRequestState == ExtStates.ExtReqRejected.ToString())
					{
						await RejectFollowingTasks(intTicket, externalRequest.TaskNumber);
						Log.WriteInfo($"External Request {externalRequest.Id} rejected", $"Reject Following Tasks for internal ticket {intTicket.Id}");
					}
					else
					{
						await CreateNextRequest(intTicket, externalRequest.TaskNumber, externalRequest);
					}
				}
			}
		}

		/// <summary>
		/// patch the external request state (called by admin in UI via middleware client)
		/// </summary>
		public async Task<bool> PatchState(ExternalRequest externalRequest)
		{
			try
			{
				await UpdateRequestState(externalRequest);
				if(externalRequest.ExtRequestState == ExtStates.ExtReqRejected.ToString() ||
					externalRequest.ExtRequestState == ExtStates.ExtReqDone.ToString())
				{
					await HandleStateChange(externalRequest);
				}
				return true;
			}
			catch(Exception exception)
			{
				Log.WriteError("Patch External Request State", $"Runs into exception: ", exception);
				return false;
			}
		}

		private async Task UpdateRequestState(ExternalRequest request)
		{
			try
			{
				var Variables = new
				{
					id = request.Id,
					extRequestState = request.ExtRequestState
				};
				await ApiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestProcess, Variables);
			}
			catch(Exception exception)
			{
				Log.WriteError("External Request Handler", $"State update failed: ", exception);
			}
		}

		private async Task<WfTicket?> InitAndResolve(long ticketId)
		{
			GetExtSystemFromConfig();
			ipProtos = await ApiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols);
			return await wfHandler.Init() ? await wfHandler.ResolveTicket(ticketId) : null;
		}

		private async Task GetInternalGroups()
		{
			List<Ldap> connectedLdaps = await ApiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
			Ldap internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new KeyNotFoundException("No internal Ldap with group handling found.");

			List<GroupGetReturnParameters> allGroups = await internalLdap.GetAllInternalGroups();
			ownerGroups = [];
			foreach (var ldapUserGroup in allGroups)
			{
				if(ldapUserGroup.OwnerGroup)
				{
					UserGroup group = new ()
					{ 
						Dn = ldapUserGroup.GroupDn,
						Name = new DistName(ldapUserGroup.GroupDn).Group,
						OwnerGroup = ldapUserGroup.OwnerGroup
					};
					foreach (var userDn in ldapUserGroup.Members)
					{
						UiUser newUser = new () { Dn = userDn, Name = new DistName(userDn).UserName };
						group.Users.Add(newUser);
					}
					ownerGroups.Add(group);
				}
			}
		}

		/// <summary>
		/// get number of last processed request task (public only for unit testing)
		/// </summary>
		/// <param name="extQueryVars"></param>
		/// <param name="oldTaskNumber"></param>
		/// <returns></returns>
		public static int GetLastTaskNumber(string extQueryVars, int oldTaskNumber)
		{
			List<int>? taskNumbers = null;
			Dictionary<string, List<int>>? extQueryVarDict = JsonSerializer.Deserialize<Dictionary<string, List<int>>?>(extQueryVars);
			extQueryVarDict?.TryGetValue(ExternalVarKeys.BundledTasks, out taskNumbers);
			if(taskNumbers != null && taskNumbers.Count > 0)
			{
				return taskNumbers[^1];
			}
			else
			{
				return oldTaskNumber;
			}
		}

		/// <summary>
		/// create next external request from internal ticket task list (public only for unit testing)
		/// </summary>
		/// <param name="ticket"></param>
		/// <param name="oldTaskNumber"></param>
		/// <param name="oldRequest"></param>
		/// <returns></returns>

		public async Task<bool> CreateNextRequest(WfTicket ticket, int oldTaskNumber, ExternalRequest? oldRequest = null)
		{
			int lastTaskNumber = UserConfig.ModRolloutBundleTasks && oldRequest != null && oldRequest.ExtQueryVariables != "" ?
				GetLastTaskNumber(oldRequest.ExtQueryVariables, oldTaskNumber) : oldTaskNumber;
			WfReqTask? nextTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == lastTaskNumber + 1);
			if(nextTask == null)
			{
				Log.WriteDebug("CreateNextRequest", "No more task found.");
				return false;
			}
			else
			{
				int waitCycles = GetWaitCycles(nextTask.TaskType, oldRequest);
				if(nextTask.TaskType == WfTaskType.access.ToString() || nextTask.TaskType == WfTaskType.rule_modify.ToString() || nextTask.TaskType == WfTaskType.rule_delete.ToString())
				{
					List<WfReqTask> bundledTasks = [];
					List<WfReqTask> handledTasks = [nextTask];
					BundleTasks(ticket, lastTaskNumber, nextTask, bundledTasks, handledTasks);
					await CreateExtRequest(ticket, bundledTasks, handledTasks, waitCycles);
				}
				else
				{
					await CreateExtRequest(ticket, [nextTask], [nextTask], waitCycles);
				}
			}
			Log.WriteInfo("CreateNextRequest", $"Created Request for ticket {ticket.Id}.");
			return true;
		}

		private void BundleTasks(WfTicket ticket, int lastTaskNumber, WfReqTask nextTask, List<WfReqTask> bundledTasks, List<WfReqTask> handledTasks)
		{
			int actTaskNumber = lastTaskNumber + 2;
			bool taskFound = true;
			WfReqTask? actBundledTask = nextTask;
			while (taskFound && bundledTasks.Count < actSystem.MaxBundledTasks())
			{
				WfReqTask? furtherTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == actTaskNumber);
				if (furtherTask != null && furtherTask.TaskType == nextTask.TaskType)
				{
					actBundledTask ??= new(furtherTask);
					if (actSystem.BundleGateways() && actSystem.TaskTypesToBundleGateways().Contains(nextTask.TaskType) && IsSameRuleOnDiffGw(actBundledTask, furtherTask))
					{
						actBundledTask.Elements.AddRange(furtherTask.GetRuleElements().ConvertAll(e => e.ToReqElement()));
					}
					else if (UserConfig.ModRolloutBundleTasks)
					{
						bundledTasks.Add(actBundledTask);
						actBundledTask = null;
					}
					handledTasks.Add(furtherTask);
					actTaskNumber++;
				}
				else
				{
					if (actBundledTask != null)
					{
						bundledTasks.Add(actBundledTask);
					}
					taskFound = false;
				}
			}
		}

		/// <summary>
		/// qad heuristic for Tufin SC (public only for unit testing)
		/// </summary>
		/// <param name="taskType"></param>
		/// <param name="oldRequest"></param>
		/// <returns></returns>
		public int GetWaitCycles(string taskType, ExternalRequest? oldRequest)
		{
			// TODO: to be refined
			if(oldRequest != null && UserConfig.ExternalRequestWaitCycles > 0 &&
				// last request handled group
				(oldRequest.ExtRequestType == "(NetworkObjectModify, CREATE)" || oldRequest.ExtRequestType == "(NetworkObjectModify, UPDATE)") &&
					// now access request
					(taskType == WfTaskType.access.ToString() ||
					// or last request created new objects in group
					ContainsNewObj(oldRequest.ExtRequestContent)))
			{
				return UserConfig.ExternalRequestWaitCycles;
			}
			return 0;
		}

		private static bool IsSameRuleOnDiffGw(WfReqTask? task1, WfReqTask? task2)
		{
			return task1 != null && task2 != null && task1.ManagementId == task2.ManagementId &&
				task1.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) == task2.GetAddInfoIntValue(AdditionalInfoKeys.ConnId);
		}

		private static bool ContainsNewObj(string contentString)
		{
			return contentString.Contains("\"object_updated_status\": \"NEW\"") || contentString.Contains("object_updated_status\\u0022: \\u0022NEW\\u0022") ||
				contentString.Contains("\"object_updated_status\":\"NEW\"") || contentString.Contains("object_updated_status\\u0022:\\u0022NEW\\u0022");
		}

		private async Task CreateExtRequest(WfTicket ticket, List<WfReqTask> tasks, List<WfReqTask> handledTasks, int waitCycles)
		{
			string taskContent = await ConstructContent(tasks, ticket.Requester);
			Dictionary<string, List<int>>? handledTaskNumbers;
			string? extQueryVars = null;
			if(handledTasks.Count > 1)
			{
				handledTaskNumbers = new() { {ExternalVarKeys.BundledTasks, handledTasks.ConvertAll(t => t.TaskNumber)} };
				extQueryVars = JsonSerializer.Serialize(handledTaskNumbers);
			}
			
			var Variables = new
			{
				ownerId = ticket.Tasks.FirstOrDefault()?.Owners.FirstOrDefault()?.Owner.Id,
  				ticketId = ticket.Id,
				taskNumber = tasks.FirstOrDefault()?.TaskNumber ?? 0,
				extTicketSystem = JsonSerializer.Serialize(actSystem),
				extTaskType = actTaskType,
				extTaskContent = taskContent,
				extQueryVariables = extQueryVars ?? "",
				extRequestState = ExtStates.ExtReqInitialized.ToString(),
				waitCycles = waitCycles
			};
			await ApiConnection.SendQueryAsync<ReturnIdWrapper>(ExtRequestQueries.addExtRequest, Variables);
			await LogRequestTasks(handledTasks, ticket.Requester?.Name, ModellingTypes.ChangeType.Request);
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
				extSystemType = extTicketSystems[0].Type;
				actSystem = extTicketSystems[0];
			}
			else
			{
				throw new InvalidOperationException("No external ticket system defined.");
			}
		}

		private async Task<string> ConstructContent(List<WfReqTask> reqTasks, UiUser? requester)
		{
			ExternalTicket? ticket;
			if(extSystemType == ExternalTicketSystemType.TufinSecureChange)
			{
				ticket = new SCTicket(actSystem)
				{
					Subject = ConstructSubject(reqTasks.Count > 0 ? reqTasks[0] : throw new ArgumentException("No Task given")),
					Priority = SCTicketPriority.Low.ToString(), // todo: handling for manually handled requests (e.g. access)
					Requester = requester?.Name ?? ""
				};
			}
			else
			{
				throw new NotSupportedException("Ticket system not supported yet");
			}
			if (ticket != null)
			{
				ModellingNamingConvention? namingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(UserConfig.ModNamingConvention);
				await ticket.CreateRequestString(reqTasks, ipProtos, namingConvention);
				actTaskType = ticket.GetTaskTypeAsString(reqTasks[0]);
				return ticket.TicketText;
			}
			return "";
		}

		private string ConstructSubject(WfReqTask reqTask)
		{
			string appId = reqTask.Owners.Count > 0 ? (reqTask.Owners.FirstOrDefault()?.Owner.ExtAppId ?? "") : "";
			string onMgt = UserConfig.GetText("on") + reqTask.OnManagement?.Name + "(" + reqTask.OnManagement?.Id + ")";
			string grpName = " " + reqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName);
            return (appId != "" ? appId + ": " : "") + reqTask.TaskType switch
            {
                nameof(WfTaskType.access) => UserConfig.GetText("create_rule") + onMgt,
				nameof(WfTaskType.rule_modify) => UserConfig.GetText("modify_rule") + onMgt,
				nameof(WfTaskType.rule_delete) => UserConfig.GetText("remove_rule") + onMgt,
                nameof(WfTaskType.group_create) => UserConfig.GetText("create_group") + grpName + onMgt,
                nameof(WfTaskType.group_modify) => UserConfig.GetText("modify_group") + grpName + onMgt,
                nameof(WfTaskType.group_delete) => UserConfig.GetText("delete_group") + grpName + onMgt,
                _ => "Request something"
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

					if(extReq.ExtRequestState == ExtStates.ExtReqDone.ToString())
					{
						await LogRequestTasks([updatedTask], actSystem.Name, ModellingTypes.ChangeType.Implement);
					}
					else if(extReq.ExtRequestState == ExtStates.ExtReqRejected.ToString())
					{
						await LogRequestTasks([updatedTask], actSystem.Name, ModellingTypes.ChangeType.Reject, extReq.LastProcessingResponse ?? extReq.LastCreationResponse ?? "");
					}
				}
				else
				{
					Log.WriteError("UpdateTicket", $"Task not found in Ticket {ticket.Id}: {taskNumber}");
				}
			}
		}

		private async Task UpdateTaskState(WfReqTask reqTask, string extReqState)
		{
			if(extStateHandler != null && reqTask.StateId != extStateHandler.GetInternalStateId(extReqState))
			{
				wfHandler.SetReqTaskEnv(reqTask);
				reqTask.StateId = extStateHandler.GetInternalStateId(extReqState) ?? throw new ArgumentException("No translation defined for external state.");
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

		private async Task LogRequestTasks(List<WfReqTask> tasks, string? requester, ModellingTypes.ChangeType changeType, string? comment = null)
		{
			foreach(var task in tasks)
			{
				(long objId, ModellingTypes.ModObjectType objType) = GetObject(task);
				await ModellingHandlerBase.LogChange(changeType, objType, objId,
                	$"{ConstructLogMessageText(changeType)} {task.Title} on {task.OnManagement?.Name}{(comment != null ? ", " + comment : "")}", 
					ApiConnection, UserConfig, task.Owners.FirstOrDefault()?.Owner.Id, DefaultInit.DoNothing, requester);
			}
		}

		private static (long, ModellingTypes.ModObjectType) GetObject(WfReqTask task)
		{
			if(task.GetAddInfoLongValue(AdditionalInfoKeys.ConnId) != null)
			{
				return (task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) ?? 0, ModellingTypes.ModObjectType.Connection);
			}
			else if(task.GetAddInfoLongValue(AdditionalInfoKeys.AppRoleId) != null)
			{
				return (task.GetAddInfoIntValue(AdditionalInfoKeys.AppRoleId) ?? 0, ModellingTypes.ModObjectType.AppRole);
			}
			else if(task.GetAddInfoIntValue(AdditionalInfoKeys.SvcGrpId) != null)
			{
				return (task.GetAddInfoIntValue(AdditionalInfoKeys.SvcGrpId) ?? 0, ModellingTypes.ModObjectType.ServiceGroup);
			}
			return (0, ModellingTypes.ModObjectType.Connection);
		}

		private static string ConstructLogMessageText(ModellingTypes.ChangeType changeType)
		{
            return changeType switch
            {
                ModellingTypes.ChangeType.Request => "Requested",
                ModellingTypes.ChangeType.Implement => "Implemented",
                ModellingTypes.ChangeType.Reject => "Rejected",
                _ => "",
            };
        }

		private static void LogMessage(Exception? exception = null, string title = "", string message = "", bool ErrorFlag = false)
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

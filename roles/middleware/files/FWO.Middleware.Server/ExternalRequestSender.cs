﻿using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.ExternalSystems;
using FWO.ExternalSystems.Tufin.SecureChange;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the sending of external requests
	/// </summary>
	public class ExternalRequestSender
	{
		/// <summary>
		/// Api Connection
		/// </summary>
		protected readonly ApiConnection apiConnection;

		/// <summary>
		/// Global Config
		/// </summary>
		protected GlobalConfig globalConfig;

		private readonly UserConfig userConfig;
		private readonly SCClient? InjScClient;
		ExternalTicketSystem? ExtTicketSystem;
	

		// todo: map to internal states to use "lowest_end_state" setting ?
		private static readonly List<string> openRequestStates =
		[
			ExtStates.ExtReqInitialized.ToString(),
			ExtStates.ExtReqFailed.ToString(),
			ExtStates.ExtReqRequested.ToString(),
			ExtStates.ExtReqInProgress.ToString()
		];
		
		private const string LogMessageTitle = "External Request Sender";


		/// <summary>
		/// Constructor for External Request Sender
		/// </summary>
		public ExternalRequestSender(ApiConnection apiConnection, GlobalConfig globalConfig, SCClient? injScClient = null)
		{
			this.apiConnection = apiConnection;
			this.globalConfig = globalConfig;
			userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });
			InjScClient = injScClient;
		}

		/// <summary>
		/// Run the External Request Sender
		/// </summary>
		public async Task<List<string>> Run()
		{
			List<string> FailedRequests = [];
			ExternalRequestDataHelper openRequests = await apiConnection.SendQueryAsync<ExternalRequestDataHelper>(ExtRequestQueries.getAndLockOpenRequests, new {states = openRequestStates});
			foreach (var request in openRequests.ExternalRequests)
			{
				await HandleRequest(request, FailedRequests);
			}
			await ReleaseRemainingLocks(openRequests.ExternalRequests);
			return FailedRequests;
		}

		private async Task HandleRequest(ExternalRequest request, List<string> FailedRequests)
		{
			try
			{
				ExtTicketSystem = JsonSerializer.Deserialize<ExternalTicketSystem>(request.ExtTicketSystem) ?? throw new JsonException("No Ticket System");
				if(request.ExtRequestState == ExtStates.ExtReqInitialized.ToString() ||
					request.ExtRequestState == ExtStates.ExtReqFailed.ToString()) // try again
				{
					if(request.WaitCycles > 0)
					{
						await CountDownWaitCycle(request);
					}
					else
					{
						await SendRequest(request);
					}
				}
				else
				{
					await RefreshState(request);
				}
				if ((await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestLock, new {id = request.Id, locked = false})).UpdatedIdLong == request.Id)
				{
					request.Locked = false;
				}
			}
			catch (Exception exception)
			{
				Log.WriteError(LogMessageTitle, "Runs into exception: ", exception);
				FailedRequests.Add(RequestInfo(request));
			}
		}

		private static string RequestInfo(ExternalRequest request)
		{
			return $"Request Id: {request.Id}, Internal TicketId: {request.TicketId}, TaskNo: {request.TaskNumber}";
		}

		private async Task ReleaseRemainingLocks(List<ExternalRequest> requests)
		{
			try
			{
				foreach (var request in requests.Where(r => r.Locked))
				{
					await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestLock, new { id = request.Id, locked = false });
				}
			}
			catch (Exception exception)
			{
				Log.WriteError(LogMessageTitle, "Release Lock runs into exception: ", exception);
			}
		}

		private async Task SendRequest(ExternalRequest request)
		{
			ExternalTicket? ticket = ConstructTicket(request);
			try
			{
				Log.WriteInfo(LogMessageTitle, $"Sending {RequestInfo(request)}");
				request.Attempts++;
				RestResponse<int> ticketIdResponse = await ticket.CreateExternalTicket();
				request.LastMessage = ticketIdResponse.Content;
				if (ticketIdResponse.StatusCode == HttpStatusCode.OK || ticketIdResponse.StatusCode == HttpStatusCode.Created)
				{
					var locationHeader = ticketIdResponse.Headers?.FirstOrDefault(h => h.Name.Equals("location", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
					if (!string.IsNullOrEmpty(locationHeader))
					{
						Uri locationUri = new(locationHeader);
						request.ExtTicketId = locationUri.Segments[^1];
					}
					request.ExtRequestState = ExtStates.ExtReqRequested.ToString();
					await UpdateRequestCreation(request);
					Log.WriteDebug(LogMessageTitle, $"{RequestInfo(request)}. Success Message: " + ticketIdResponse.Content);
				}
				else
				{
					Log.WriteError(LogMessageTitle, $"{RequestInfo(request)}. Error Message: " + ticketIdResponse.StatusDescription + ", " + ticketIdResponse.Content);
					if (AnalyseForRejected(ticketIdResponse))
					{
						await RejectRequest(request);
					}
					else
					{
						request.ExtRequestState = ExtStates.ExtReqFailed.ToString();
						await UpdateRequestCreation(request);
					}
					throw new ProcessingFailedException("RestResponse: HttpStatusCode not OK");
				}
			}
			catch (ProcessingFailedException)
			{
				throw;
			}
			catch (Exception)
			{
				if (!await HandleTimeOut(request, ticket))
				{
					throw;
				}
			}
		}

		private ExternalTicket ConstructTicket(ExternalRequest request)
		{
			ExternalTicket ticket;
			
			if (ExtTicketSystem?.Type == ExternalTicketSystemType.TufinSecureChange)
			{
				ticket = new SCTicket(ExtTicketSystem, InjScClient)
				{
					TicketSystem = ExtTicketSystem,
					TicketText = request.ExtRequestContent
				};
			}
			else
			{
				throw new NotSupportedException("Ticket system not supported yet");
			}
			return ticket;
		}

		private async Task RejectRequest(ExternalRequest request)
		{
			request.ExtRequestState = ExtStates.ExtReqRejected.ToString();
			await UpdateRequestCreation(request);
			ExternalRequestHandler extReqHandler = new(userConfig, apiConnection);
			await extReqHandler.HandleStateChange(request);
		}

		private async Task<bool> HandleTimeOut(ExternalRequest request, ExternalTicket? ticket)
		{
			if(ticket != null && request.Attempts > 0)
			{
				try
				{
					if (request.Attempts >= ticket.TicketSystem.MaxAttempts)
					{
						await RejectRequest(request);
					}
					else
					{
						request.ExtRequestState = ExtStates.ExtReqFailed.ToString();
						request.WaitCycles = request.Attempts * ticket.TicketSystem.CyclesBetweenAttempts;
						await UpdateRequestCreation(request);
						return true;
					}
				}
				catch(Exception exception)
				{
					Log.WriteError(LogMessageTitle, "Timeout handling failed: ", exception);
				}
			}
			return false;
		}

		private static bool AnalyseForRejected(RestResponse<int>? ticketIdResponse)
		{
			return ticketIdResponse != null && ticketIdResponse.Content != null && 
				((ticketIdResponse.Content.Contains("GENERAL_ERROR") && !TryAgain(ticketIdResponse))||
				ticketIdResponse.Content.Contains("ILLEGAL_ARGUMENT_ERROR") ||
				ticketIdResponse.Content.Contains("FIELD_VALIDATION_ERROR") ||
				ticketIdResponse.Content.Contains("WEB_APPLICATION_ERROR") ||
				ticketIdResponse.Content.Contains("implementation failure"));
		}

		private static bool TryAgain(RestResponse<int> ticketIdResponse)
		{
			return ticketIdResponse.Content != null &&
				ticketIdResponse.Content.Contains("Unable to rollback against JDBC Connection");
		}

		private async Task RefreshState(ExternalRequest request)
		{
			(request.ExtRequestState, request.LastMessage) = await PollState(request);
			await UpdateRequestProcess(request);
			ExternalRequestHandler extReqHandler = new(userConfig, apiConnection);
			await extReqHandler.HandleStateChange(request);
		}

		private async Task<(string, string?)> PollState(ExternalRequest request)
		{
			try
			{
				ExternalTicket ticket;
				if (ExtTicketSystem?.Type == ExternalTicketSystemType.TufinSecureChange)
				{
					ticket = new SCTicket(ExtTicketSystem, InjScClient)
					{
						TicketId = request.ExtTicketId
					};
				}
				else
				{
					throw new NotSupportedException("Ticket system not supported yet");
				}
				return await ticket.GetNewState(request.ExtRequestState);
			}
			catch (Exception exc)
			{
				request.LastMessage = exc.Message;
				await UpdateRequestProcess(request);
				throw;
			}
		}

		private async Task UpdateRequestCreation(ExternalRequest request)
		{
			var Variables = new
			{
				id = request.Id,
				extRequestState = request.ExtRequestState,
				extTicketId = request.ExtTicketId,
				creationResponse = request.LastMessage,
				waitCycles = request.WaitCycles,
				attempts = request.Attempts
			};
			await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestCreation, Variables);
		}

		private async Task UpdateRequestProcess(ExternalRequest request)
		{
			try
			{
				var Variables = new
				{
					id = request.Id,
					extRequestState = request.ExtRequestState,
					processingResponse = request.LastMessage
				};
				await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestProcess, Variables);
			}
			catch (Exception exception)
			{
				Log.WriteError(LogMessageTitle, "UpdateRequestProcess failed: ", exception);
			}
		}

		private async Task CountDownWaitCycle(ExternalRequest request)
		{
			var Variables = new
			{
				id = request.Id,
				waitCycles = --request.WaitCycles
			};
			await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestWaitCycles, Variables);
		}
	}
}

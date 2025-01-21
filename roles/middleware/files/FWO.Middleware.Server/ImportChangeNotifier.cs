using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Mail;
using FWO.Encryption;
using FWO.Report;
using FWO.Report.Filter;
using Newtonsoft.Json;
using System.Text.Json.Serialization; 
using System.Text.RegularExpressions;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the Import Change Notification
	/// </summary>
	public class ImportChangeNotifier
	{
		/// <summary>
		/// Api Connection
		/// </summary>
		protected readonly ApiConnection apiConnection;

		/// <summary>
		/// Global Config
		/// </summary>
		protected GlobalConfig globalConfig;

		private ReportBase? changeReport;

		private struct ImportToNotify
		{
			[JsonProperty("control_id"), JsonPropertyName("control_id")]
			public long ControlId { get; set; }

			[JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
			public int MgmtId { get; set; }

			[JsonProperty("management"), JsonPropertyName("management")]
			public ImportManagement Mgmt { get; set; }

			[JsonProperty("stop_time"), JsonPropertyName("stop_time")]
			public DateTime StopTime { get; set; }

			[JsonProperty("security_relevant_changes_counter"), JsonPropertyName("security_relevant_changes_counter")]
			public int RelevantChanges { get; set; }
		};
		private struct ImportManagement
		{
			[JsonProperty("mgm_name"), JsonPropertyName("mgm_name")]
			public string MgmtName { get; set; }
		}

		private List<ImportToNotify> importsToNotify = [];

		private bool WorkInProgress = false;
		private readonly DeviceFilter deviceFilter = new();
		private List<int> importedManagements = [];
		private readonly UserConfig userConfig;


		/// <summary>
		/// Constructor for Import Change Notifier
		/// </summary>
		public ImportChangeNotifier(ApiConnection apiConnection, GlobalConfig globalConfig)
		{
			this.apiConnection = apiConnection;
			this.globalConfig = globalConfig;
			userConfig = new(globalConfig);
		}

		/// <summary>
		/// Run the Import Change Notifier
		/// </summary>
		public async Task<bool> Run()
		{
			try
			{
				if(!WorkInProgress)
				{
					WorkInProgress = true;
					if(await NewImportFound())
					{
						if(globalConfig.ImpChangeNotifyType != (int)ImpChangeNotificationType.SimpleText)
						{
							await GenerateChangeReport();
						}
						await SendEmail();
						await SetImportsNotified();
					}
					WorkInProgress = false;
				}
			}
			catch(Exception exception)
			{
				Log.WriteError("Import Change Notification", $"Runs into exception: ", exception);
				WorkInProgress = false;
				return false;
			}
			return true;
		}

		private async Task<bool> NewImportFound()
		{
			importsToNotify = await apiConnection.SendQueryAsync<List<ImportToNotify>>(ReportQueries.getImportsToNotify);
			importedManagements = [];
			foreach(var imp in importsToNotify)
			{
				if(!importedManagements.Contains(imp.MgmtId))
				{
					importedManagements.Add(imp.MgmtId);
				}
			}
			return importsToNotify.Count > 0;
		}

		private async Task GenerateChangeReport()
		{
			try
			{
				CancellationToken token = new();
				changeReport = ReportBase.ConstructReport(new ReportTemplate("", await SetFilters()), userConfig);
				ReportData reportData = new();
				await changeReport.Generate(int.MaxValue, apiConnection,
					rep =>
					{
						reportData.ManagementData = rep.ManagementData;
						foreach (var mgm in reportData.ManagementData)
						{
							mgm.Ignore = !deviceFilter.getSelectedManagements().Contains(mgm.Id);
						}
						return Task.CompletedTask;
					}, token);
			}
			catch (Exception exception)
			{
				Log.WriteError("Import Change Notifier", $"Report generation leads to exception.", exception);
			}
		}

		private async Task<ReportParams> SetFilters()
		{
			deviceFilter.Managements = (await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement))
				.Where(x => importedManagements.Contains(x.Id)).ToList();
			deviceFilter.applyFullDeviceSelection(true);

			return new((int)ReportType.Changes, deviceFilter)
			{
				TimeFilter = new()
				{
					TimeRangeType = TimeRangeType.Fixeddates,
					StartTime = importsToNotify.First().StopTime,
					EndTime = importsToNotify.Last().StopTime.AddSeconds(1)
				}
			};
		}

		private async Task SendEmail()
		{
			string decryptedSecret = "";
			try
			{
				string mainKey = AesEnc.GetMainKey();
				decryptedSecret = AesEnc.Decrypt(globalConfig.EmailPassword, mainKey);
			}
			catch (Exception exception)
			{
				Log.WriteError("Import Change Notifier", $"Could not decrypt mailserver password.", exception);				
			}

			EmailConnection emailConnection = new(globalConfig.EmailServerAddress, globalConfig.EmailPort,
				globalConfig.EmailTls, globalConfig.EmailUser, decryptedSecret, globalConfig.EmailSenderAddress);
			MailKitMailer mailer = new(emailConnection);

			MailData? mail = await PrepareEmail();

            await mailer.SendAsync(mail, emailConnection, new CancellationToken(),
				globalConfig.ImpChangeNotifyType == (int)ImpChangeNotificationType.HtmlInBody);
		}

		private async Task<MailData> PrepareEmail()
		{
			string subject = globalConfig.ImpChangeNotifySubject;
			string body = CreateBody();
			FormFile? attachment = null;
			if(changeReport != null)
			{
				switch(globalConfig.ImpChangeNotifyType)
				{
					case (int)ImpChangeNotificationType.HtmlInBody:
						body += changeReport?.ExportToHtml();
						break;
					case (int)ImpChangeNotificationType.PdfAsAttachment:
						string? pdfData = await changeReport.ToPdf(PeachPDF.PdfSharpCore.PageSize.A4);

						if (string.IsNullOrWhiteSpace(pdfData))
							throw new Exception("No Pdf generated.");

                        attachment = CreateAttachment(pdfData, GlobalConst.kPdf);
						break;
					case (int)ImpChangeNotificationType.HtmlAsAttachment:
						attachment = CreateAttachment(changeReport?.ExportToHtml(), GlobalConst.kHtml);
						break;
					// case (int)ImpChangeNotificationType.CsvAsAttachment: // Currently not implemented
					//     attachment = CreateAttachment(changeReport?.ExportToCsv(), GlobalConst.kCsv);
					//     break;
					case (int)ImpChangeNotificationType.JsonAsAttachment:
						attachment = CreateAttachment(changeReport?.ExportToJson(), GlobalConst.kJson);
						break;
					default:
						break;
				}
			}
			MailData mailData = new(CollectRecipients(), subject, body);
			if(attachment != null)
			{
				mailData.Attachments = new FormFileCollection() { attachment };
			}
			return mailData;
		}

		private string CreateBody()
		{
			string body = globalConfig.ImpChangeNotifyBody;
			foreach(var mgmtId in importedManagements)
			{
				int mgmtCounter = 0;
				foreach(var imp in importsToNotify.Where(x => x.MgmtId == mgmtId))
				{
					mgmtCounter += imp.RelevantChanges;
				}
				body += globalConfig.ImpChangeNotifyType == (int)ImpChangeNotificationType.HtmlInBody ? "<br>" : "\r\n\r\n";
				body += $"{importsToNotify.FirstOrDefault(x => x.MgmtId == mgmtId).Mgmt.MgmtName} (id={mgmtId}): {mgmtCounter} {userConfig.GetText("changes")}";
			}
			return body;
		}

		private FormFile? CreateAttachment(string? content, string fileFormat)
		{
			if(content != null)
			{                
				MemoryStream memoryStream = new(System.Text.Encoding.UTF8.GetBytes(content));
				string fileName = $"{Regex.Replace(globalConfig.ImpChangeNotifySubject, @"\s", "")}_{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssK")}.{fileFormat}";
				return new(memoryStream, 0, memoryStream.Length, "FWO-Report-Attachment", fileName)
				{
					Headers = new HeaderDictionary(),
					ContentType = $"application/{fileFormat}"
				};
			}
			return null;
		}
        private List<string> CollectRecipients()
        {
            if(globalConfig.UseDummyEmailAddress)
            {
                return [globalConfig.DummyEmailAddress];
            }
            string[] separatingStrings = [",", ";", "|"];
            return globalConfig.ImpChangeNotifyRecipients.Split(separatingStrings, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        }

		private async Task SetImportsNotified()
		{
			try
			{
				await apiConnection.SendQueryAsync<ReturnId>(ReportQueries.setImportsNotified, new { ids = importsToNotify.ConvertAll(x => x.ControlId) });
			}
			catch (Exception exception)
			{
				Log.WriteError("Import Change Notifier", $"Could not mark imports as notified.", exception);
			}
		}
	}
}

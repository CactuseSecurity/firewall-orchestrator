using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Mail;
using FWO.Report;
using FWO.Report.Filter;
using Newtonsoft.Json;
using System.Text.Json.Serialization; 
using WkHtmlToPdfDotNet;

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

            [JsonProperty("stop_time"), JsonPropertyName("stop_time")]
            public DateTime StopTime { get; set; }
        };
        private List<ImportToNotify> importsToNotify = new();


        /// <summary>
        /// Constructor for Import Change Notifier
        /// </summary>
        public ImportChangeNotifier(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Run the Import Change Notifier
        /// </summary>
        public async Task<bool> Run()
        {
            try
            {
                if(await NewImportFound())
                {
                    if(globalConfig.ImpChangeNotifyType != (int)ImpChangeNotificationType.SimpleText)
                    {
                        await GenerateChangeReport();
                    }
                    await SendEmail();
                    await SetImportsNotified();
                }
            }
            catch(Exception exception)
            {
                Log.WriteError("Import Change Notification", $"Runs into exception: ", exception);
                return false;
            }
            return true;
        }

        private async Task<bool> NewImportFound()
        {
            importsToNotify = await apiConnection.SendQueryAsync<List<ImportToNotify>>(ReportQueries.getImportsToNotify);
            return importsToNotify.Count > 0;
        }

        private async Task GenerateChangeReport()
        {
            try
            {
                CancellationToken token = new();
                UserConfig userConfig = new(globalConfig);

                changeReport = ReportBase.ConstructReport(new ReportTemplate("", await SetFilters()), userConfig);

                Management[] managements = Array.Empty<Management>();

                await changeReport.Generate(int.MaxValue, apiConnection,
                managementsReportIntermediate =>
                {
                    managements = managementsReportIntermediate;
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
            List<int> selectedManagements = new();
            foreach(var imp in importsToNotify)
            {
                selectedManagements.Add(imp.MgmtId);
            }
            DeviceFilter deviceFilter = new()
            {
                Managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement)
            };
            deviceFilter.Managements = deviceFilter.Managements.Where(x => selectedManagements.Contains(x.Id)).ToList();
            deviceFilter.applyFullDeviceSelection(true);

            return new((int)ReportType.Changes, deviceFilter)
            {
                TimeFilter = new()
                {
                    TimeRangeType = TimeRangeType.Fixeddates,
                    StartTime = importsToNotify.First().StopTime,
                    EndTime = importsToNotify.Last().StopTime
                }
            };
        }

        private async Task SendEmail()
        {
            EmailConnection emailConnection = new EmailConnection(globalConfig.EmailServerAddress, globalConfig.EmailPort,
                globalConfig.EmailTls, globalConfig.EmailUser, globalConfig.EmailPassword, globalConfig.EmailSenderAddress);
            MailKitMailer mailer = new MailKitMailer(emailConnection);
            await mailer.SendAsync(PrepareEmail(), emailConnection, new CancellationToken(),
                globalConfig.ImpChangeNotifyType == (int)ImpChangeNotificationType.HtmlInBody);
        }

        private MailData PrepareEmail()
        {
            string subject = globalConfig.ImpChangeNotifySubject;
            string body = globalConfig.ImpChangeNotifyBody;
            FormFile? attachment = null;
            if(changeReport != null)
            {
                switch(globalConfig.ImpChangeNotifyType)
                {
                    case (int)ImpChangeNotificationType.HtmlInBody:
                        body += changeReport?.ExportToHtml();
                        break;
                    case (int)ImpChangeNotificationType.PdfAsAttachment:
                        attachment = CreateAttachment(Convert.ToBase64String(changeReport?.ToPdf(PaperKind.A4) ?? throw new Exception("No Pdf generated.")));
                        break;
                    case (int)ImpChangeNotificationType.HtmlAsAttachment:
                        attachment = CreateAttachment(changeReport?.ExportToHtml());
                        break;
                    case (int)ImpChangeNotificationType.CsvAsAttachment:
                        attachment = CreateAttachment(changeReport?.ExportToCsv());
                        break;
                    case (int)ImpChangeNotificationType.JsonAsAttachment:
                        attachment = CreateAttachment(changeReport?.ExportToJson());
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

        private static FormFile? CreateAttachment(string? content)
        {
            if(content != null)
            {                
                MemoryStream memoryStream = new(System.Text.Encoding.UTF8.GetBytes(content));
                long baseStreamOffset = 0;
                long length = memoryStream.Length;
                string name = "x";
                string fileName = "y";
                return new(memoryStream, baseStreamOffset, length, name, fileName);
            }
            return null;
        }

        private List<string> CollectRecipients()
        {
            string[] separatingStrings = { ",", ";", "|" };
            return globalConfig.ImpChangeNotifyRecipients.Split(separatingStrings, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private async Task SetImportsNotified()
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnId>(ReportQueries.setImportsNotified, importsToNotify.ConvertAll(x => x.ControlId));
            }
            catch (Exception exception)
            {
                Log.WriteError("Import Change Notifier", $"Could not mark imports as notified.", exception);
            }
        }
    }
}

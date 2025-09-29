using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Report;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Mail;
using FWO.Encryption;
using FWO.Report;
using FWO.Services;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using System.Text;

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
        private const string LogMessageTitle = "Import Change Notifier";


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
        public async Task Run()
        {
            try
            {
                if (!WorkInProgress)
                {
                    WorkInProgress = true;
                    if (await NewImportFound())
                    {
                        if (globalConfig.ImpChangeNotifyType != (int)NotificationLayout.SimpleText)
                        {
                            await GenerateChangeReport();
                        }
                        await SendEmail();
                        await SetImportsNotified();
                    }
                    WorkInProgress = false;
                }
            }
            catch (Exception)
            {
                WorkInProgress = false;
                throw;
            }
        }

        private async Task<bool> NewImportFound()
        {
            importsToNotify = await apiConnection.SendQueryAsync<List<ImportToNotify>>(ReportQueries.getImportsToNotify);
            importedManagements = [];
            foreach (var impMgt in importsToNotify.Select(i => i.MgmtId).Where(m => !importedManagements.Contains(m)))
            {
                importedManagements.Add(impMgt);
            }
            return importsToNotify.Count > 0;
        }

        private async Task GenerateChangeReport()
        {
            try
            {
                changeReport = await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", await SetFilters()), apiConnection, userConfig, DefaultInit.DoNothing);
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Report generation leads to exception.", exception);
            }
        }

        private async Task<ReportParams> SetFilters()
        {
            deviceFilter.Managements = [.. (await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement)).Where(x => importedManagements.Contains(x.Id))];
            deviceFilter.ApplyFullDeviceSelection(true);

            return new((int)ReportType.Changes, deviceFilter)
            {
                TimeFilter = new()
                {
                    TimeRangeType = TimeRangeType.Fixeddates,
                    StartTime = importsToNotify[0].StopTime,
                    EndTime = importsToNotify[^1].StopTime.AddSeconds(1)
                }
            };
        }

        private async Task SendEmail()
        {
            string decryptedSecret = AesEnc.TryDecrypt(globalConfig.EmailPassword, false, LogMessageTitle, "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(globalConfig.EmailServerAddress, globalConfig.EmailPort,
                globalConfig.EmailTls, globalConfig.EmailUser, decryptedSecret, globalConfig.EmailSenderAddress);

            MailData? mail = await PrepareEmail();

            await MailKitMailer.SendAsync(mail, emailConnection,
                globalConfig.ImpChangeNotifyType == (int)NotificationLayout.HtmlInBody, new());
        }

        private async Task<MailData> PrepareEmail()
        {
            string subject = globalConfig.ImpChangeNotifySubject;
            string body = CreateBody();
            FormFile? attachment = null;
            if (changeReport != null)
            {
                switch (globalConfig.ImpChangeNotifyType)
                {
                    case (int)NotificationLayout.HtmlInBody:
                        body += changeReport.ExportToHtml();
                        break;
                    case (int)NotificationLayout.PdfAsAttachment:
                        string html = changeReport.ExportToHtml();
                        string? pdfData = await changeReport.ToPdf(html);

                        if (string.IsNullOrWhiteSpace(pdfData))
                            throw new ProcessingFailedException("No Pdf generated.");

                        attachment = CreateAttachment(pdfData, GlobalConst.kPdf);
                        break;
                    case (int)NotificationLayout.HtmlAsAttachment:
                        attachment = CreateAttachment(changeReport.ExportToHtml(), GlobalConst.kHtml);
                        break;
                    case (int)NotificationLayout.CsvAsAttachment:
                        attachment = CreateAttachment(changeReport.ExportToCsv(), GlobalConst.kCsv);
                        break;
                    case (int)NotificationLayout.JsonAsAttachment:
                        attachment = CreateAttachment(changeReport.ExportToJson(), GlobalConst.kJson);
                        break;
                    default:
                        break;
                }
            }
            MailData mailData = new(CollectRecipients(), subject) { Body = body };
            if (attachment != null)
            {
                mailData.Attachments = new FormFileCollection() { attachment };
            }
            return mailData;
        }

        private string CreateBody()
        {
            StringBuilder body = new(globalConfig.ImpChangeNotifyBody);
            foreach (var mgmtId in importedManagements)
            {
                int mgmtCounter = 0;
                foreach (var imp in importsToNotify.Where(x => x.MgmtId == mgmtId))
                {
                    mgmtCounter += imp.RelevantChanges;
                }
                body.Append(globalConfig.ImpChangeNotifyType == (int)NotificationLayout.HtmlInBody ? "<br>" : "\r\n\r\n");
                body.Append($"{importsToNotify.FirstOrDefault(x => x.MgmtId == mgmtId).Mgmt.MgmtName} (id={mgmtId}): {mgmtCounter} {userConfig.GetText("changes")}");
            }
            return body.ToString();
        }

        private FormFile? CreateAttachment(string? content, string fileFormat)
        {
            if (content != null)
            {
                string fileName = $"{Regex.Replace(globalConfig.ImpChangeNotifySubject, @"\s", "")}_{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssK")}.{fileFormat}";

                MemoryStream memoryStream;
                string contentType;

                if (fileFormat == GlobalConst.kPdf)
                {
                    memoryStream = new(Convert.FromBase64String(content));
                    contentType = "application/octet-stream";
                }
                else
                {
                    memoryStream = new(System.Text.Encoding.UTF8.GetBytes(content));
                    contentType = $"application/{fileFormat}";
                }

                return new(memoryStream, 0, memoryStream.Length, "FWO-Report-Attachment", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = contentType
                };
            }
            return null;
        }

        private List<string> CollectRecipients()
        {
            if (globalConfig.UseDummyEmailAddress)
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
                Log.WriteError(LogMessageTitle, $"Could not mark imports as notified.", exception);
            }
        }
    }
}

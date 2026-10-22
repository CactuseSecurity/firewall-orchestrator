using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.EventMediator.Events;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.Labelling;
using FWO.Services.Modelling;
using Microsoft.AspNetCore.Components.Forms;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace FWO.Ui.Services
{
    public class FileUploadService
    {
        /// <summary>
        /// Uploaded data as bytes.
        /// </summary>
        private byte[] UploadedData { get; set; } = [];

        private UserConfig UserConfig { get; set; }
        private ApiConnection ApiConnection { get; set; }
        private MiddlewareClient MiddlewareClient { get; set; }

        private readonly ModellingNamingConvention NamingConvention = new();
        private readonly List<AppServerType> AppServerTypes = [];
        private string ImportSource = "";
        private readonly string AllowedFileFormats;

        private readonly IEventMediator EventMediator;
        private readonly FileUploadEvent CustomLogoUploadEvent;
        private readonly FileUploadEvent FileUploadEvent;
        private readonly AppServerImportEvent AppServerImportEvent;
        private readonly FileUploadEvent MatrixImportEvent;
        private const int kFixedColumnCount = 4;

        public FileUploadService(ApiConnection apiConnection, UserConfig userConfig, MiddlewareClient middlewareClient, string allowedFileFormats, IEventMediator eventMediator)
        {
            UserConfig = userConfig;
            ApiConnection = apiConnection;
            MiddlewareClient = middlewareClient;
            NamingConvention = ModellingNamingConvention.FromJson(userConfig.ModNamingConvention);
            AppServerTypes = JsonSerializer.Deserialize<List<AppServerType>>(UserConfig.ModAppServerTypes) ?? [];
            AllowedFileFormats = allowedFileFormats;
            EventMediator = eventMediator;
            CustomLogoUploadEvent = new();
            FileUploadEvent = new();
            AppServerImportEvent = new();
            MatrixImportEvent = new();
        }

        public async Task<FileUploadEventArgs> ReadFileToBytes(InputFileChangeEventArgs args)
        {
            try
            {
                string fileExtension = Path.GetExtension(args.File.Name);

                if (!AllowedFileFormats.Contains(fileExtension))
                {
                    throw new ArgumentException(UserConfig.GetText("E5430"));
                }

                if (args.File.Size > GlobalConst.MaxUploadFileSize)
                {
                    throw new ArgumentException(UserConfig.GetText("E5431"));
                }

                using MemoryStream ms = new();

                await args.File.OpenReadStream(GlobalConst.MaxUploadFileSize).CopyToAsync(ms);
                UploadedData = ms.ToArray();

                FileUploadEvent.EventArgs!.Success = true;
            }
            catch (Exception ex)
            {
                FileUploadEvent.EventArgs!.Success = false;

                FileUploadEvent.EventArgs.Error = new()
                {
                    Message = ex.Message,
                    InternalException = ex,
                    MessageType = MessageType.Error
                };
            }
            finally
            {
                EventMediator.Publish(nameof(ReadFileToBytes), FileUploadEvent);
            }

            return FileUploadEvent.EventArgs;
        }

        public FileUploadEventArgs ImportCustomLogo()
        {
            try
            {
                string base64Data = Convert.ToBase64String(UploadedData);

                CustomLogoUploadEvent.EventArgs!.Success = true;
                CustomLogoUploadEvent.EventArgs.Data = base64Data;
            }
            catch (Exception ex)
            {
                CustomLogoUploadEvent.EventArgs!.Success = false;

                CustomLogoUploadEvent.EventArgs!.Error = new()
                {
                    Message = UserConfig.GetText("file_upload_failed"),
                    InternalException = ex,
                    MessageType = MessageType.Error
                };
            }
            finally
            {
                EventMediator.Publish(nameof(ImportCustomLogo), CustomLogoUploadEvent);
            }

            return CustomLogoUploadEvent.EventArgs;
        }

        public async Task ImportAppServersFromCSV(string filename = "")
        {
            ImportSource = GlobalConst.kCSV_ + filename;

            List<string> success = [];
            List<CSVFileUploadErrorModel> errors = [];

            string text = System.Text.Encoding.UTF8.GetString(UploadedData);
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Dictionary<int, string> labelColumns = [];
            List<(long appServerId, int ownerId, CSVAppServerImportModel appServer)> uploadedAppServers = [];

            foreach (string line in lines)
            {
                CSVFileUploadErrorModel? error = new()
                {
                    EntryData = line,
                    MessageType = MessageType.Error,
                };

                if (!TryGetEntries(line, ';', out string[] entries) && !TryGetEntries(line, ',', out entries))
                {
                    error.Message = UserConfig.GetText("E5422");
                    errors.Add(error);

                    continue;
                }

                if (IsHeader(entries))
                {
                    labelColumns = FindLabelColumns(entries, line, errors);
                    continue;
                }

                string ipString = entries[3];

                if (!ipString.TryParseIPString<(string, string)>(out (string Start, string End) ipRange, strictv4Parse: true))
                {
                    error.Message = UserConfig.GetText("E5423");
                    errors.Add(error);

                    continue;
                }

                CSVAppServerImportModel importAppServer = new()
                {
                    AppIPRangeStart = ipRange.Start,
                    AppIPRangeEnd = ipRange.End,
                    AppID = entries[1],
                    AppServerTyp = entries[2],
                    Labels = CsvLabelColumns.GetLabels(entries, labelColumns)
                };

                importAppServer.AppServerName = UserConfig.DnsLookup ?
                    await AppServerHelper.ConstructAppServerNameFromDns(importAppServer.ToModellingAppServer(), NamingConvention, UserConfig.OverwriteExistingNames) :
                    entries[0];

                // write to db
                (bool importSuccess, Exception? e, long? appServerId, int ownerId) = await AddAppServerToDb(importAppServer);

                if (!importSuccess && e is not null)
                {
                    error.Message = e.Message;
                    errors.Add(error);
                }
                else
                {
                    success.Add(line);
                    if (appServerId != null)
                    {
                        uploadedAppServers.Add((appServerId.Value, ownerId, importAppServer));
                    }
                }
            }
            await ImportLabels(uploadedAppServers, errors);

            if (AppServerImportEvent.EventArgs is not null)
            {
                success = [.. success.Distinct()];

                AppServerImportEvent.EventArgs.Success = success.Count > 0;
                AppServerImportEvent.EventArgs.Appserver = success;
                AppServerImportEvent.EventArgs.Errors = errors;

                EventMediator.Publish(nameof(ImportAppServersFromCSV), AppServerImportEvent);
            }
        }

        public async Task ImportMatrix(string filename = "")
        {
            string data = System.Text.Encoding.UTF8.GetString(UploadedData);
            ImportMatrixParameters importParams = new() { FileName = filename, Data = data, UserName = UserConfig.User.Name, UserDn = UserConfig.User.Dn };
            RestResponse<string> middlewareServerResponse = await MiddlewareClient.ImportMatrix(importParams);
            if (MatrixImportEvent.EventArgs is not null)
            {
                if (middlewareServerResponse.StatusCode != HttpStatusCode.OK)
                {
                    MatrixImportEvent.EventArgs.Success = false;
                    MatrixImportEvent.EventArgs.Data = GetResponseMessage(middlewareServerResponse);
                }
                else
                {
                    MatrixImportEvent.EventArgs.Success = middlewareServerResponse.Data?.StartsWith("Ok") ?? false;
                    MatrixImportEvent.EventArgs.Data = middlewareServerResponse.Data;
                }
                EventMediator.Publish(nameof(ImportMatrix), MatrixImportEvent);
            }
        }

        /// <summary>
        /// Extracts a user-facing message from a middleware response.
        /// </summary>
        private string? GetResponseMessage(RestResponse<string> response)
        {
            if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
            {
                return response.ErrorMessage;
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<string>(response.Content);
            }
            catch (JsonException)
            {
                return UserConfig.GetText("file_upload_failed");
            }
        }

        private static bool TryGetEntries(string line, char separator, out string[] entries)
        {
            if (line.StartsWith('\n'))
                line = line[1..];

            entries = line.Split(separator);

            if (entries.Length < 4)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = entries[i].Trim('"');
            }

            return true;
        }

        /// <summary>
        /// Writes the labels of the uploaded app servers: the labels of the optional label columns and the App label of
        /// their owners, with one label change event for the whole upload. Rejected labels are reported as warnings.
        /// </summary>
        private async Task ImportLabels(List<(long appServerId, int ownerId, CSVAppServerImportModel appServer)> uploadedAppServers, List<CSVFileUploadErrorModel> errors)
        {
            AppServerLabelImport labelImport = new(ApiConnection, () => Task.FromResult<long?>(null), true, (_, _, description) =>
            {
                errors.Add(new() { EntryData = description, Message = description, MessageType = MessageType.Warning });
                return Task.CompletedTask;
            }, UserConfig.AppLabelKey);
            List<(long appServerId, int ownerId, CSVAppServerImportModel appServer)> withLabels = [.. uploadedAppServers.Where(uploaded => uploaded.appServer.Labels != null)];
            await labelImport.LoadActiveLabels(withLabels.Select(uploaded => uploaded.appServerId));
            foreach ((long appServerId, _, CSVAppServerImportModel appServer) in withLabels)
            {
                await labelImport.SyncLabels(appServerId, appServer.AppServerName ?? "", appServer.Labels);
            }
            foreach ((int ownerId, string appId) in uploadedAppServers.Select(uploaded => (uploaded.ownerId, uploaded.appServer.AppID ?? "")).Distinct())
            {
                await labelImport.SyncOwnerAppLabels(ownerId, appId);
            }
        }

        /// <summary>
        /// Reads the optional label columns of the header and warns about other optional columns.
        /// </summary>
        private Dictionary<int, string> FindLabelColumns(string[] header, string line, List<CSVFileUploadErrorModel> errors)
        {
            Dictionary<int, string> labelColumns = CsvLabelColumns.Find(header, kFixedColumnCount, out List<string> unknownColumns);
            if (unknownColumns.Count > 0)
            {
                errors.Add(new() { EntryData = line, Message = $"{UserConfig.GetText("E5424")}: {string.Join(", ", unknownColumns)}", MessageType = MessageType.Warning });
            }
            return labelColumns;
        }

        private static bool IsHeader(string[] columns)
        {
            return columns.Length >= kFixedColumnCount
                   && columns[0].Trim('"').Trim() == "App-Server-Name"
                   && columns[1].Trim('"').Trim() == "External-App-ID"
                   && columns[2].Trim('"').Trim() == "App-Server-Typ"
                   && columns[3].Trim('"').Trim() == "App-IP-Address-Range";
        }

        private async Task<(bool success, Exception? exception, long? appServerId, int ownerId)> AddAppServerToDb(CSVAppServerImportModel importAppServer)
        {
            try
            {
                AppServerType? appServerType = AppServerTypes.FirstOrDefault(_ => _.Name == importAppServer.AppServerTyp);
                if (appServerType is null)
                {
                    return (false, new Exception($"{UserConfig.GetText("owner_appservertype_notfound")} At: {importAppServer.AppServerName}/{importAppServer.AppID}"), null, 0);
                }

                List<OwnerIdModel> ownerIds = await ApiConnection.SendQueryAsync<List<OwnerIdModel>>(OwnerQueries.getOwnerId, new { externalAppId = importAppServer.AppID });
                if (ownerIds is null || ownerIds.Count == 0)
                {
                    return (false, new Exception($"{UserConfig.GetText("owner_appserver_notfound")} At: {importAppServer.AppServerName}/{importAppServer.AppID}"), null, 0);
                }

                int ownerId = ownerIds.First().Id;
                long? appServerId = (await AppServerHelper.UpsertAppServer(ApiConnection, UserConfig,
                            new(importAppServer.ToModellingAppServer()) { ImportSource = ImportSource, AppId = ownerId, CustomType = appServerType.Id },
                            !UserConfig.DnsLookup, assignAppLabel: false)).Item1;
                return (appServerId != null, default, appServerId, ownerId);
            }
            catch (Exception exception)
            {
                return (false, exception, null, 0);
            }
        }
    }
}

using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Ui.Data;
using Microsoft.AspNetCore.Components.Forms;
using NetTools;
using Org.BouncyCastle.Utilities;
using System.Text.Json;

namespace FWO.Ui.Services
{
    public class FileUploadService
    {
        /// <summary>
        /// Uploaded data as bytes.
        /// </summary>
        private byte[]? UploadedData { get; set; }
        /// <summary>
        /// Errors that occured while trying to write file data in database.
        /// </summary>
        private List<Exception> importErrors { get; set; }

        private UserConfig userConfig { get; set; }
        private ApiConnection apiConnection { get; set; }

        public FileUploadService(IServiceProvider services)
        {
            userConfig = services.GetRequiredService<UserConfig>();
            apiConnection = services.GetRequiredService<ApiConnection>();

            importErrors = new List<Exception>();
        }

        public async Task ReadFileToBytes(InputFileChangeEventArgs args)
        {
            using MemoryStream ms = new MemoryStream();
            await args.File.OpenReadStream().CopyToAsync(ms);
            UploadedData = ms.ToArray();
        }

        public async Task<List<Exception>> ImportUploadedData(FileUploadCase fileUploadCase)
        {
            importErrors.Clear();

            if (fileUploadCase == FileUploadCase.ImportAppServerFromCSV)
            {
                await ImportAppServerFromCSV(importErrors);
            }

            return importErrors;
        }

        public async Task ImportAppServerFromCSV(List<Exception> importErrors)
        {
            string text = System.Text.Encoding.UTF8.GetString(UploadedData);
            string[] lines = text.Split('\r');

            foreach (string tmpLine in lines)
            {
                string line = tmpLine;

                string[]? entries;

                // create import model

                if (IsHeader(line))
                    continue;

                if (!TryGetEntries(line, ';', out entries) && !TryGetEntries(line, ',', out entries))
                    continue;

                CSVAppServerImportModel appServer = new()
                {
                    AppServerName = entries[0],
                    AppID = entries[1],
                    AppServerTyp = entries[2],
                    AppIPRangeStart = entries[3]
                };

                // get IP range

                if (appServer.AppIPRangeStart.TryGetNetmask(out string netmask))
                {
                    (string Start, string End) ip = appServer.AppIPRangeStart.CidrToRangeString();
                    appServer.AppIPRangeStart = ip.Start;
                    appServer.AppIPRangeEnd = ip.End;
                }
                else if (appServer.AppIPRangeStart.TrySplit('-', 1, out string ipEnd) && IPAddressRange.TryParse(appServer.AppIPRangeStart, out IPAddressRange ipRange))
                {
                    appServer.AppIPRangeStart = ipRange.Begin.ToString();
                    appServer.AppIPRangeEnd = ipRange.End.ToString();
                }
                else
                {
                    appServer.AppIPRangeEnd = appServer.AppIPRangeStart;
                }

                // write to db

                (bool importSuccess, Exception? error) = await AddAppServerToDb(appServer);

                if (!importSuccess && error is not null)
                    importErrors.Add(error);
            }
        }

        private bool TryGetEntries(string line, char separator, out string[]? entries)
        {
            entries = null;

            if (line.StartsWith("\n"))
                line = line.Remove(0, 1);

            entries = line.Split(separator);

            if (entries.Length < 3)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim('"');
                entries[i] = entry;
            }

            return true;
        }

        private bool IsHeader(string lineText)
        {
            bool splitOnSemicolon = lineText.TrySplit(';', out int splitLength);

            string[] columns;

            if (!splitOnSemicolon)
            {
                bool splitOnComma = lineText.TrySplit(',', out splitLength);

                if (!splitOnComma)
                    return false;

                columns = lineText.Split(',');
            }
            else
            {
                columns = lineText.Split(';');
            }

            return (columns.Length == 4
                   && columns[0].Trim('"') == "App-Server-Name"
                   && columns[1].Trim('"') == "External-App-ID"
                   && columns[2].Trim('"') == "App-Server-Typ"
                   && columns[3].Trim('"') == "App-IP-Address-Range");

        }

        private async Task<(bool, Exception?)> AddAppServerToDb(CSVAppServerImportModel appServer)
        {
            try
            {
                var appServerTypes = JsonSerializer.Deserialize<List<AppServerType>>(userConfig.ModAppServerTypes) ?? new();
                AppServerType? appServerType = appServerTypes.FirstOrDefault(_ => _.Name == appServer.AppServerTyp);

                if (appServerType is null)
                {
                    return new(false, new Exception($"{userConfig.GetText("owner_appservertype_notfound")} At: {appServer.AppServerName}/{appServer.AppID}"));
                }

                List<OwnerIdModel> ownerIds = await apiConnection.SendQueryAsync<List<OwnerIdModel>>(OwnerQueries.getOwnerId, new { externalAppId = appServer.AppID });

                if (ownerIds is null || !ownerIds.Any())
                {
                    return new(false, new Exception($"{userConfig.GetText("owner_appserver_notfound")} At: {appServer.AppServerName}/{appServer.AppID}"));
                }

                var Variables = new
                {
                    name = appServer.AppServerName,
                    appId = ownerIds.First().Id,
                    ip = appServer.AppIPRangeStart,
                    ipEnd = appServer.AppIPRangeEnd,
                    importSource = GlobalConst.kManual,
                    customType = appServerType.Id
                };

                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppServer, Variables)).ReturnIds;
            }
            catch (Exception exception)
            {
                //if IP already exists, skip displaying error message
                if (exception.Message.Contains("Uniqueness violation"))
                    return (true, exception);

                return (false, exception);
            }

            return (true, default);
        }
    }
}
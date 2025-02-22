using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Basics;
using FWO.Services;
using FWO.Config.Api;
using FWO.Ui.Data;
using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json;

namespace FWO.Ui.Services
{
    public class FileUploadService
    {
        /// <summary>
        /// Uploaded data as bytes.
        /// </summary>
        private byte[] UploadedData { get; set; } = [];

        /// <summary>
        /// Errors that occured while trying to write file data in database.
        /// </summary>
        private List<Exception> ImportErrors { get; set; } = [];

        private UserConfig UserConfig { get; set; }
        private ApiConnection ApiConnection { get; set; }
        private readonly ModellingNamingConvention NamingConvention = new();
        private readonly List<AppServerType> AppServerTypes = [];
        private string ImportSource = "";

        public FileUploadService(ApiConnection apiConnection, UserConfig userConfig)
        {
            UserConfig = userConfig;
            ApiConnection = apiConnection;
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            AppServerTypes = JsonSerializer.Deserialize<List<AppServerType>>(UserConfig.ModAppServerTypes) ?? [];
        }

        public async Task ReadFileToBytes(InputFileChangeEventArgs args)
        {
            using MemoryStream ms = new();
            await args.File.OpenReadStream().CopyToAsync(ms);
            UploadedData = ms.ToArray();
        }

        public async Task<List<Exception>> ImportUploadedData(FileUploadCase fileUploadCase, string filename = "")
        {
            ImportErrors.Clear();

            if (fileUploadCase == FileUploadCase.ImportAppServerFromCSV)
            {
                ImportSource = GlobalConst.kCSV_ + filename;
                await ImportAppServersFromCSV();
            }

            return ImportErrors;
        }

        private async Task ImportAppServersFromCSV()
        {
            string text = System.Text.Encoding.UTF8.GetString(UploadedData);
            string[] lines = text.Split('\r');

            foreach (string line in lines)
            {
                // create import model
                if (!TryGetEntries(line, ';', out string[] entries) && !TryGetEntries(line, ',', out entries))
                    continue;

                if (IsHeader(entries))
                    continue;

                CSVAppServerImportModel importAppServer = new(entries[3])
                {
                    AppID = entries[1],
                    AppServerTyp = entries[2]
                };
                importAppServer.AppServerName = UserConfig.DnsLookup ? 
                    await AppServerHelper.ConstructAppServerNameFromDns(importAppServer.ToModellingAppServer(), NamingConvention, UserConfig.OverwriteExistingNames) :
                    entries[0];
                
                // write to db
                (bool importSuccess, Exception? error) = await AddAppServerToDb(importAppServer);

                if (!importSuccess && error is not null)
                    ImportErrors.Add(error);
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

        private static bool IsHeader(string[] columns)
        {
            return columns.Length == 4
                   && columns[0].Trim('"').Trim() == "App-Server-Name"
                   && columns[1].Trim('"').Trim() == "External-App-ID"
                   && columns[2].Trim('"').Trim() == "App-Server-Typ"
                   && columns[3].Trim('"').Trim() == "App-IP-Address-Range";
        }

        private async Task<(bool, Exception?)> AddAppServerToDb(CSVAppServerImportModel importAppServer)
        {
            try
            {
                AppServerType? appServerType = AppServerTypes.FirstOrDefault(_ => _.Name == importAppServer.AppServerTyp);
                if (appServerType is null)
                {
                    return new(false, new Exception($"{UserConfig.GetText("owner_appservertype_notfound")} At: {importAppServer.AppServerName}/{importAppServer.AppID}"));
                }

                List<OwnerIdModel> ownerIds = await ApiConnection.SendQueryAsync<List<OwnerIdModel>>(OwnerQueries.getOwnerId, new { externalAppId = importAppServer.AppID });
                if (ownerIds is null || ownerIds.Count == 0)
                {
                    return new(false, new Exception($"{UserConfig.GetText("owner_appserver_notfound")} At: {importAppServer.AppServerName}/{importAppServer.AppID}"));
                }

                return ((await AppServerHelper.UpsertAppServer(ApiConnection, UserConfig,
                            new(importAppServer.ToModellingAppServer()){ ImportSource = ImportSource, AppId = ownerIds.First().Id, CustomType = appServerType.Id},
                            !UserConfig.DnsLookup
                    )).Item1 != null, default);
            }
            catch (Exception exception)
            {
                return (false, exception);
            }
        }
    }
}

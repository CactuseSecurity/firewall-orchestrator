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

        // public FileUploadService(IServiceProvider services)
        // {
        //     UserConfig = services.GetRequiredService<UserConfig>();
        //     ApiConnection = services.GetRequiredService<ApiConnection>();
        // }

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

        public async Task<List<Exception>> ImportUploadedData(FileUploadCase fileUploadCase)
        {
            ImportErrors.Clear();

            if (fileUploadCase == FileUploadCase.ImportAppServerFromCSV)
            {
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

                CSVAppServerImportModel appServer = new(entries[3])
                {
                    AppID = entries[1],
                    AppServerTyp = entries[2]
                };
                appServer.AppServerName = UserConfig.DnsLookup ? 
                    await AppServerHelper.ConstructAppServerNameFromDns(appServer.ToModellingAppServer(), NamingConvention, UserConfig.OverwriteExistingNames) :
                    entries[0];
                
                // write to db
                (bool importSuccess, Exception? error) = await AddAppServerToDb(appServer);

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
                   && columns[0].Trim('"') == "App-Server-Name"
                   && columns[1].Trim('"') == "External-App-ID"
                   && columns[2].Trim('"') == "App-Server-Typ"
                   && columns[3].Trim('"') == "App-IP-Address-Range";
        }

        private async Task<(bool, Exception?)> AddAppServerToDb(CSVAppServerImportModel appServer)
        {
            try
            {
                AppServerType? appServerType = AppServerTypes.FirstOrDefault(_ => _.Name == appServer.AppServerTyp);
                if (appServerType is null)
                {
                    return new(false, new Exception($"{UserConfig.GetText("owner_appservertype_notfound")} At: {appServer.AppServerName}/{appServer.AppID}"));
                }

                List<OwnerIdModel> ownerIds = await ApiConnection.SendQueryAsync<List<OwnerIdModel>>(OwnerQueries.getOwnerId, new { externalAppId = appServer.AppID });
                if (ownerIds is null || ownerIds.Count == 0)
                {
                    return new(false, new Exception($"{UserConfig.GetText("owner_appserver_notfound")} At: {appServer.AppServerName}/{appServer.AppID}"));
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppServer, Variables)).ReturnIds;

                return (returnIds != null && returnIds.Length > 0, default);
            }
            catch (Exception exception)
            {
                //if IP already exists, skip displaying error message
                if (exception.Message.Contains("Uniqueness violation"))
                    return (true, exception);

                return (false, exception);
            }
        }
    }
}

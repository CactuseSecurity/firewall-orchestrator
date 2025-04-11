using FWO.Api.Client.Queries;
using FWO.Api.Client;
using FWO.Data;
using FWO.Data.Modelling;
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

        private UserConfig UserConfig { get; set; }
        private ApiConnection ApiConnection { get; set; }
        private readonly ModellingNamingConvention NamingConvention = new();
        private readonly List<AppServerType> AppServerTypes = [];
        private string ImportSource = "";
        private readonly string AllowedFileFormats;

        public FileUploadService(ApiConnection apiConnection, UserConfig userConfig, string allowedFileFormats)
        {
            UserConfig = userConfig;
            ApiConnection = apiConnection;
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            AppServerTypes = JsonSerializer.Deserialize<List<AppServerType>>(UserConfig.ModAppServerTypes) ?? [];
            AllowedFileFormats = allowedFileFormats;
        }

        public async Task ReadFileToBytes(InputFileChangeEventArgs args)
        {
            string fileExtension = Path.GetExtension(args.File.Name);

            if(!AllowedFileFormats.Contains(fileExtension))
            {
                throw new ArgumentException(UserConfig.GetText("E5430"));
            }

            using MemoryStream ms = new();
            await args.File.OpenReadStream().CopyToAsync(ms);
            UploadedData = ms.ToArray();
        }

        public async Task<(List<string>? success, List<TError>? errors)> ImportUploadedData<TError>(FileUploadCase fileUploadCase, string filename = "") 
            where TError : ErrorBaseModel
        {
            if (fileUploadCase == FileUploadCase.ImportAppServerFromCSV)
            {
                ImportSource = GlobalConst.kCSV_ + filename;
                (List<string>? success, List<CSVFileUploadErrorModel>? errors)  =  await ImportAppServersFromCSV();

                List<TError>? importErrors = errors is not null ? [.. errors.Cast<TError>()] : default;

                return (success, importErrors);
            } 
            throw new NotImplementedException();
        }

        public async Task<(bool success, TError? error)> ImportCustomLogo<TError>(FileUploadCase fileUploadCase, string filename)
            where TError : ErrorBaseModel
        {
            if(fileUploadCase == FileUploadCase.CustomLogoUpload)
            {
                (bool success, ErrorBaseModel? error) = await SaveCustomLogo(filename);

                TError? logoUploadError = error is not null ? (TError)error : default;

                return (success, logoUploadError);
            }

            throw new NotImplementedException();
        }

        private async Task<(List<string>? success, List<CSVFileUploadErrorModel>? errors)> ImportAppServersFromCSV()
        {
            List<string> success = [];
            List<CSVFileUploadErrorModel> errors = [];

            string text = System.Text.Encoding.UTF8.GetString(UploadedData);
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string line in lines)
            {
                CSVFileUploadErrorModel? error = new()
                {
                    EntryData = line,
                    MessageType = MessageType.Error,
                };

                if(!TryGetEntries(line, ';', out string[] entries) && !TryGetEntries(line, ',', out entries))
                {
                    error.Message = UserConfig.GetText("E5422"); 
                    errors.Add(error);

                    continue;
                }

                if (IsHeader(entries))
                    continue;

                string ipString = entries[3];

                if(!ipString.TryParseIPString<(string, string)>(out (string Start, string End) ipRange, strictv4Parse: true))
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
                    AppServerTyp = entries[2]
                };

                importAppServer.AppServerName = UserConfig.DnsLookup ? 
                    await AppServerHelper.ConstructAppServerNameFromDns(importAppServer.ToModellingAppServer(), NamingConvention, UserConfig.OverwriteExistingNames) :
                    entries[0];
                
                // write to db
                (bool importSuccess, Exception? e) = await AddAppServerToDb(importAppServer);

                if (!importSuccess && e is not null)
                {
                    error.Message = e.Message;
                    errors.Add(error);
                }
                else
                {
                    success.Add(line);
                }
            }

            return (success, errors);
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

        private async Task<(bool success, ErrorBaseModel? error)> SaveCustomLogo(string filename)
        {
            string extension = Path.GetExtension(filename);
            string path = GlobalConst.CustomLogoPath + GlobalConst.CustomLogoFilename + extension;

            try
            {
                if(!Directory.Exists(GlobalConst.CustomLogoPath))
                {
                    Directory.CreateDirectory(GlobalConst.CustomLogoPath);
                }

                await File.WriteAllBytesAsync(path, UploadedData);

                return (true, default);
            }
            catch(Exception ex)
            { 
                return (false, new ErrorBaseModel() { InternalException = ex, MessageType = MessageType.Error, Message = ex.Message});
            }
        }
    }
}

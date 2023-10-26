using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Api.Client;
using FWO.Config.Api;


namespace FWO.Middleware.Server
{
    public class ImportAppData
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private string importFile { get; set; }


        public ImportAppData(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            Read();
        }

        private void Read()
        {
            try
            {
                // Read config as json from file
                importFile = System.IO.File.ReadAllText(globalConfig.ImportAppDataPath).TrimEnd();
            }
            catch (Exception fileReadException)
            {
                Log.WriteError("Read file", $"File could not be found.", fileReadException);
                throw;
            }
        }

        public async Task<bool> Run()
        {
            try
            {

            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"File could not be processed.", exc);
                return false;
            }
            Log.WriteDebug("Import App Data", "Import Ok.");
            return true;
        }
    }
}

using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using Novell.Directory.Ldap;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the App Data Import
    /// </summary>
    public partial class AppDataImport : DataImportBase
    {
        private List<ModellingImportAppData> ImportedApps = [];
        private List<FwoOwner> ExistingApps = [];
        private List<ModellingAppServer> ExistingAppServers = [];

        private Ldap internalLdap = new();

        private List<Ldap> connectedLdaps = [];
        private Dictionary<int, List<string>> rolesToSetByType = [];
        private Dictionary<int, OwnerResponsibleType> ownerResponsibleTypeById = [];
        private Dictionary<string, int> ownerResponsibleTypeIdByName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> ownerLifeCycleStateIdsByName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<int, bool> ownerLifeCycleStateActiveById = [];
        private bool hasImmediateAppDecommNotificationForImport;
        private ModellingNamingConvention NamingConvention = new();
        private UserConfig userConfig = new();
        private const string LogMessageTitle = "Import App Data";
        private const string LevelFile = "Import File";
        private const string LevelApp = "App";
        private const string LevelAppServer = "App Server";

        /// <summary>
        /// Constructor for App Data Import
        /// </summary>
        public AppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : base(apiConnection, globalConfig)
        { }
    }
}

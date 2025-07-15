using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using System.Timers;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the scheduler for the compliance check notifications
    /// </summary>
    public class ComplianceCheckScheduler : SchedulerBase
    {
        /// <summary>
        /// Async Constructor needing the connection
        /// </summary>
        public static async Task<ComplianceCheckScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ComplianceCheckScheduler(apiConnection, globalConfig);
        }

        private ComplianceCheckScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeComplianceCheckConfigChanges, SchedulerInterval.Minutes, "ComplianceCheck")
        { }

        /// <summary>
        /// set scheduling timer from config values
        /// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            if(globalConfig.ComplianceCheckSleepTime > 0)
            {
                StartScheduleTimer(globalConfig.ComplianceCheckSleepTime, globalConfig.ComplianceCheckStartAt);
            }
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
        {
            try
            {
                var userConfig = new UserConfig(globalConfig);

                userConfig.EmailServerAddress = globalConfig.EmailServerAddress;
                userConfig.EmailPort = globalConfig.EmailPort;
                userConfig.EmailTls = userConfig.EmailTls;
                userConfig.EmailUser = globalConfig.EmailUser;
                userConfig.EmailSenderAddress = globalConfig.EmailSenderAddress;
                userConfig.ComplianceCheckMailSubject = globalConfig.ComplianceCheckMailSubject;
                userConfig.ComplianceCheckMailBody = globalConfig.ComplianceCheckMailBody;
                userConfig.ComplianceCheckMailRecipients = globalConfig.ComplianceCheckMailRecipients;

                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                
                await complianceCheck.CheckAll();
            }
            catch (Exception exc)
            {
                await LogErrorsWithAlert(1, "Compliance Check", GlobalConst.kComplianceCheck, AlertCode.ComplianceCheck, exc);
            }
        }
    }
}

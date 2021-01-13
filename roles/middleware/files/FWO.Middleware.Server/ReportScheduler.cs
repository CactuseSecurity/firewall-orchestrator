using FWO.Api.Data;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Report;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace FWO.Middleware.Server
{
    public class ReportScheduler
    {
        private readonly object scheduledReportsLock = new object();
        private List<ScheduledReport> scheduledReports = new List<ScheduledReport>();

        private readonly string apiServerUri;
        private readonly ApiSubscription<ScheduledReport[]> scheduledReportsSubscription;

        private readonly JwtWriter jwtWriter;

        private readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        public ReportScheduler(APIConnection apiConnection, JwtWriter jwtWriter)
        {
            this.jwtWriter = jwtWriter;
            apiServerUri = apiConnection.APIServerURI;

            //scheduledReports = apiConnection.SendQueryAsync<ScheduledReport[]>(ReportQueries.getReportSchedules).Result.ToList();
            scheduledReportsSubscription = apiConnection.GetSubscription<ScheduledReport[]>(ApiExceptionHandler, ReportQueries.subscribeReportScheduleChanges);
            scheduledReportsSubscription.OnUpdate += OnScheduleUpdate;

            Timer checkScheduleTimer = new Timer();
            checkScheduleTimer.Elapsed += CheckSchedule;
            checkScheduleTimer.Interval = CheckScheduleInterval.TotalMilliseconds;
            checkScheduleTimer.AutoReset = true;
            checkScheduleTimer.Start();
        }

        private void OnScheduleUpdate(ScheduledReport[] scheduledReports)
        {
            lock (scheduledReportsLock)
            {
                this.scheduledReports = scheduledReports.ToList();
            }
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError("Report scheduler", "Api subscription lead to exception. Retry subscription.", exception);
            // Subscription will be restored if no exception is thrown here
        }

        private async void CheckSchedule(object _, ElapsedEventArgs __)
        {
            List<Task> reportGeneratorTasks = new List<Task>();

            DateTime dateTimeNowRounded = RoundUp(DateTime.Now, CheckScheduleInterval);

            lock (scheduledReports)
            {
                foreach (ScheduledReport scheduledReport in scheduledReports)
                {
                    try
                    {
                        if (scheduledReport.Active)
                        {
                            // Add schedule interval as long as schedule time is smaller then current time 
                            while (RoundUp(scheduledReport.StartTime, CheckScheduleInterval) < dateTimeNowRounded)
                            {
                                scheduledReport.StartTime = scheduledReport.RepeatInterval switch
                                {
                                    Interval.Days => scheduledReport.StartTime.AddDays(scheduledReport.RepeatOffset),
                                    Interval.Weeks => scheduledReport.StartTime.AddDays(scheduledReport.RepeatOffset * 7),
                                    Interval.Months => scheduledReport.StartTime.AddMonths(scheduledReport.RepeatOffset),
                                    Interval.Years => scheduledReport.StartTime.AddYears(scheduledReport.RepeatOffset),
                                    _ => throw new NotSupportedException("Time interval is not supported.")
                                };
                            }

                            if (RoundUp(scheduledReport.StartTime, CheckScheduleInterval) == dateTimeNowRounded)
                            {
                                reportGeneratorTasks.Add(Task.Run(async () =>
                                {
                                    try
                                    {
                                        ReportBase reportRules = ReportBase.ConstructReport(scheduledReport.Template.Filter);
                                        await reportRules.Generate
                                        (
                                            int.MaxValue,
                                            scheduledReport.Template.Filter,
                                            new APIConnection(apiServerUri, await jwtWriter.CreateJWT(scheduledReport.Owner)),
                                            _ => Task.CompletedTask
                                        );

                                        foreach (FileFormat format in scheduledReport.OutputFormat)
                                        {
                                            switch (format.Name)
                                            {
                                                case "csv":
                                                    break;

                                                case "html":
                                                    break;

                                                case "pdf":
                                                    break;

                                                case "json":
                                                    break;

                                                default:
                                                    throw new NotSupportedException("Output format is not supported.");
                                            }
                                        }
                                    }
                                    catch (Exception exception)
                                    {
                                        Log.WriteError("Report Scheduling", $"Generating scheduled report \"{scheduledReport.Name}\" lead to exception.", exception);
                                    }
                                }));
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.WriteError("Report Scheduling", "Checking scheduled reports lead to exception.", exception);
                    }
                }
            }

            await Task.WhenAll(reportGeneratorTasks);
        }

        private static DateTime RoundUp(DateTime dateTime, TimeSpan roundInterval)
        {
            return new DateTime((dateTime.Ticks + roundInterval.Ticks - 1) / roundInterval.Ticks * roundInterval.Ticks, dateTime.Kind);
        }
    }
}

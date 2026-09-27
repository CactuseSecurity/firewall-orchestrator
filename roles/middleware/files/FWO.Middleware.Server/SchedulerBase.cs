using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Services;
using System.Timers;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the scheduler base processing
    /// </summary>
    public abstract class SchedulerBase : IDisposable
    {
        /// <summary>
        /// API connection
        /// </summary>
        protected readonly ApiConnection apiConnection;

        /// <summary>
        /// Global config
        /// </summary>
        protected GlobalConfig globalConfig;

        /// <summary>
        /// Global config change subscription
        /// </summary>
        protected GraphQlApiSubscription<List<ConfigItem>>? ConfigDataSubscription;

        // both timers are private on purpose: every access has to go through TimerGate, and a
        // subclass reaching past StartScheduleTimer would silently break that invariant
        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer RecurringTimer = new();
        private readonly string SchedulerText;
        private readonly SchedulerInterval SchedulerInterval;
        private int SleepTime;
        private readonly object TimerGate = new();
        private bool Disposed;


        /// <summary>
        /// Constructor starting the Schedule timer
        /// </summary>
        protected SchedulerBase(ApiConnection apiConnection, GlobalConfig globalConfig, string configDataSubscription, SchedulerInterval schedulerInterval, string schedulerName)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            ConfigDataSubscription = apiConnection.GetSubscription<List<ConfigItem>>(GraphqlExceptionHandler.Handle, OnGlobalConfigChange, configDataSubscription);
            SchedulerText = "Scheduler-" + schedulerName;
            SchedulerInterval = schedulerInterval;
        }

        /// <summary>
        /// set scheduling timer from config values, to be overwritten for specific scheduler
        /// </summary>
        protected abstract void OnGlobalConfigChange(List<ConfigItem> config);

        /// <summary>
        /// define the processing to be done, to be overwritten for specific schedule
        /// </summary>
        protected abstract void Process(object? _, ElapsedEventArgs __);

        /// <summary>
        /// start the scheduling timer, to be called by specific scheduler
        /// </summary>
        protected void StartScheduleTimer(int sleepTime, DateTime startTime)
        {
            SleepTime = sleepTime;
            if (SleepTime > 0)
            {
                try
                {
                    lock (TimerGate)
                    {
                        // the whole replacement has to happen under the lock: checking the flag
                        // and then releasing it would let Dispose stop the old timer while a new
                        // one is being started, leaving a timer nothing can reach any more
                        if (Disposed)
                        {
                            return;
                        }

                        // Dispose old timer if existant
                        ScheduleTimer.Stop();
                        ScheduleTimer.Elapsed -= Process;
                        ScheduleTimer.Elapsed -= StartRecurringTimer;
                        ScheduleTimer.Dispose();

                        ScheduleTimer = new();
                        ScheduleTimer.Elapsed += Process;
                        ScheduleTimer.Elapsed += StartRecurringTimer;
                        ScheduleTimer.Interval = (CalculateStartTime(startTime) - DateTime.Now).TotalMilliseconds;
                        ScheduleTimer.AutoReset = false;
                        ScheduleTimer.Start();
                    }
                    Log.WriteInfo(SchedulerText, "ScheduleTimer started.");
                }
                catch (Exception exception)
                {
                    Log.WriteError(SchedulerText, "Could not start ScheduleTimer.", exception);
                }
            }
        }

        private void StartRecurringTimer(object? _, ElapsedEventArgs __)
        {
            try
            {
                lock (TimerGate)
                {
                    // the schedule timer may elapse while the scheduler is being disposed,
                    // which would otherwise revive a recurring timer nobody can stop any more
                    if (Disposed)
                    {
                        return;
                    }

                    // Dispose old timer if existant
                    RecurringTimer.Stop();
                    RecurringTimer.Elapsed -= Process;
                    RecurringTimer.Dispose();

                    RecurringTimer = new();
                    RecurringTimer.Elapsed += Process;
                    RecurringTimer.Interval = SleepTimeToMilliseconds();
                    RecurringTimer.AutoReset = true;
                    RecurringTimer.Start();
                }
                Log.WriteInfo(SchedulerText, "RecurringTimer started.");
            }
            catch (Exception exception)
            {
                Log.WriteError(SchedulerText, "Could not start RecurringTimer.", exception);
            }
        }

        private DateTime CalculateStartTime(DateTime startTime)
        {
            try
            {
                while (startTime < DateTime.Now)
                {
                    startTime = SchedulerInterval switch
                    {
                        SchedulerInterval.Days => startTime.AddDays(SleepTime),
                        SchedulerInterval.Hours => startTime.AddHours(SleepTime),
                        SchedulerInterval.Minutes => startTime.AddMinutes(SleepTime),
                        SchedulerInterval.Seconds => startTime.AddSeconds(SleepTime),
                        _ => throw new NotSupportedException($"Error: wrong time interval format:" + SchedulerInterval.ToString())
                    };
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(SchedulerText, "Could not calculate start time.", exception);
            }
            return startTime;
        }

        private int SleepTimeToMilliseconds()
        {
            return SchedulerInterval switch
            {
                SchedulerInterval.Days => SleepTime * GlobalConst.kDaysToMilliseconds,
                SchedulerInterval.Hours => SleepTime * GlobalConst.kHoursToMilliseconds,
                SchedulerInterval.Minutes => SleepTime * GlobalConst.kMinutesToMilliseconds,
                SchedulerInterval.Seconds => SleepTime * GlobalConst.kSecondsToMilliseconds,
                _ => throw new NotSupportedException($"Error: wrong time interval format:" + SchedulerInterval.ToString())
            };
        }

        /// <summary>
        /// Stops both timers and the config subscription. Without it the recurring timer keeps
        /// firing - and logging - for the lifetime of the process.
        /// </summary>
        /// <remarks>
        /// Does not wait for a <see cref="Process"/> callback that is already running. The timers
        /// are stopped, but Process is implemented as async void, so a tick that is already in
        /// flight runs to completion after this method returns. Callers must therefore not
        /// dispose resources Process uses - the API connection for instance - immediately after
        /// disposing the scheduler.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the timers and the config change subscription.
        /// </summary>
        /// <param name="disposing">True when called from Dispose rather than from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            lock (TimerGate)
            {
                if (Disposed)
                {
                    return;
                }
                Disposed = true;
                StopTimers();
            }

            ConfigDataSubscription?.Dispose();
            ConfigDataSubscription = null;
        }

        private void StopTimers()
        {
            ScheduleTimer.Stop();
            ScheduleTimer.Elapsed -= Process;
            ScheduleTimer.Elapsed -= StartRecurringTimer;
            ScheduleTimer.Dispose();

            RecurringTimer.Stop();
            RecurringTimer.Elapsed -= Process;
            RecurringTimer.Dispose();
        }
    }
}

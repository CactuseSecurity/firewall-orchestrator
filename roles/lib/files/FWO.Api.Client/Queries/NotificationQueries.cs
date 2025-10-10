using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class NotificationQueries : Queries
    {
        public static readonly string getNotifications;
        public static readonly string addNotification;
		public static readonly string updateNotification;
		public static readonly string updateNotificationLastSent;
        public static readonly string deleteNotification;


        static NotificationQueries()
        {
            try
            {
                getNotifications = File.ReadAllText(QueryPath + "notification/getNotifications.graphql");
                addNotification = File.ReadAllText(QueryPath + "notification/addNotification.graphql");
				updateNotification = File.ReadAllText(QueryPath + "notification/updateNotification.graphql");
				updateNotificationLastSent = File.ReadAllText(QueryPath + "notification/updateNotificationLastSent.graphql");
                deleteNotification = File.ReadAllText(QueryPath + "notification/deleteNotification.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize NotificationQueries", "Api NotificationQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}

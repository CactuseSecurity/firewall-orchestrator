using System.Reflection;

namespace FWO.Middleware.Server
{
    public static class MethodTimeLogger
    {
        public static void Log(MethodBase methodBase, TimeSpan timeSpan, string message)
        {
            string additionalInfo = $" Additional Info: {message}";
            string duration = $" Runtime: {timeSpan:mm}m {timeSpan:ss}s {timeSpan:fff}ms";

            Type type = methodBase.DeclaringType;
            Type? @interface = type!.GetInterfaces()
                .FirstOrDefault(i => type.GetInterfaceMap(i).TargetMethods.Any(m => m.DeclaringType == type));

            string info = "Executed ";
            string logs = $"{DateTime.Now} | " + info + $"{methodBase.DeclaringType!.Name}.{methodBase.Name}.{duration}";

            Logging.Log.WriteInfo("", logs);
        }
    }
}
    


using System;

namespace FWO_Auth
{
    class Program
    {
        static void Main()
        {
            try
            {
                AuthModule Server = new AuthModule();
            }
            catch (Exception exception)
            {
                // Log error
                FWO_Logging.Log.WriteError("Unhandeled unexpected exception", "Unhandeled unexpected exception caught at Programm.cs", exception);

                // Exit auth module with error
                Environment.Exit(1);
            }
        }
    }
}

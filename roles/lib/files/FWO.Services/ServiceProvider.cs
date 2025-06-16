namespace FWO.Services
{
    /// <summary>
    /// A container for service provider objects to make them accesible globally and facilitate injecting them without using constructors for that.
    /// </summary>
    public static class ServiceProvider
    {
        public static IServiceProvider? UiServices { get; set; }
    }
}

namespace FWO.Basics
{
	public struct SCConstants
	{
		// If the API is called to open a ticket for a SecureApp application with more than 100 ARs,
		// it must be split into multiple tickets of up to 100 ARs each.
		public const int SCMaxBundledTasks = 100;
		public const bool SCBundleGateways = true;
	}
}

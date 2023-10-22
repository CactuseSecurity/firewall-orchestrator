using FWO.Api.Data;

namespace FWO.Ui.Services
{
    public static class DefaultInit
    {
        public static void DoNothing(Exception? e, string t, string m, bool E) {}
        public static async Task DoNothing() { await Task.CompletedTask; }
        public static async Task DoNothing(string s) { await Task.CompletedTask; }
        public static async Task DoNothing(RequestStatefulObject s) { await Task.CompletedTask; }
        public static async Task DoNothing(RequestReqTask r) { await Task.CompletedTask; }
        public static async Task DoNothing(RequestImplTask i) {await Task.CompletedTask; }
        public static async Task DoNothing(NetworkConnection c) {await Task.CompletedTask; }
        public static async Task DoNothing(AppRole a) {await Task.CompletedTask; }
        public static async Task DoNothing(NetworkService n) {await Task.CompletedTask; }

        public static bool DoNothingSync() { return false; }
        public static bool DoNothingSync(NetworkConnection c) { return false; }
        public static bool DoNothingSync(AppRole a) { return false; }
        public static bool DoNothingSync(List<AppRole> a) { return false; }
        public static bool DoNothingSync(NetworkService s) { return false; }
        public static bool DoNothingSync(List<NetworkService> s) { return false; }
        public static bool DoNothingSync(ServiceGroup s) { return false; }
        public static bool DoNothingSync(List<ServiceGroup> s) { return false; }
        public static bool DoNothingSync(List<NetworkObject> o) { return false; }
    }
}

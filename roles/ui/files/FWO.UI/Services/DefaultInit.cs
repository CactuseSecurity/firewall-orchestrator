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
        public static bool DoNothingSync() { return false; }
    }
}

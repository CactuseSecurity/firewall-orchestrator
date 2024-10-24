using FWO.Api.Data;

namespace FWO.Services
{
    public static class DefaultInit
    {
        public static void DoNothing(Exception? e, string t, string m, bool E) {}
        public static async Task DoNothing() { await Task.CompletedTask; }
        public static async Task DoNothing(string _) { await Task.CompletedTask; }
        public static async Task DoNothing(WfStatefulObject _) { await Task.CompletedTask; }
        public static async Task DoNothing(WfReqTask _) { await Task.CompletedTask; }
        public static async Task DoNothing(WfImplTask _) {await Task.CompletedTask; }
        public static async Task DoNothing(UiUser _) {await Task.CompletedTask; }
        public static async Task DoNothing(FwoOwner _) {await Task.CompletedTask; }
        public static async Task DoNothing(Device _) {await Task.CompletedTask; }


        public static bool DoNothingSync() { return false; }
        public static bool DoNothingSync(ModellingNwGroup _) { return false; }
    }
}

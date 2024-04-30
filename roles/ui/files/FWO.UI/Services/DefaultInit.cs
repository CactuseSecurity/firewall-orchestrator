using FWO.GlobalConstants;
using FWO.Api.Data;

namespace FWO.Ui.Services
{
    public static class DefaultInit
    {
        public static void DoNothing(Exception? e, string t, string m, bool E) {}
        public static async Task DoNothing() { await Task.CompletedTask; }
        public static async Task DoNothing(string _) { await Task.CompletedTask; }
        public static async Task DoNothing(RequestStatefulObject _) { await Task.CompletedTask; }
        public static async Task DoNothing(RequestReqTask _) { await Task.CompletedTask; }
        public static async Task DoNothing(RequestImplTask _) {await Task.CompletedTask; }
        public static async Task DoNothing(UiUser _) {await Task.CompletedTask; }
        public static async Task DoNothing(FwoOwner _) {await Task.CompletedTask; }
        public static async Task DoNothing(Device _) {await Task.CompletedTask; }


        public static bool DoNothingSync() { return false; }
        public static bool DoNothingSync(ModellingNwGroup _) { return false; }
    }
}

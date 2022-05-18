namespace FWO.Ui.Services
{
    public class RequestStates
    {
        public Dictionary<int, string> Name = new Dictionary<int, string>();


        public void Init()
        {
            Name.Add(0,"Draft");
            Name.Add(9,"Requested");
            
            Name.Add(10,"ToApprove");
            Name.Add(11,"InApproval");
            Name.Add(19,"Approved");

            Name.Add(20,"ToPlan");
            Name.Add(21,"InPlanning");
            Name.Add(22,"WaitForApproval");
            Name.Add(23,"ComplianceViolation");
            Name.Add(29,"Planned");

            Name.Add(30,"ToImplement");
            Name.Add(31,"InImplementation");
            Name.Add(32,"ImplementationTrouble");
            Name.Add(39,"Implemented");

            Name.Add(40,"ToVerify");
            Name.Add(41,"InVerification");
            Name.Add(42,"FurtherWorkRequested");
            Name.Add(49,"Verified");

            Name.Add(50,"InProgress");

            Name.Add(60,"Done");
            Name.Add(61,"Rejected");
            Name.Add(62,"Discarded");
        }
    }
}

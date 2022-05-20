namespace FWO.Ui.Services
{
    public class RequestStates
    {
        public Dictionary<int, string> Name = new Dictionary<int, string>();


        public void Init()
        {
            Name.Add(0,"Draft");
            Name.Add(49,"Requested");
            
            Name.Add(50,"ToApprove");
            Name.Add(60,"InApproval");
            Name.Add(99,"Approved");

            Name.Add(100,"ToPlan");
            Name.Add(110,"InPlanning");
            Name.Add(120,"WaitForApproval");
            Name.Add(130,"ComplianceViolation");
            Name.Add(149,"Planned");

            Name.Add(200,"ToImplement");
            Name.Add(210,"InImplementation");
            Name.Add(220,"ImplementationTrouble");
            Name.Add(249,"Implemented");

            Name.Add(250,"ToVerify");
            Name.Add(260,"InVerification");
            Name.Add(270,"FurtherWorkRequested");
            Name.Add(299,"Verified");

            Name.Add(500,"InProgress");

            Name.Add(600,"Done");
            Name.Add(610,"Rejected");
            Name.Add(620,"Discarded");
        }
    }
}

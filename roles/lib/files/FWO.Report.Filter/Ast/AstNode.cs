namespace FWO.Report.Filter.Ast
{
    public abstract class AstNode
    {
        public abstract void Extract(ref DynGraphqlQuery query, ReportType? reportType);
    }
}

namespace FWO.Ui.Services
{
    /// <summary>
    /// Live feedback for the report filter input: the input split into the text before, at and after the position a
    /// filter error refers to. Created by <see cref="ReportFilterFeedbackEvaluator"/>.
    /// </summary>
    /// <param name="Start">Text before the error, or the whole input if there is no error.</param>
    /// <param name="Error">Text the error refers to; empty if there is none.</param>
    /// <param name="End">Text after the error.</param>
    /// <param name="UnexpectedError">An error other than a filter error, which is logged and not shown as feedback.</param>
    public sealed record ReportFilterFeedback(string Start, string Error, string End, Exception? UnexpectedError = null);
}

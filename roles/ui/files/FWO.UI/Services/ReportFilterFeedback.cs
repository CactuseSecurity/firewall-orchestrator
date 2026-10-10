using FWO.Logging;
using FWO.Report.Filter.Exceptions;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Live feedback for the report filter input: the input split into the text before, at and after the position a
    /// filter error refers to. Kept out of the component so it can be tested without rendering.
    /// </summary>
    /// <param name="Start">Text before the error, or the whole input if there is no error.</param>
    /// <param name="Error">Text the error refers to; empty if there is none.</param>
    /// <param name="End">Text after the error.</param>
    /// <param name="UnexpectedError">An error other than a filter error, which is logged and not shown as feedback.</param>
    public sealed record ReportFilterFeedback(string Start, string Error, string End, Exception? UnexpectedError = null)
    {
        private const string kLogTitle = "Report filter";

        /// <summary>
        /// Checks the filter and returns the feedback for it.
        /// </summary>
        /// <param name="input">The filter text entered so far.</param>
        /// <param name="validate">Checks the filter and throws a <see cref="FilterException"/> naming the position of an error.</param>
        /// <returns>
        /// The input split at the reported error. Any other failure is logged in every build configuration and
        /// the input is returned without error marker, so no internals reach the user; the filter is checked
        /// again when the report is generated.
        /// </returns>
        public static ReportFilterFeedback Evaluate(string input, Action<string> validate)
        {
            try
            {
                try
                {
                    validate(input);
                    return new ReportFilterFeedback(input, "", "");
                }
                catch (FilterException filterError)
                {
                    int errorStart = filterError.ErrorPosition.Start.Value;
                    int errorEnd = filterError.ErrorPosition.End.Value;
                    return new ReportFilterFeedback(input[..errorStart], input[errorStart..errorEnd], input[errorEnd..]);
                }
            }
            catch (Exception unexpectedError)
            {
                Log.WriteError(kLogTitle, "Unexpected error while checking the report filter.", unexpectedError);
                return new ReportFilterFeedback(input, "", "", unexpectedError);
            }
        }
    }
}

using FWO.Logging;
using FWO.Report.Filter.Exceptions;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Checks the report filter while it is typed and creates the live feedback for it. One instance belongs to one
    /// filter input, so that an unexpected error is logged once instead of on every keystroke. Kept out of the
    /// component so it can be tested without rendering.
    /// </summary>
    public sealed class ReportFilterFeedbackEvaluator
    {
        private const string kLogTitle = "Report filter";
        private string? lastLoggedError;

        /// <summary>
        /// Checks the filter and returns the feedback for it.
        /// </summary>
        /// <param name="input">The filter text entered so far.</param>
        /// <param name="validate">Checks the filter and throws a <see cref="FilterException"/> naming the position of an error.</param>
        /// <returns>
        /// The input split at the reported error. Any other failure is logged in every build configuration - once
        /// until the filter is valid again or fails differently - and the input is returned without error marker, so
        /// no internals reach the user; the filter is checked again when the report is generated.
        /// </returns>
        public ReportFilterFeedback Evaluate(string input, Action<string> validate)
        {
            try
            {
                ReportFilterFeedback feedback = Check(input, validate);
                lastLoggedError = null;
                return feedback;
            }
            catch (Exception unexpectedError)
            {
                LogOnce(unexpectedError);
                return new ReportFilterFeedback(input, "", "", unexpectedError);
            }
        }

        private static ReportFilterFeedback Check(string input, Action<string> validate)
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

        private void LogOnce(Exception unexpectedError)
        {
            string errorKey = $"{unexpectedError.GetType().FullName}: {unexpectedError.Message}";
            if (errorKey == lastLoggedError)
            {
                return;
            }
            lastLoggedError = errorKey;
            Log.WriteError(kLogTitle, "Unexpected error while checking the report filter.", unexpectedError);
        }
    }
}

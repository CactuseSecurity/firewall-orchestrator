using Microsoft.Extensions.AI;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Reads the answer text of a run of the FWO agent from its updates: the texts of the assistant, the texts of
    /// different messages separated by an empty line.
    /// </summary>
    public sealed class AgentAnswerText
    {
        /// <summary>Put between the texts of two messages of the run.</summary>
        public const string kMessageSeparator = "\n\n";

        private string? lastTextMessageId;
        private bool hasText;

        /// <summary>
        /// Returns the answer text of an update, starting with the separator if it continues the answer with another message.
        /// </summary>
        /// <param name="update">Update of the run</param>
        /// <returns>The text to append to the answer; empty if the update carries none</returns>
        public string GetDelta(ChatResponseUpdate update)
        {
            ArgumentNullException.ThrowIfNull(update);
            string text = update.Role == ChatRole.Assistant || update.Role == null ? update.Text : "";
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            bool startsOtherMessage = hasText && update.MessageId != null && lastTextMessageId != null && update.MessageId != lastTextMessageId;
            hasText = true;
            lastTextMessageId = update.MessageId ?? lastTextMessageId;
            return startsOtherMessage ? kMessageSeparator + text : text;
        }
    }
}

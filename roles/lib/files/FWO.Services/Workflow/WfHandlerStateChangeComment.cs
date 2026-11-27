using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfHandler
    {
        /// <summary>
        /// Comment or reason the user entered with the state change that is currently processed, e.g. the reason for
        /// rejecting an interface or the comment of an approval. It is offered to notifications as
        /// <c>@@STATECHANGE_COMMENT@@</c> and is empty outside of a state change.
        /// </summary>
        public string StateChangeComment { get; private set; } = "";

        /// <summary>
        /// Sets the comment of the state change that starts now. Dispose the returned scope when the state change
        /// including its actions is finished, which restores the previous comment. An empty comment keeps the
        /// comment of an enclosing state change, so that e.g. an automatic promotion triggered by a rejection still
        /// offers the rejection reason.
        /// </summary>
        /// <param name="comment">The comment entered by the user; null or empty if there is none.</param>
        /// <returns>The scope of the comment.</returns>
        public IDisposable BeginStateChangeComment(string? comment)
        {
            string previousComment = StateChangeComment;
            if (!string.IsNullOrWhiteSpace(comment))
            {
                StateChangeComment = comment;
            }
            return new StateChangeCommentScope(this, previousComment);
        }

        /// <summary>
        /// Returns the comment the user entered in the promotion dialog of the given object and removes it from the
        /// object, so that a later state change of the same object, e.g. an automatic promotion, does not offer it again.
        /// </summary>
        /// <param name="statefulObject">The object promoted by the user.</param>
        /// <returns>The comment; null if there is none.</returns>
        private static string? TakeOptComment(WfStatefulObject statefulObject)
        {
            string? comment = statefulObject.OptComment();
            statefulObject.SetOptComment(null);
            return comment;
        }

        private sealed class StateChangeCommentScope(WfHandler wfHandler, string previousComment) : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                wfHandler.StateChangeComment = previousComment;
                disposed = true;
            }
        }
    }
}

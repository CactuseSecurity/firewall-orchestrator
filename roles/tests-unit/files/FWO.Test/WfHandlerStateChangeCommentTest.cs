using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerStateChangeCommentTest
    {
        [Test]
        public void BeginStateChangeComment_SetsTheCommentUntilTheScopeEnds()
        {
            WfHandler wfHandler = new();

            using (wfHandler.BeginStateChangeComment("interface is obsolete"))
            {
                Assert.That(wfHandler.StateChangeComment, Is.EqualTo("interface is obsolete"));
            }

            Assert.That(wfHandler.StateChangeComment, Is.Empty);
        }

        [Test]
        public void BeginStateChangeComment_KeepsTheEnclosingCommentWhenNoneIsGiven()
        {
            WfHandler wfHandler = new();

            using (wfHandler.BeginStateChangeComment("rejected"))
            {
                using (wfHandler.BeginStateChangeComment(null))
                {
                    Assert.That(wfHandler.StateChangeComment, Is.EqualTo("rejected"));
                }
                using (wfHandler.BeginStateChangeComment(" "))
                {
                    Assert.That(wfHandler.StateChangeComment, Is.EqualTo("rejected"));
                }
                using (wfHandler.BeginStateChangeComment("inner"))
                {
                    Assert.That(wfHandler.StateChangeComment, Is.EqualTo("inner"));
                }
                Assert.That(wfHandler.StateChangeComment, Is.EqualTo("rejected"));
            }

            Assert.That(wfHandler.StateChangeComment, Is.Empty);
        }

        [Test]
        public void BeginStateChangeComment_DisposingTwiceDoesNotRestoreAgain()
        {
            WfHandler wfHandler = new();
            IDisposable outerScope = wfHandler.BeginStateChangeComment("outer");
            IDisposable innerScope = wfHandler.BeginStateChangeComment("inner");

            innerScope.Dispose();
            using (wfHandler.BeginStateChangeComment("second inner"))
            {
                innerScope.Dispose();
                Assert.That(wfHandler.StateChangeComment, Is.EqualTo("second inner"));
            }
            outerScope.Dispose();

            Assert.That(wfHandler.StateChangeComment, Is.Empty);
        }

        [Test]
        public void TakeOptComment_ReturnsAndRemovesThePromotionComment()
        {
            WfTicket ticket = new();
            ticket.SetOptComment("approved with conditions");
            MethodInfo takeOptComment = typeof(WfHandler).GetMethod("TakeOptComment", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "TakeOptComment");

            string? comment = (string?)takeOptComment.Invoke(null, [ticket]);

            Assert.Multiple(() =>
            {
                Assert.That(comment, Is.EqualTo("approved with conditions"));
                Assert.That(ticket.OptComment(), Is.Null);
            });
        }
    }
}

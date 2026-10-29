using FWO.Basics;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using NUnit.Framework;
using System.Runtime.ExceptionServices;
using System.Text;

namespace FWO.Test
{
    /// <summary>
    /// Covers the limits that keep deeply nested or very long report filters from overflowing the stack.
    /// A stack overflow ends the process, so the deep cases run on a thread with a small stack: without the limits
    /// they would end the test run instead of failing a single test.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ReportFilterNestingLimitTest
    {
        private const string kTerm = "src=1.1.1.1";
        private const string kOrTerm = " or src=1.1.1.1";
        private const int kAdvisoryNestingDepth = 100000;
        private const int kSmallStackBytes = 256 * 1024;
        private const int kLongChainLength = 5000;

        [Test]
        public void Compile_AcceptsBracketsUpToTheLimit()
        {
            string filter = Nest("(", Parser.kMaxNestingDepth, kTerm, ")");

            Assert.DoesNotThrow(() => CompileOnSmallStack(filter));
        }

        [Test]
        public void Compile_RejectsBracketsBeyondTheLimit()
        {
            string filter = Nest("(", Parser.kMaxNestingDepth + 1, kTerm, ")");

            SyntaxException exception = Assert.Throws<SyntaxException>(() => CompileOnSmallStack(filter))!;

            Assert.That(exception.Message, Does.Contain(Parser.kMaxNestingDepth.ToString()));
            Assert.That(exception.ErrorPosition.Start.Value, Is.EqualTo(Parser.kMaxNestingDepth));
        }

        [Test]
        public void Compile_AcceptsNegationsUpToTheLimit()
        {
            string filter = Nest("not ", Parser.kMaxNestingDepth, kTerm, "");

            Assert.DoesNotThrow(() => CompileOnSmallStack(filter));
        }

        [Test]
        public void Compile_RejectsNegationsBeyondTheLimit()
        {
            string filter = Nest("not ", Parser.kMaxNestingDepth + 1, kTerm, "");

            Assert.Throws<SyntaxException>(() => CompileOnSmallStack(filter));
        }

        [Test]
        public void Compile_CountsBracketsAndNegationsTogether()
        {
            int halfDepth = Parser.kMaxNestingDepth / 2;
            string atLimit = Nest("not (", halfDepth, kTerm, ")");
            string beyondLimit = "not " + atLimit;

            Assert.DoesNotThrow(() => CompileOnSmallStack(atLimit));
            Assert.Throws<SyntaxException>(() => CompileOnSmallStack(beyondLimit));
        }

        [Test]
        public void Compile_ReleasesNestingLevelsOfClosedBrackets()
        {
            string nestedAtLimit = Nest("(", Parser.kMaxNestingDepth, kTerm, ")");
            string filter = $"{nestedAtLimit} and {nestedAtLimit} or not {Nest("(", Parser.kMaxNestingDepth - 1, kTerm, ")")}";

            Assert.DoesNotThrow(() => CompileOnSmallStack(filter));
        }

        [Test]
        public void Compile_RejectsDeepNestingWithinTheLengthLimit()
        {
            int depth = (Compiler.kMaxFilterLength - kTerm.Length) / 2;
            string filter = Nest("(", depth, kTerm, ")");

            Assert.That(filter.Length, Is.LessThanOrEqualTo(Compiler.kMaxFilterLength));
            Assert.Throws<SyntaxException>(() => CompileOnSmallStack(filter));
        }

        [Test]
        public void Compile_RejectsAdvisoryBracketFixture()
        {
            string filter = Nest("(", kAdvisoryNestingDepth, kTerm, ")");

            SyntaxException exception = Assert.Throws<SyntaxException>(() => CompileOnSmallStack(filter))!;

            Assert.That(exception.ErrorPosition.Start.Value, Is.EqualTo(Compiler.kMaxFilterLength));
            Assert.That(exception.ErrorPosition.End.Value, Is.EqualTo(filter.Length));
        }

        [Test]
        public void Compile_RejectsAdvisoryNegationFixture()
        {
            string filter = Nest("not ", kAdvisoryNestingDepth, kTerm, "");

            Assert.Throws<SyntaxException>(() => CompileOnSmallStack(filter));
        }

        [Test]
        public void CompileToAst_AcceptsMaximumLengthAndRejectsOneCharacterMore()
        {
            string maximumFilter = kTerm + new string(' ', Compiler.kMaxFilterLength - kTerm.Length);

            Assert.DoesNotThrow(() => Compiler.CompileToAst(maximumFilter));
            Assert.Throws<SyntaxException>(() => Compiler.CompileToAst(maximumFilter + " "));
        }

        [Test]
        public void CompileToAst_RejectsNullInput()
        {
            Assert.Throws<ArgumentNullException>(() => Compiler.CompileToAst(null!));
        }

        [Test]
        public void Compile_AcceptsLongestOrChainWithinTheLengthLimit()
        {
            int termCount = (Compiler.kMaxFilterLength - kTerm.Length) / kOrTerm.Length;
            string filter = kTerm + string.Concat(Enumerable.Repeat(kOrTerm, termCount));

            DynGraphqlQuery query = RunOnSmallStack(() => Compile(filter));

            Assert.That(query.QueryVariables.Keys.Count(key => key.StartsWith("srcIpLow")), Is.EqualTo(termCount + 1));
        }

        [Test]
        public void Extract_WalksLongLeftChainsWithoutRecursion()
        {
            AstNode chain = BuildLeftChain(kLongChainLength, out int leafCount);

            DynGraphqlQuery query = RunOnSmallStack(() =>
            {
                DynGraphqlQuery extracted = new("");
                chain.Extract(ref extracted, ReportType.Rules);
                return extracted;
            });

            Assert.That(leafCount, Is.EqualTo(kLongChainLength + 1));
            Assert.That(query.RuleWhereStatement, Does.StartWith("_or: [{_or: [{"));
            Assert.That(query.RuleWhereStatement.Split("leaf").Length - 1, Is.EqualTo(leafCount));
            Assert.That(query.ConnectionWhereStatement.Split("}] ").Length - 1, Is.EqualTo(kLongChainLength));
        }

        [Test]
        public void Extract_KeepsOutputOfNestedChainsUnchanged()
        {
            DynGraphqlQuery query = new("");
            AstNodeConnector innerChain = new()
            {
                Connector = new Token(new Range(), "and", TokenKind.And),
                Left = new LeafNode("a"),
                Right = new LeafNode("b")
            };
            AstNodeConnector outerChain = new()
            {
                Connector = new Token(new Range(), "or", TokenKind.Or),
                Left = new AstNodeConnector
                {
                    Connector = new Token(new Range(), "or", TokenKind.Or),
                    Left = innerChain,
                    Right = new LeafNode("c")
                },
                Right = new AstNodeConnector
                {
                    Connector = new Token(new Range(), "and", TokenKind.And),
                    Left = new LeafNode("d"),
                    Right = new LeafNode("e")
                }
            };

            outerChain.Extract(ref query, ReportType.Rules);

            Assert.That(query.RuleWhereStatement, Is.EqualTo(
                "_or: [{_or: [{_and: [{a}, {b}] }, {c}] }, {_and: [{d}, {e}] }] "));
        }

        [Test]
        public void Extract_RejectsInvalidConnectorInsideTheChainBeforeWritingOutput()
        {
            DynGraphqlQuery query = new("");
            AstNodeConnector chain = new()
            {
                Connector = new Token(new Range(), "or", TokenKind.Or),
                Left = new AstNodeConnector
                {
                    Connector = new Token(new Range(), "or", TokenKind.Or),
                    Right = new LeafNode("b")
                },
                Right = new LeafNode("c")
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => chain.Extract(ref query, ReportType.Rules))!;

            Assert.That(exception.Message, Does.Contain("Left"));
            Assert.That(query.RuleWhereStatement, Is.Empty);
        }

        private static string Nest(string opening, int depth, string term, string closing)
        {
            StringBuilder filter = new();
            filter.Insert(0, opening, depth);
            filter.Append(term);
            filter.Insert(filter.Length, closing, depth);
            return filter.ToString();
        }

        private static DynGraphqlQuery Compile(string filter)
        {
            ReportTemplate template = new() { Filter = filter };
            template.ReportParams.ReportType = (int)ReportType.Rules;
            return Compiler.Compile(template);
        }

        private static void CompileOnSmallStack(string filter)
        {
            RunOnSmallStack(() => Compile(filter));
        }

        /// <summary>
        /// Runs the action on a thread with a small stack and rethrows its exception on the test thread.
        /// </summary>
        private static T RunOnSmallStack<T>(Func<T> action)
        {
            T? result = default;
            Exception? failure = null;
            Thread thread = new(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }, kSmallStackBytes);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            return result!;
        }

        private static AstNodeConnector BuildLeftChain(int connectorCount, out int leafCount)
        {
            AstNode current = new LeafNode("leaf");
            leafCount = 1;
            for (int index = 0; index < connectorCount; index++)
            {
                current = new AstNodeConnector
                {
                    Connector = new Token(new Range(), "or", TokenKind.Or),
                    Left = current,
                    Right = new LeafNode("leaf")
                };
                leafCount++;
            }
            return (AstNodeConnector)current;
        }

        private sealed class LeafNode(string text) : AstNode
        {
            public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
            {
                query.RuleWhereStatement += text;
                query.ConnectionWhereStatement += text;
            }
        }
    }
}

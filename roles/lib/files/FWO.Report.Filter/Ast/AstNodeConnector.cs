using FWO.Report.Filter.Exceptions;
using FWO.Basics;


namespace FWO.Report.Filter.Ast
{
    public class AstNodeConnector : AstNode
    {
        public AstNode? Right { get; set; }
        public AstNode? Left { get; set; }
        public Token? Connector { get; set; }
        private readonly string StartAnd = "_and: [{";
        private readonly string StartOr = "_or: [{";
        private readonly string Inbetween = "}, {";
        private readonly string CloseBrackets = "}] ";


        /// <summary>
        /// Adds the connected terms to all where statements of the query.
        /// </summary>
        /// <remarks>
        /// A chain such as <c>a or b or c</c> is parsed into connectors that are nested in their left operand,
        /// one level per term. Its length is bounded by the filter length only, so the left operands are walked
        /// in a loop instead of recursively to keep long chains from overflowing the stack.
        /// </remarks>
        /// <param name="query">Query to extend.</param>
        /// <param name="reportType">Type of report for which the filter is requested.</param>
        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            List<AstNodeConnector> leftChain = CollectLeftChain(out AstNode innermostLeft);

            foreach (AstNodeConnector connector in leftChain)
            {
                AppendToAllWhereStatements(query, connector.Connector!.Kind == TokenKind.And ? StartAnd : StartOr);
            }

            innermostLeft.Extract(ref query, reportType);

            for (int index = leftChain.Count - 1; index >= 0; index--)
            {
                AppendToAllWhereStatements(query, Inbetween);
                leftChain[index].Right!.Extract(ref query, reportType);
                AppendToAllWhereStatements(query, CloseBrackets);
            }
        }

        /// <summary>
        /// Collects this connector and all connectors nested in its left operands, outermost first, and checks each.
        /// </summary>
        /// <param name="innermostLeft">The left operand of the innermost connector, which is no connector itself.</param>
        /// <returns>The connectors of the chain, starting with this one.</returns>
        private List<AstNodeConnector> CollectLeftChain(out AstNode innermostLeft)
        {
            List<AstNodeConnector> leftChain = [];
            AstNode currentNode = this;
            while (currentNode is AstNodeConnector connector)
            {
                connector.Validate();
                leftChain.Add(connector);
                currentNode = connector.Left!;
            }
            innermostLeft = currentNode;
            return leftChain;
        }

        /// <summary>
        /// Checks that the connector has both operands and is an and or an or connector.
        /// </summary>
        private void Validate()
        {
            if (Connector == null)
            {
                throw new InvalidOperationException(nameof(Connector));
            }
            if (Left == null)
            {
                throw new InvalidOperationException(nameof(Left));
            }
            if (Right == null)
            {
                throw new InvalidOperationException(nameof(Right));
            }
            if (Connector.Kind != TokenKind.And && Connector.Kind != TokenKind.Or)
            {
                throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (prefix): \"{Connector}\". ###", Connector.Position);
            }
        }

        private static void AppendToAllWhereStatements(DynGraphqlQuery query, string text)
        {
            query.RuleWhereStatement += text;
            query.NwObjWhereStatement += text;
            query.SvcObjWhereStatement += text;
            query.UserObjWhereStatement += text;
            query.ConnectionWhereStatement += text;
        }
    }
}

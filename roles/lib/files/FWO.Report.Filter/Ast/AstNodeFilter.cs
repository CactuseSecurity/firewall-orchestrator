using FWO.Basics;
using FWO.Report.Filter.Exceptions;
using FWO.Report.Filter.FilterTypes;


namespace FWO.Report.Filter.Ast
{
    abstract class AstNodeFilter : AstNode
    {
        public Token Name { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Operator { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Value { get; set; } = new Token(new Range(), "", TokenKind.Value);

        protected static void CheckOperator(Token isOperator, bool equalsIsExactEquals, params TokenKind[] expectedOperators)
        {
            if (equalsIsExactEquals)
            {
                isOperator.EqualsIsExactEquals();
            }
            if (!expectedOperators.Contains(isOperator.Kind))
            {
                throw new SemanticException($"Expected one of the following tokens: {string.Join(", ", expectedOperators)} Got: {isOperator.Kind}", isOperator.Position);
            }
        }

        protected string ExtractOperator()
        {
            return Operator.Kind switch
            {
                TokenKind.EEQ => "_eq",
                TokenKind.EQ => "_ilike",
                TokenKind.NEQ => "_nilike",
                TokenKind.LSS => "_lt",
                TokenKind.GRT => "_gt",
                _ => throw new SemanticException("Invalid operator, even though this operator was expected. Internal error.", Operator.Position),
            };
        }

        protected static string AddVariable<TValue>(DynGraphqlQuery query, string name, TokenKind op, TValue value)
        {
            string queryVarName = name + query.parameterCounter++;
            string queryVarType;
            string queryVarValue;

            switch (value)
            {
                case bool boolValue:
                    queryVarType = "Boolean";
                    queryVarValue = boolValue ? "true" : "false";
                    break;

                case string stringValue:
                    queryVarType = "String";
                    queryVarValue = stringValue;
                    break;

                case int intValue:
                    queryVarType = "Int";
                    queryVarValue = intValue.ToString();
                    break;

                case DateTime dateTimeValue:
                    queryVarType = "timestamp";
                    queryVarValue = dateTimeValue.ToString(DynGraphqlQuery.fullTimeFormat);
                    break;

                case DateTimeRange dateTimeValue:
                    queryVarType = "timestamp";
                    if (dateTimeValue.Start == null && dateTimeValue.End == null)
                        throw new NotSupportedException($"LastHit filter with missing date");
                    DateTime date = new();
                    if (dateTimeValue.End != null)
                        date = (DateTime)dateTimeValue.End;
                    if (dateTimeValue.Start != null)
                        date = (DateTime)dateTimeValue.Start;
                    queryVarValue = date.ToString(DynGraphqlQuery.fullTimeFormat);
                    break;

                default:
                    throw new NotSupportedException($"Type \"{typeof(TValue)}\" is not supported in GraphQL Query");
            }

            query.QueryParameters.Add($"${queryVarName}: {queryVarType}! ");
            query.QueryVariables[queryVarName] = op == TokenKind.EQ ? $"%{queryVarValue}%" : queryVarValue;

            return queryVarName;
        }

        /// <summary>
        /// Builds a GraphQL condition for the canonical, protocol-agnostic ANY service.
        /// </summary>
        /// <param name="protocolIdField">Name of the protocol ID field in the target service type.</param>
        /// <param name="portStartField">Name of the start-port field in the target service type.</param>
        /// <param name="portEndField">Name of the end-port field in the target service type.</param>
        /// <returns>GraphQL condition matching only canonical ANY services.</returns>
        protected static string BuildCanonicalAnyServiceFilter(string protocolIdField, string portStartField, string portEndField)
        {
            return $"{{ {protocolIdField}: {{ _eq: {GlobalConst.kAnyIpProtocolId} }}, " +
                $"{portStartField}: {{ _is_null: true }}, {portEndField}: {{ _is_null: true }} }}";
        }

        public abstract void ConvertToSemanticType();
    }
}

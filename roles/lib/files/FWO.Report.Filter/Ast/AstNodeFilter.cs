using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter.Ast
{
    abstract class AstNodeFilter : AstNode
    {
        public Token Name { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Operator { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Value { get; set; } = new Token(new Range(), "", TokenKind.Value);

        protected void CheckOperator(Token isOperator, bool equalsIsExactEquals, params TokenKind[] expectedOperators)
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

        protected string AddVariable<Type>(DynGraphqlQuery query, string name, TokenKind op, Type value)
        {
            string queryVarName = name + query.parameterCounter++;
            string queryVarType = "";
            string queryVarValue = "";

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
                    DateTime date = new DateTime();
                    if (dateTimeValue.End != null)
                        date = (DateTime)dateTimeValue.End;
                    if (dateTimeValue.Start != null)
                        date = (DateTime)dateTimeValue.Start;
                    queryVarValue = date.ToString(DynGraphqlQuery.fullTimeFormat);
                    break;

                default:
                    throw new NotSupportedException($"Type \"{typeof(Type)}\" is not supported in GraphQL Query");
            }

            query.QueryParameters.Add($"${queryVarName}: {queryVarType}! ");
            query.QueryVariables[queryVarName] = op == TokenKind.EQ ? $"%{queryVarValue}%" : queryVarValue;

            return queryVarName;
        }

        public abstract void ConvertToSemanticType();

    }
}

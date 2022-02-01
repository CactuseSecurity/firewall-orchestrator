using System.ComponentModel;
using System.Net;
using FWO.Logging;
using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter.Ast
{
    abstract class AstNodeFilter : AstNode
    {
        public Token Name { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Operator { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Value { get; set; } = new Token(new Range(), "", TokenKind.Value);
        private List<string>? ruleFieldNames { get; set; }

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

                default:
                    throw new NotSupportedException($"Type \"{typeof(Type)}\" is not supported in GraphQL Query");
            }

            query.QueryParameters.Add($"${queryVarName}: {queryVarType}! ");
            query.QueryVariables[queryVarName] = op == TokenKind.EQ ? $"%{queryVarValue}%" : queryVarValue;

            return queryVarName;
        }

        public abstract void ConvertToSemanticType();

        //public void ConvertToSemanticType()
        //{
        //    TypeConverter converter = TypeDescriptor.GetConverter(this.GetType());
        //    if (converter.CanConvertFrom(this.GetType()))
        //    {
        //        try
        //        {
        //            object convertedValue = converter.ConvertFrom(this) ?? throw new NullReferenceException("Error while converting: converted value is null");
        //            SemanticValue = (SemanticType)convertedValue ?? throw new NullReferenceException($"Error while converting: value could not be converted to semantic type: {typeof(SemanticType)}");
        //        }
        //        catch (SemanticException)
        //        {
        //            throw;
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new SemanticException($"Filter could not be converted to expected semantic type {typeof(SemanticType)}: {ex.Message}", Value.Position);
        //        }
        //    }
        //    else
        //    {
        //        throw new NotSupportedException($"Internal error: TypeConverter does not support conversion from {this.GetType()} to {typeof(SemanticType)}");
        //    }
        //}

        //public override void Extract(ref DynGraphqlQuery query)
        //{
        //    switch (Name.Kind)
        //    

        //        // "xy" and "FullText=xy" are the same filter
        //        case TokenKind.FullText:
        //        case TokenKind.Value:
        //            ExtractFullTextFilter(query);
        //            break;
        //        case TokenKind.ReportType:
        //            ExtractReportTypeFilter(query);
        //            break;
        //        case TokenKind.Source:
        //            ExtractSourceFilter(query);
        //            break;
        //        case TokenKind.Destination:
        //            ExtractDestinationFilter(query);         
        //            break;
        //        case TokenKind.Action:
        //            ExtractActionFilter(query);
        //            break;
        //        case TokenKind.Service:
        //            ExtractServiceFilter(query);
        //            break;
        //        case TokenKind.DestinationPort:
        //            ExtractDestinationPortFilter(query);
        //            break;
        //        case TokenKind.Protocol:
        //            ExtractProtocolFilter(query);
        //            break;
        //        case TokenKind.Management:
        //            ExtractManagementFilter(query);
        //            break;
        //        case TokenKind.Gateway:
        //            ExtractGatewayFilter(query);
        //            break;
        //        case TokenKind.Remove:
        //            ExtractRemoveFilter(query);
        //            break;
        //        case TokenKind.RecertDisplay:
        //            ExtractRecertDisplayFilter(query); //,  (int)(SemanticValue as int?)!);
        //            break;
        //        case TokenKind.Time:
        //            ExtractTimeFilter(query);
        //            break;
        //        default:
        //            throw new NotSupportedException($"### Compiler Error: Found unexpected and unsupported filter token: \"{Name}\" ###");
        //    }
        //}      

        //private static string SetQueryOpString(Token @operator, Token filter, string value)
        //{
        //    string operation;
        //    switch (@operator.Kind)
        //    {
        //        case TokenKind.EQ:
        //            if (filter.Kind == TokenKind.Time || filter.Kind == TokenKind.DestinationPort)
        //                operation = "_eq";
        //            else if ((filter.Kind == TokenKind.Source && IsCidr(value)) || filter.Kind == TokenKind.DestinationPort)
        //                operation = "_eq";
        //            else if (filter.Kind == TokenKind.Management && int.TryParse(value, out int _))
        //                operation = "_eq";
        //            else
        //                operation = "_ilike";
        //            break;
        //        case TokenKind.NEQ:
        //            operation = "_nilike";
        //            break;
        //        default:
        //            throw new Exception("### Parser Error: Expected Operator Token (and thought there is one) ###");
        //    }
        //    return operation;
        //}
    }
}

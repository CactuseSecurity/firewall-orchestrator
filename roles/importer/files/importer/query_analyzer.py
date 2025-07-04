try:
    # GraphQL-core v3+
    from graphql import parse, print_ast, visit
    from graphql.language import Visitor
    from graphql.language.ast import DocumentNode as Document, VariableDefinitionNode as VariableDefinition, OperationDefinitionNode as OperationDefinition
except ImportError:
    # GraphQL-core v2
    from graphql import parse, print_ast, visit
    from graphql.language.visitor import Visitor
    from graphql.language.ast import Document, VariableDefinition, OperationDefinition

from typing import Dict, Any, Optional

from fwo_const import api_call_chunk_size


class QueryAnalyzer(Visitor):
    """
        A class for analyzing GraphQL queries.
    """
    
    _ast: Optional[Document]
    _variable_definitions: Dict[str, Dict[str, Any]]
    _query_string: str
    _query_variables: Dict[str, Any]
    _query_info: Dict[str, Any]

    @property
    def variable_definitions(self) -> Dict[str, Dict[str, Any]]:
        """Returns the dictionary of extracted variable definitions."""
        return self._variable_definitions
    
    @property
    def ast(self) -> Optional[Document]:
        """Returns the AST."""
        return self._ast
    
    @property
    def query_string(self) -> str:
        """Returns the original query string."""
        return self._query_string
    
    @property
    def query_variables(self) -> Dict[str, Any]:
        """Returns the provided query variables."""
        return self._query_variables
    
    @property
    def query_variables(self) -> Dict[str, Any]:
        """Returns a dictionary that provides all information about the query and the provided query variables."""
        return self._query_variables


    def __init__(self):
        super().__init__()
        self._ast = None
        self._variable_definitions = {}
        self._query_string = ""
        self._query_variables = {}
        self._query_info = {}

    

    def analyze_payload(self, query_string: str, query_variables: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """
            Analyzes a GraphQL query and returns information about it.
        """

        self._ast = parse(query_string)
        self._query_string = query_string
        self._query_variables = query_variables or {}
        self._variable_definitions = {}
        
        # Apply visitor pattern (calls enter_* methods)

        visit(self._ast, self)

        # Analyze necessity of chunking and parameters that are necessary for the chunking process.

        needs_chunking, adjusted_chunk_size, list_elements_length, chunkable_variables = self._get_chunking_info(query_variables)
        self._query_info["chunking_info"]  = {
            "needs_chunking": needs_chunking,
            "adjusted_chunk_size": adjusted_chunk_size,
            "chunkable_variables": chunkable_variables,
            "total_elements": list_elements_length
        }
        
        return self._query_info
    

    def get_adjusted_chunk_size(self, lists_in_query_variable: dict):
        """
            Gets an adjusted chunk size.
        """
        
        return int(api_call_chunk_size / 
                    len(
                        [
                        list_object for list_object in lists_in_query_variable.values() 
                        if len(list_object) > 0
                ]
            )
        ) or 1
    

    def enter_OperationDefinition(self, node: OperationDefinition, *_):
        """
            Called by visit function for each variable definition in the AST.
        """

        self.enter_operation_definition(node)


    def enter_VariableDefinition(self, node: VariableDefinition, *_):
        """
            Called by visit function for each variable definition in the AST.
        """

        self.enter_variable_definition(node)


    def enter_operation_definition(self, node: OperationDefinition, *_):
        """
            Called by visit function for each variable definition in the AST.
        """

        self._query_info["query_type"] = node.operation
        self._query_info["query_name"] = node.name.value if node.name else ""


    def enter_variable_definition(self, node: VariableDefinition, *_):
        """
            Called by visit function for each variable definition in the AST.
        """

        var_name = node.variable.name.value
        type_str = print_ast(node.type)
        
        # Store information about the variable definitions.

        if not "query_args" in self._query_info.keys():
            self._query_info["query_args"] = {}

        self._query_info["query_args"][var_name] = type_str

        self._variable_definitions[var_name] = {
            "type": type_str,
            "required": "!" in type_str,
            "is_list": "[" in type_str
        }
        
        # If a value was provided for this variable, store it.

        if var_name in self._query_variables:
            self._variable_definitions[var_name]["provided_value"] = self._query_variables[var_name]


    def _get_chunking_info(self, query_variables: dict):

        # Get all query variables of type list.

        lists_in_query_variable = {
            chunkable_variable_name: list_object 
            for chunkable_variable_name, list_object in query_variables.items() 
            if isinstance(list_object, list)
        }

        # If there is no list typed query variable there is nothing chunkable.

        if not lists_in_query_variable or len(lists_in_query_variable.items()) == 0:
            return False, 0, 0, []
        
        list_elements_length = sum(
            len(list_object) 
            for list_object in lists_in_query_variable.values() 
        )

        # If the number of all elements is lower than the configured threshold, there is no need for chunking.

        if list_elements_length < api_call_chunk_size:
            return False, 0, 0, []
        
        # If there are more than one chunkable variable, the chunk_size has to be adjusted accordingly.

        adjusted_chunk_size = self.get_adjusted_chunk_size(lists_in_query_variable)

        return True, adjusted_chunk_size, list_elements_length, list(lists_in_query_variable.keys())


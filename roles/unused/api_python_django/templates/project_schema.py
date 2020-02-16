import graphene
import {{ api_app_name }}.schema

class Query({{ api_app_name }}.schema.Query, graphene.ObjectType):
    pass

schema = graphene.Schema(query=Query)

# used for graphql only

import graphene
from graphene_django import DjangoObjectType
from {{ api_app_name }}.models import Management,Device

class ManagementType(DjangoObjectType):
    class Meta:
        model = Management
class DeviceType(DjangoObjectType):
    class Meta:
        model = Device
#class StmDevTypType(DjangoObjectType):
#    class Meta:
#        model = StmDevTyp
#class NwObjecType(DjangoObjectType):
#    class Meta:
#        model = NwObject

class Query(graphene.ObjectType):
    managements = graphene.List(ManagementType)
    def resolve_managements(self, info, **kwargs):
        return Management.objects.all()

    devices = graphene.List(DeviceType)
    def resolve_devices(self, info, **kwargs):
        return Device.objects.all()
        
#    stmDevTypes = graphene.List(StmDevTypType)
#    def resolve_stmDevTypes(self, info, **kwargs):
#        return StmDevTyp.objects.all()
        
#    NwObjects = graphene.List(NwObjectType)
#    def resolve_NwObjects(self, info, **kwargs):
#        return NwObject.objects.all()
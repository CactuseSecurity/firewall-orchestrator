# api/resources.py
from tastypie.resources import ModelResource
from {{ api_project_name }}.models import Isoclass IsoResource(ModelResource):
    class Meta:
        queryset = Management.objects.all()
        resource_name = 'management'
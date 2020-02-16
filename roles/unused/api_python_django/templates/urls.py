from django.conf.urls import url
from django.contrib import admin
from django.urls import path
from {{ api_app_name }} import views #import views
from graphene_django.views import GraphQLView
from django.views.decorators.csrf import csrf_exempt


urlpatterns = [
#   path('', include(router.urls)),
#   path('auth/', include('rest_framework.urls', namespace='rest_framework'))
   path('admin/', admin.site.urls),
   path('managements/', views.managementList.as_view()),
   path('devices/', views.deviceList.as_view()),
#   path('stmDevTyp/', views.stmDevTypList.as_view()),
   path('graphql/', csrf_exempt(GraphQLView.as_view(graphiql=True))),
]

#from django.conf.urls import url, include
#from django.contrib import admin
#from {{ api_project_name }}.resources import NoteResourcenote_resource = NoteResource()urlpatterns = [
#    url(r'^admin/', admin.site.urls),
#    url(r'^managements/', include(management_resource.urls)),
#    url(r'^devices/', include(device_resource.urls)),
#]
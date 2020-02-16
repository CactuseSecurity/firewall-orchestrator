 # Create your views here.
 
from django.shortcuts import render
from django.http import HttpResponse
from django.shortcuts import get_object_or_404
from rest_framework.views import APIView
from rest_framework.response import Response
from rest_framework import status
from .models import Management, Device
from .serializers import managementsSerializer, devicesSerializer
from django.contrib.auth.mixins import LoginRequiredMixin

class managementList(LoginRequiredMixin, APIView):
    login_url = '/api/admin/'
    def get(self,request):
        management_list = Management.objects.all()
        serializer = managementsSerializer(management_list, many= True)
        return Response(serializer.data) # Return JSON
    def post(self):
        pass
    def put(self):
        pass
        
class deviceList(LoginRequiredMixin, APIView):
    login_url = '/api/admin/'
    def get(self,request):
        device_list = Device.objects.all()
        serializer = devicesSerializer(device_list, many= True)
        return Response(serializer.data) # Return JSON
    def post(self):
        pass
    def put(self):
        pass
        
#class stmDevTypList(LoginRequiredMixin, APIView):
#    login_url = '/api/admin/'
#    def get(self,request):
#        stmDevTyp_list = StmDevTyp.objects.all()
#        serializer = stmDevTypSerializer(stmDevTyp_list, many= True)
#        return Response(serializer.data) # Return JSON
#    def post(self):
#        pass
#    def put(self):
#        pass

#class NwObjectList(LoginRequiredMixin, APIView):
#    login_url = '/api/admin/'
#    def get(self,request):
#        NwObject_list = NwObject.objects.all()
#        serializer = nwObjectSerializer(NwObject_list, many= True)
#        return Response(serializer.data) # Return JSON
#    def post(self):
#        pass
#    def put(self):
#        pass

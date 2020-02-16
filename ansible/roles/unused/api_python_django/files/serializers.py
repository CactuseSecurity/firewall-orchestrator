from rest_framework import serializers
from .models import Device,Management   #import model

class managementsSerializer(serializers.ModelSerializer):

    class Meta:
        model = Management
        fields = '__all__'
        
        
class devicesSerializer(serializers.ModelSerializer):

    class Meta:
        model = Device
        fields = '__all__'
        
        
#class stmDevTypSerializer(serializers.ModelSerializer):
#
#    class Meta:
#        model = StmDevTyp
#        fields = '__all__'
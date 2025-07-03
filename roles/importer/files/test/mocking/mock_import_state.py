from importer.model_controllers.import_state_controller import ImportStateController
from importer.model_controllers.import_statistics_controller import ImportStatisticsController
from importer.model_controllers.fworch_config_controller import FworchConfigController
from importer.models.track import Track
from importer.models.action import Action
from .mock_fwo_api_oo import MockFwoApi

try:
    from mock_management_details_controller import MockManagementDetailsController
except ModuleNotFoundError:
    from .mock_management_details_controller import MockManagementDetailsController


def make_hashable(obj):
    if isinstance(obj, dict):
        return tuple(sorted((k, make_hashable(v)) for k, v in obj.items()))
    elif isinstance(obj, (list, set)):
        return tuple(make_hashable(i) for i in obj)
    elif isinstance(obj, tuple):
        return tuple(make_hashable(i) for i in obj)
    else:
        return obj
class MockImportStateController(ImportStateController):
    """
        Mock class for ImportState.
    """

    def __init__(self, import_id: int = 0):
        """
            Initializes without calling base init. This avoids the necessity to provide JWT and management details.
        """

        self.DebugLevel = 0
        self.Stats = ImportStatisticsController()
        self.call_log = []
        self.stub_responses = {}
        self.ImportVersion = 9
        self.MgmDetails = MockManagementDetailsController()
        self.api_connection = MockFwoApi()
        self.FwoConfig = FworchConfigController(
            fwoApiUri='',
            fwoUserMgmtApiUri='',
            importerPwd='',
            apiFetchSize=500
        )
        self.Jwt = None
        self.ImportId = import_id
        self.IsFullImport = True
        self.setCoreData()

        self.color_id_map = {}
        self.track_id_map = {}
        self.action_id_map = {}
        self.service_id_map = {}
        self.network_object_id_map = {}
        self.user_id_map = {}


    def call(self, *args, **kwargs):

        self.call_log.append((args, kwargs))
        key = (make_hashable(args), make_hashable(kwargs))

        if key in self.stub_responses:
            return self.stub_responses[key]

        return self.api_connection.call(*args, **kwargs)


    def setup_response(self, args, kwargs, response):
        
        key = (args, make_hashable(kwargs))
        self.stub_responses[key] = response

    dummy_track = Track(
        track_id=0,
        track_name="Dummy Track",
    )
    def lookupTrack(self, trackStr):
        if trackStr not in self.track_id_map:
            self.track_id_map[trackStr] = len(self.track_id_map) + 1
        return self.track_id_map[trackStr]
    
    def lookupTrackStr(self, track_id):
        for track_name, id_ in self.track_id_map.items():
            if id_ == track_id:
                return track_name
        return None
    
    dummy_action = Action(
        action_id=0,
        action_name="Dummy Action",
        allowed=True
    )
    def lookupAction(self, actionStr):
        if actionStr not in self.action_id_map:
            self.action_id_map[actionStr] = len(self.action_id_map) + 1
        return self.action_id_map[actionStr]
    
    def lookupActionStr(self, action_id):
        for action_name, id_ in self.action_id_map.items():
            if id_ == action_id:
                return action_name
        return None
    
    def lookupLinkType(self, linkUid):
        if linkUid not in self.track_id_map:
            self.track_id_map[linkUid] = len(self.track_id_map) + 1
        return self.track_id_map[linkUid]
    
    def lookupLinkTypeUid(self, linkId):
        for linkUid, id_ in self.track_id_map.items():
            if id_ == linkId:
                return linkUid
        return None
    
    def lookupGatewayId(self, gwUid):
        if gwUid not in self.network_object_id_map:
            self.network_object_id_map[gwUid] = len(self.network_object_id_map) + 1
        return self.network_object_id_map[gwUid]
    
    def lookupGatewayUid(self, gwId):
        for gwUid, id_ in self.network_object_id_map.items():
            if id_ == gwId:
                return gwUid
        return None
    
    def lookupColorId(self, color_str):
        if color_str not in self.color_id_map:
            self.color_id_map[color_str] = len(self.color_id_map) + 1
        return self.color_id_map[color_str]
    
    def lookupColorStr(self, color_id):
        for color_str, id_ in self.color_id_map.items():
            if id_ == color_id:
                return color_str
        return None

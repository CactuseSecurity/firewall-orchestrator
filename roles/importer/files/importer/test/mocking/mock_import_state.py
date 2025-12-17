from fwo_api_call import FwoApiCall
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.import_state_controller import ImportStateController
from model_controllers.import_statistics_controller import ImportStatisticsController
from models.action import Action
from models.import_state import ImportState
from models.track import Track

from .mock_fwo_api_oo import MockFwoApi

try:
    from .mock_management_controller import MockManagementController
except ModuleNotFoundError:
    from .mock_management_controller import MockManagementController


def make_hashable(obj):
    if isinstance(obj, dict):
        return tuple(sorted((k, make_hashable(v)) for k, v in obj.items()))
    if isinstance(obj, (list, set, tuple)):
        return tuple(make_hashable(i) for i in obj)
    return obj


class MockImportStateController(ImportStateController):
    """
    Mock class for ImportState.
    """

    _stub_setCoreData: bool = False

    def __init__(self, import_id: int = 0, stub_setCoreData: bool = False):
        """
        Initializes without calling base init. This avoids the necessity to provide JWT and management details.
        """
        self._stub_setCoreData = stub_setCoreData


        self.state = ImportState()
        self.api_connection = MockFwoApi()
        self.api_call = FwoApiCall(self.api_connection)

    @property
    def stub_setCoreData(self) -> bool:
        """
        Indicates whether to stub setCoreData.
        """
        return self._stub_setCoreData

    @stub_setCoreData.setter
    def stub_setCoreData(self, value: bool):
        self._stub_setCoreData = value

    def set_core_data(self):
        if self._stub_setCoreData:
            return
        super().set_core_data()

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

    def lookup_track(self, trackStr):
        if trackStr not in self.track_id_map:
            self.track_id_map[trackStr] = len(self.track_id_map) + 1
        return self.track_id_map[trackStr]

    def lookupTrackStr(self, track_id):
        for track_name, id_ in self.track_id_map.items():
            if id_ == track_id:
                return track_name
        return None

    dummy_action = Action(action_id=0, action_name="Dummy Action", allowed=True)

    def lookup_action(self, actionStr):
        if actionStr not in self.action_id_map:
            self.action_id_map[actionStr] = len(self.action_id_map) + 1
        return self.action_id_map[actionStr]

    def lookupActionStr(self, action_id):
        for action_name, id_ in self.action_id_map.items():
            if id_ == action_id:
                return action_name
        return None

    def lookup_link_type(self, linkUid):
        if linkUid not in self.track_id_map:
            self.track_id_map[linkUid] = len(self.track_id_map) + 1
        return self.track_id_map[linkUid]

    def lookupLinkTypeUid(self, linkId):
        for linkUid, id_ in self.track_id_map.items():
            if id_ == linkId:
                return linkUid
        return None

    def lookup_gateway_id(self, gwUid):
        if gwUid not in self.network_object_id_map:
            self.network_object_id_map[gwUid] = len(self.network_object_id_map) + 1
        return self.network_object_id_map[gwUid]

    def lookupGatewayUid(self, gwId):
        for gwUid, id_ in self.network_object_id_map.items():
            if id_ == gwId:
                return gwUid
        return None

    def lookupColorStr(self, color_id):
        for color_str, id_ in self.ColorMap.items():
            if id_ == color_id:
                return color_str
        return None

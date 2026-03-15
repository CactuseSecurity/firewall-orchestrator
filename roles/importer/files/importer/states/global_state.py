from models.fwo_config_controller import FwoConfigController


class GlobalState:
    fwo_config_controller: FwoConfigController
    # stm tabellen

    def __init__(
        self,
        config_filename: str,
        force: bool,
        clear: bool,
        debug_level: int,
    ):
        self.fwo_config_controller = FwoConfigController(
            config_filename, force=force, clear=clear, debug_level=debug_level
        )

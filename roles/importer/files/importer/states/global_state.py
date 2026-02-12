from models.fwo_config_controller import FwoConfigController


class GlobalState:
    fwo_config_controller: FwoConfigController

    def __init__(
        self,
        config_filename: str,
        force: bool,
        is_full_import: bool,
        clear: bool,
        debug_level: int,
    ):
        self.fwo_config_controller = FwoConfigController(
            config_filename, force=force, is_full_import=is_full_import, clear=clear, debug_level=debug_level
        )

Importer (roles/importer) normalizes vendor firewall and manager configurations into the common FWO model. It fetches data from management APIs or files, applies normalization, and persists results through the GraphQL API. The summaries below describe each relevant code file under roles/importer/files.

## Importer entry points and config

### `roles/importer/files/import.conf`
Primary importer configuration file that sets directories, sleep intervals, and CLI paths. It also defines CSV and group delimiters plus defaults for importer runtime behavior.

### `roles/importer/files/importer/import-main-loop.py`
Main loop entry point that repeatedly imports all configured managements. It handles JWT login, scheduling, and per-management import execution with retries.

### `roles/importer/files/importer/import-mgm.py`
CLI entry point to import a single management or a provided config file. It logs in via the middleware, initializes import state, and calls the core import routine.

## Core importer framework

### `roles/importer/files/importer/common.py`
Top-level import orchestration helpers that manage locking, exception handling, and lifecycle steps. It coordinates reading config data, normalization, consistency checks, and API writes.

### `roles/importer/files/importer/fwo_api.py`
GraphQL API client used by the importer to call Hasura with JWT authentication. It handles login, chunked payloads, and request error handling.

### `roles/importer/files/importer/fwo_api_call.py`
Higher-level API wrapper that provides importer-specific GraphQL operations. It manages import locks, configuration lookups, and helper queries used during imports.

### `roles/importer/files/importer/fwo_base.py`
Core utility functions for normalization, serialization, and data comparison. It includes IP helpers and rule order diff logic used across import steps.

### `roles/importer/files/importer/fwo_config.py`
Reads fworch runtime configuration and importer secrets from disk. It returns API endpoints and version info needed to start imports.

### `roles/importer/files/importer/fwo_const.py`
Central constants for paths, API settings, and importer defaults. Used across modules to keep configuration values consistent.

### `roles/importer/files/importer/fwo_enums.py`
Enum definitions for configuration formats, actions, and other importer types. Shared by models, controllers, and vendor modules for consistent values.

### `roles/importer/files/importer/fwo_exceptions.py`
Custom exception types used throughout the importer pipeline. They standardize error handling for API, parsing, and normalization failures.

### `roles/importer/files/importer/fwo_globals.py`
Global flags for import behavior such as certificate verification or shutdown state. Provides shared state accessed by API clients and the main loop.

### `roles/importer/files/importer/fwo_log.py`
Logging setup for the importer with debug level routing. It provides a common logger used by all modules and tests.

### `roles/importer/files/importer/fwo_encrypt.py`
Helpers for encrypting and decrypting secrets used by importer credentials. It reads the main key file and wraps cryptographic operations.

### `roles/importer/files/importer/fwo_file_import.py`
Reads configuration files from disk or URL and detects legacy formats. It converts file content into normalized manager lists for import.

### `roles/importer/files/importer/fwo_signalling.py`
Signal handling helpers to support graceful shutdown of the importer loop. It registers handlers and exposes shutdown state to long-running tasks.

### `roles/importer/files/importer/fwconfig_base.py`
Base helpers for config serialization, including a JSON encoder and null handling. Shared by model code when emitting normalized data structures.

### `roles/importer/files/importer/fwo_local_settings.py`
Optional local overrides for importer settings. Used to layer site-specific configuration on top of defaults.

### `roles/importer/files/importer/query_analyzer.py`
Analyzes GraphQL payloads to decide when chunking is required. Used by the API client to control request sizing and logging.

## Models

### `roles/importer/files/importer/models/action.py`
Defines the action model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/caseinsensitiveenum.py`
Enum helper that performs case-insensitive parsing. Used by model enums to handle vendor values reliably.

### `roles/importer/files/importer/models/fw_common.py`
Abstract base class for firewall-specific import modules. Defines the expected interface for fetching and normalizing configs.

### `roles/importer/files/importer/models/fwconfig.py`
Defines the fwconfig model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/fwconfig_base.py`
Defines the fwconfig base model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/fwconfig_normalized.py`
Defines the fwconfig normalized model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/fwconfigmanager.py`
Defines the fwconfigmanager model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/fwconfigmanagerlist.py`
Defines the fwconfigmanagerlist model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/fworch_config.py`
Defines the fworch config model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/gateway.py`
Defines the gateway model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/import_state.py`
Defines the import state model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/import_statistics.py`
Defines the import statistics model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/management.py`
Defines the management model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/networkobject.py`
Defines the networkobject model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rule.py`
Defines the rule model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rule_enforced_on_gateway.py`
Defines the rule enforced on gateway model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rule_from.py`
Defines the rule from model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rule_metadatum.py`
Defines the rule metadatum model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rule_service.py`
Defines the rule service model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rule_to.py`
Defines the rule to model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rulebase.py`
Defines the rulebase model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/rulebase_link.py`
Defines the rulebase link model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/serviceobject.py`
Defines the serviceobject model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

### `roles/importer/files/importer/models/track.py`
Defines the track model used by the importer data layer. It is serialized into normalized configs and consumed by controllers.

## Controllers

### `roles/importer/files/importer/model_controllers/check_consistency.py`
Controller for check consistency that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfig_controller.py`
Controller for fwconfig controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfig_import.py`
Controller for fwconfig import that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfig_import_gateway.py`
Controller for fwconfig import gateway that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfig_import_object.py`
Controller for fwconfig import object that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfig_import_rule.py`
Controller for fwconfig import rule that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfig_import_ruleorder.py`
Controller for fwconfig import ruleorder that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfig_normalized_controller.py`
Controller for fwconfig normalized controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfigmanager_controller.py`
Controller for fwconfigmanager controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fwconfigmanagerlist_controller.py`
Controller for fwconfigmanagerlist controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/fworch_config_controller.py`
Controller for fworch config controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/gateway_controller.py`
Controller for gateway controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/import_state_controller.py`
Controller for import state controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/import_statistics_controller.py`
Controller for import statistics controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/interface_controller.py`
Controller for interface controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/management_controller.py`
Controller for management controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/rollback.py`
Controller for rollback that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/route_controller.py`
Controller for route controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/rule_enforced_on_gateway_controller.py`
Controller for rule enforced on gateway controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/rulebase_link_controller.py`
Controller for rulebase link controller that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/rulebase_link_map.py`
Controller for rulebase link map that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

### `roles/importer/files/importer/model_controllers/test_fwconfig_import_rule.py`
Controller for test fwconfig import rule that coordinates import logic and persistence. It transforms API payloads into models and writes normalized data.

## Services

### `roles/importer/files/importer/services/enums.py`
Service-layer enums for lifetimes and service identifiers. Used by the service provider to manage shared dependencies.

### `roles/importer/files/importer/services/global_state.py`
Global runtime state for a single import process. Stores the active import state and the normalized config being processed.

### `roles/importer/files/importer/services/group_flats_mapper.py`
Service that resolves group membership into flat lists. Used to expand groups during rule and object normalization.

### `roles/importer/files/importer/services/service_provider.py`
Dependency container that constructs and caches importer services. It centralizes service lifetime management and shared state.

### `roles/importer/files/importer/services/uid2id_mapper.py`
Service that maps vendor UIDs to database IDs. It caches mappings to reduce repeated API queries.

## Vendor modules

### Azure firewall

#### `roles/importer/files/importer/fw_modules/azure2022ff/fwcommon.py`
Shared utilities and common types for the Azure firewall module. Used by vendor-specific parsers and normalizers.

### Check Point R8x

#### `roles/importer/files/importer/fw_modules/checkpointR8x/cp_const.py`
Constants and fixed mappings for Check Point R8x imports. Centralizes IDs, field names, and defaults used by the module.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/cp_gateway.py`
Gateway and device parsing for Check Point R8x. Builds gateway models and associates them with management data.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/cp_getter.py`
API fetching helpers for Check Point R8x management data. Handles request sequencing, pagination, and response parsing.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/cp_network.py`
Network object parsing and normalization for Check Point R8x. Maps addresses, groups, and networks into normalized objects.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/cp_rule.py`
Rule parsing and normalization for Check Point R8x policies. Transforms vendor rule objects into the common rule model.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/cp_service.py`
Service object parsing and normalization for Check Point R8x. Converts protocol and port definitions into common service objects.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/cp_user.py`
User and identity object parsing for Check Point R8x. Maps vendor users and groups into normalized representations.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/discovery_logging.conf`
Logging configuration for Check Point R8x discovery workflows. Used to tune verbosity and handlers for module runs.

#### `roles/importer/files/importer/fw_modules/checkpointR8x/fwcommon.py`
Shared utilities and common types for the Check Point R8x module. Used by vendor-specific parsers and normalizers.

### Cisco ASA

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_maps.py`
Lookup tables for translating Cisco ASA identifiers. Used during parsing and normalization to map values.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_models.py`
Lightweight data structures for Cisco ASA parser output. Holds parsed objects prior to normalization.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_network.py`
Network object parsing and normalization for Cisco ASA. Maps addresses, groups, and networks into normalized objects.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_normalize.py`
Normalization pipeline for Cisco ASA parser output. Converts parsed objects into the standard config schema.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_parser.py`
Parser for Cisco ASA native configuration. Builds intermediate objects that the normalizer consumes.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_parser_functions.py`
Helper functions for parsing Cisco ASA configuration syntax. Supports tokenization and object extraction for the parser.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_rule.py`
Rule parsing and normalization for Cisco ASA policies. Transforms vendor rule objects into the common rule model.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/asa_service.py`
Service object parsing and normalization for Cisco ASA. Converts protocol and port definitions into common service objects.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/fwcommon.py`
Shared utilities and common types for the Cisco ASA module. Used by vendor-specific parsers and normalizers.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/test.py`
Local test harness for the Cisco ASA module. Used for manual runs against sample configuration data.

#### `roles/importer/files/importer/fw_modules/ciscoasa9/test_asa.conf`
Sample Cisco ASA configuration used for parser tests. Provides representative objects and rules for normalization.

### Cisco Firepower

#### `roles/importer/files/importer/fw_modules/ciscofirepowerdomain7ff/fwcommon.py`
Shared utilities and common types for the Cisco Firepower module. Used by vendor-specific parsers and normalizers.

### FortiManager

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/discovery_logging.conf`
Logging configuration for FortiManager discovery workflows. Used to tune verbosity and handlers for module runs.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_base.py`
Base classes and shared helpers for FortiManager integration. Provides session handling and common utilities used by the module.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_consts.py`
Constants and fixed mappings for FortiManager imports. Centralizes IDs, field names, and defaults used by the module.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_getter.py`
API fetching helpers for FortiManager management data. Handles request sequencing, pagination, and response parsing.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_gw_networking.py`
Gateway networking parsing for FortiManager. Maps interfaces and routing data into normalized structures.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_network.py`
Network object parsing and normalization for FortiManager. Maps addresses, groups, and networks into normalized objects.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_rule.py`
Rule parsing and normalization for FortiManager policies. Transforms vendor rule objects into the common rule model.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_service.py`
Service object parsing and normalization for FortiManager. Converts protocol and port definitions into common service objects.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_user.py`
User and identity object parsing for FortiManager. Maps vendor users and groups into normalized representations.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fmgr_zone.py`
Zone parsing and normalization for FortiManager. Maps interfaces and zones into the common zoning model.

#### `roles/importer/files/importer/fw_modules/fortiadom5ff/fwcommon.py`
Shared utilities and common types for the FortiManager module. Used by vendor-specific parsers and normalizers.

### FortiOS REST

#### `roles/importer/files/importer/fw_modules/fortiosmanagementREST/fwcommon.py`
Shared utilities and common types for the FortiOS REST module. Used by vendor-specific parsers and normalizers.

### VMware NSX

#### `roles/importer/files/importer/fw_modules/nsx4ff/fwcommon.py`
Shared utilities and common types for the VMware NSX module. Used by vendor-specific parsers and normalizers.

### Palo Alto management

#### `roles/importer/files/importer/fw_modules/paloaltomanagement2023ff/fwcommon.py`
Shared utilities and common types for the Palo Alto management module. Used by vendor-specific parsers and normalizers.

## Tests

### `roles/importer/files/importer/test/test_fOS_normalize_access_rules.py`
Test module for test fOS normalize access rules behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/test_fwconfig_import_consistency.py`
Test module for test fwconfig import consistency behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/test_fwconfig_import_ruleorder.py`
Test module for test fwconfig import ruleorder behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/test_fwo_base.py`
Test module for test fwo base behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/test_update_rulebase_diffs.py`
Test module for test update rulebase diffs behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/test_update_rulebase_link_diffs.py`
Test module for test update rulebase link diffs behaviors. It provides coverage for importer logic and data transformations.

## Test tooling

### `roles/importer/files/importer/test/tools/create_mock_config_file.py`
Test module for create mock config file behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/tools/set_up_test.py`
Test module for set up test behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/tools/stopwatch.py`
Test module for stopwatch behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/tools/testhelper.py`
Test module for testhelper behaviors. It provides coverage for importer logic and data transformations.

## Test mocks

### `roles/importer/files/importer/test/mocking/mock_config.py`
Test module for mock config behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/mocking/mock_fwconfig_import_gateway.py`
Test module for mock fwconfig import gateway behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/mocking/mock_fwconfig_import_rule.py`
Test module for mock fwconfig import rule behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/mocking/mock_fwo_api_oo.py`
Test module for mock fwo api oo behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/mocking/mock_import_state.py`
Test module for mock import state behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/mocking/mock_management_controller.py`
Test module for mock management controller behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/mocking/mock_rulebase.py`
Test module for mock rulebase behaviors. It provides coverage for importer logic and data transformations.

### `roles/importer/files/importer/test/mocking/uid_manager.py`
Test module for uid manager behaviors. It provides coverage for importer logic and data transformations.

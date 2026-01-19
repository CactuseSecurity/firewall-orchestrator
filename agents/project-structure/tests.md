Tests live under the unit and integration test roles in `roles/tests-unit/files` and `roles/tests-integration/files`. Unit tests focus on C# assemblies and helpers, while integration assets drive API, importer, and LDAP smoke checks. The summaries below describe each relevant test file to help agents locate coverage quickly.

## Unit tests

### `roles/tests-unit/files/FWO.Test/AesEncryptionTest.cs`
Unit tests for Aes Encryption. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ApiConfigTest.cs`
Unit tests for Api Config. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ApiTest.cs`
Unit tests for Api. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/AppRoleTest.cs`
Unit tests for App Role. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ComparerTest.cs`
Unit tests for Comparer. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ComplianceCheckTest.cs`
Unit tests for Compliance Check. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ConfigFileTest.cs`
Unit tests for Config File. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/DisplayBaseTest.cs`
Unit tests for Display Base. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/DistNameTest.cs`
Unit tests for Dist Name. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ExportTest.cs`
Unit tests for Export. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ExtRequestSenderTest.cs`
Unit tests for Ext Request Sender. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ExtRequestSenderTestApiConn.cs`
Test API connection stub for Ext Request Sender. Supplies canned responses to isolate unit test behavior.

### `roles/tests-unit/files/FWO.Test/ExtStateTestApiConn.cs`
Test API connection stub for Ext State. Supplies canned responses to isolate unit test behavior.

### `roles/tests-unit/files/FWO.Test/ExtTicketHandlerTest.cs`
Unit tests for Ext Ticket Handler. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ExtTicketHandlerTestApiConn.cs`
Test API connection stub for Ext Ticket Handler. Supplies canned responses to isolate unit test behavior.

### `roles/tests-unit/files/FWO.Test/FakeLocalTimeZone.cs`
Fake implementation for Local Time Zone used in unit tests. It replaces runtime behavior to simplify assertions.

### `roles/tests-unit/files/FWO.Test/FilterTest.cs`
Unit tests for Filter. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/Fixtures/ComplianceCheckTestFixture.cs`
Shared test fixture for Compliance Check tests. Provides setup and teardown data for repeatable unit runs.

### `roles/tests-unit/files/FWO.Test/HtmlToPdfTest.cs`
Unit tests for Html To Pdf. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/IPAddressRangeComparerTest.cs`
Unit tests for IP Address Range Comparer. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/IPOperationsTest.cs`
Unit tests for IP Operations. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/IPOverlapTest.cs`
Unit tests for IP Overlap. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/LockTest.cs`
Unit tests for Lock. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ManagedIdStringTest.cs`
Unit tests for Managed Id String. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/Mocks/Mock.cs`
Mock implementation for Mock used in unit tests. It provides deterministic behavior and isolates external dependencies.

### `roles/tests-unit/files/FWO.Test/Mocks/MockApiConnection.cs`
Mock implementation for Api Connection used in unit tests. It provides deterministic behavior and isolates external dependencies.

### `roles/tests-unit/files/FWO.Test/Mocks/MockLogger.cs`
Mock implementation for Logger used in unit tests. It provides deterministic behavior and isolates external dependencies.

### `roles/tests-unit/files/FWO.Test/Mocks/MockReportCompliance.cs`
Mock implementation for Report Compliance used in unit tests. It provides deterministic behavior and isolates external dependencies.

### `roles/tests-unit/files/FWO.Test/Mocks/MockReportComplianceDiff.cs`
Mock implementation for Report Compliance Diff used in unit tests. It provides deterministic behavior and isolates external dependencies.

### `roles/tests-unit/files/FWO.Test/Mocks/MockReportRules.cs`
Mock implementation for Report Rules used in unit tests. It provides deterministic behavior and isolates external dependencies.

### `roles/tests-unit/files/FWO.Test/Mocks/MockRuleTreeBuilder.cs`
Mock implementation for Rule Tree Builder used in unit tests. It provides deterministic behavior and isolates external dependencies.

### `roles/tests-unit/files/FWO.Test/ModellingConnectionHandlerTest.cs`
Unit tests for Modelling Connection Handler. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ModellingHandlerTest.cs`
Unit tests for Modelling Handler. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ModellingHandlerTestApiConn.cs`
Test API connection stub for Modelling Handler. Supplies canned responses to isolate unit test behavior.

### `roles/tests-unit/files/FWO.Test/ModellingVarianceAnalysisTest.cs`
Unit tests for Modelling Variance Analysis. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ModellingVarianceAnalysisTestApiConn.cs`
Test API connection stub for Modelling Variance Analysis. Supplies canned responses to isolate unit test behavior.

### `roles/tests-unit/files/FWO.Test/NetworkZoneServiceTest.cs`
Unit tests for Network Zone Service. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ReportComplianceTest.cs`
Unit tests for Report Compliance. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/ReportRulesTest.cs`
Unit tests for Report Rules. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/RuleTreeBuilderTest.cs`
Unit tests for Rule Tree Builder. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/RuleViewDataTest.cs`
Unit tests for Rule View Data. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/SCTicketTest.cs`
Unit tests for SC Ticket. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/SchedulerTest.cs`
Unit tests for Scheduler. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/SchedulerTestApiConn.cs`
Test API connection stub for Scheduler. Supplies canned responses to isolate unit test behavior.

### `roles/tests-unit/files/FWO.Test/SimulatedApiConnection.cs`
Simulated test component for Api Connection. Used to provide predictable inputs for unit tests.

### `roles/tests-unit/files/FWO.Test/SimulatedReport.cs`
Simulated test component for Report. Used to provide predictable inputs for unit tests.

### `roles/tests-unit/files/FWO.Test/SimulatedSCClient.cs`
Simulated test component for SC Client. Used to provide predictable inputs for unit tests.

### `roles/tests-unit/files/FWO.Test/SimulatedUserConfig.cs`
Simulated test component for User Config. Used to provide predictable inputs for unit tests.

### `roles/tests-unit/files/FWO.Test/StringExtensionsTest.cs`
Unit tests for String Extensions. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/TestDataGeneratorTest.cs`
Unit tests for Data Generator. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/TestHelper.cs`
Unit test helper for Test Helper. Supports the test suite with shared behaviors.

### `roles/tests-unit/files/FWO.Test/TestInitializer.cs`
Unit test helper for Test Initializer. Supports the test suite with shared behaviors.

### `roles/tests-unit/files/FWO.Test/Tools/CustomAssert.cs`
Test utility for Custom Assert scenarios. Used by unit tests to build data or assert shared behaviors.

### `roles/tests-unit/files/FWO.Test/Tools/TestDataGenerator.cs`
Test utility for Data Generator scenarios. Used by unit tests to build data or assert shared behaviors.

### `roles/tests-unit/files/FWO.Test/UiRsbLinkTest.cs`
Unit tests for Ui Rsb Link. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/UiRsbTestApiConn.cs`
Test API connection stub for Ui Rsb. Supplies canned responses to isolate unit test behavior.

### `roles/tests-unit/files/FWO.Test/UiZoneMatrixTest.cs`
Unit tests for Ui Zone Matrix. Covers expected behavior and edge cases for the component.

### `roles/tests-unit/files/FWO.Test/UrlSanitisationTest.cs`
Unit tests for Url Sanitisation. Covers expected behavior and edge cases for the component.

## Integration tests

### `roles/tests-integration/files/api/test_api.sh`
Shell script that runs api integration checks. Used to exercise API or importer behavior in test environments.

### `roles/tests-integration/files/auth/config.ldif`
LDAP test data (config) used by integration tests. Seed entries for authentication or role mapping scenarios.

### `roles/tests-integration/files/auth/roles.ldif`
LDAP test data (roles) used by integration tests. Seed entries for authentication or role mapping scenarios.

### `roles/tests-integration/files/importer/CP-R8x/iso_cp_r8x_api_generate_testdata.py`
Python helper for iso cp r 8 x api generate data integration testing. Generates or modifies test data for end-to-end runs.

### `roles/tests-integration/files/importer/CP-R8x/iso_cp_r8x_api_get_layer_names.py`
Python helper for iso cp r 8 x api get layer names integration testing. Generates or modifies test data for end-to-end runs.

### `roles/tests-integration/files/importer/CP-R8x/unused_create_csv_sting.sh`
Legacy shell helper for unused create csv sting integration scenarios. Kept for reference and not used in the current test flow.

### `roles/tests-integration/files/importer/config_changes/changeRule.py`
Python helper for change Rule integration testing. Generates or modifies test data for end-to-end runs.

### `roles/tests-integration/files/importer/config_changes/enlarge_rule.py`
Python helper for enlarge rule integration testing. Generates or modifies test data for end-to-end runs.

### `roles/tests-integration/files/importer/config_changes/write_date_to_comment.py`
Python helper for write date to comment integration testing. Generates or modifies test data for end-to-end runs.

### `roles/tests-integration/files/tenant_networks/create_tenant_network_data.py`
Python helper for create tenant network data integration testing. Generates or modifies test data for end-to-end runs.

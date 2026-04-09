namespace FWO.Data
{
    public static class ChangeLogActions
    {
        public const string ManualFamily = "manual_";
        public const string PromptedFamily = "prompted_";
        public const string PromptDismissedFamily = "prompt_dismissed_";
        public const string AutodiscoveryPromptFamily = "autodiscovery_prompt_";
        public const string MiddlewareMatrixImportFamily = "middleware_matrix_import_";

        public const string ManualMatrixCreate = ManualFamily + "matrix_create";
        public const string ManualMatrixSoftDelete = ManualFamily + "matrix_soft_delete";

        public const string ManualManagementCreate = ManualFamily + "management_create";
        public const string ManualManagementUpdate = ManualFamily + "management_update";
        public const string ManualManagementDelete = ManualFamily + "management_delete";

        public const string AutodiscoveryPromptManagementCreate = AutodiscoveryPromptFamily + "prompt_management_create";
        public const string AutodiscoveryPromptManagementDelete = AutodiscoveryPromptFamily + "prompt_management_delete";
        public const string AutodiscoveryPromptManagementReactivate = AutodiscoveryPromptFamily + "management_reactivate";

        public const string AutodiscoveryPromptGatewayCreate = AutodiscoveryPromptFamily + "gateway_create";
        public const string AutodiscoveryPromptGatewayDelete = AutodiscoveryPromptFamily + "gateway_delete";
        public const string AutodiscoveryPromptGatewayReactivate = AutodiscoveryPromptFamily + "gateway_reactivate";

        public const string PromptedManagementCreate = PromptedFamily + "management_create";
        public const string PromptedManagementDelete = PromptedFamily + "management_delete";
        public const string PromptedManagementDisable = PromptedFamily + "management_disable";
        public const string PromptedManagementReactivate = PromptedFamily + "management_reactivate";

        public const string PromptedGatewayCreate = PromptedFamily + "gateway_create";
        public const string PromptedGatewayDelete = PromptedFamily + "gateway_delete";
        public const string PromptedGatewayDisable = PromptedFamily + "gateway_disable";
        public const string PromptedGatewayReactivate = PromptedFamily + "gateway_reactivate";

        public const string PromptDismissedManagementCreate = PromptDismissedFamily + "management_create";
        public const string PromptDismissedManagementDelete = PromptDismissedFamily + "management_delete";
        public const string PromptDismissedManagementReactivate = PromptDismissedFamily + "management_reactivate";

        public const string PromptDismissedGatewayCreate = PromptDismissedFamily + "gateway_create";
        public const string PromptDismissedGatewayDelete = PromptDismissedFamily + "gateway_delete";
        public const string PromptDismissedGatewayReactivate = PromptDismissedFamily + "gateway_reactivate";

        public const string MiddlewareMatrixImportCreate = MiddlewareMatrixImportFamily + "import_create";
    }

    public static class ChangeLogOrigins
    {
        public const string UiSettings = "ui_settings";
        public const string UiAutodiscovery = "ui_autodiscovery";
        public const string Autodiscovery = "autodiscovery";
        public const string ImportZoneMatrixData = "import_zone_matrix_data";
    }
}

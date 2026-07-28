DELETE_IMPORT: str = """
mutation deleteImport($importId: bigint!) {
    delete_import_control(where: {control_id: {_eq: $importId}}) {
        affected_rows
    }
}
"""

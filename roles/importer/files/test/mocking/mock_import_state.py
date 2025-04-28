class MockImportState:
    previous_calls = []

    def call(self, query, queryVariables):
        full_query = {"query": query, "variables": queryVariables}
        self.previous_calls.append(full_query)

from typing import Any

mock_mgm_details: dict[str, Any] = {
    "data": {
        "management": [
            {
                "id": 12,
                "name": "Checkpoint_MDS",
                "hostname": "192.168.10.5",  # Needs to NOT have "://" to trigger decryption logic
                "ssh_port": 22,
                # Required for: api_call_result["data"]["management"][0]["import_credential"]["secret"]
                "import_credential": {"id": 45, "username": "api_admin", "secret": "encrypted_secret_string"},
                # Optional: The code iterates this if present
                "subManagers": [
                    {
                        "id": 101,
                        "name": "CMA_London",
                        "hostname": "192.168.10.6",
                        "import_credential": {"id": 46, "secret": "encrypted_sub_secret"},
                    }
                ],
            }
        ]
    },
    "deviceType": {"id": 12, "name": "Checkpoint MDS"},
}

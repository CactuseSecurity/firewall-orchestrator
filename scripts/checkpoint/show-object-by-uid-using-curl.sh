curl --request POST \
  --url https://[checkpoint-manager]/web_api/login \
  --header 'Content-Type: application/json' \
  --cookie Session=Login \
  --data '{
		"user": "[Benutzername]",
		"password": "[PWD]"
}'

curl --request POST \
  --url https://[checkpoint-manager]/web_api/show-object \
  --header 'Content-Type: application/json' \
  --header 'x-chkp-sid:   [SID-Ausgabe vom vorherigen Befehl]' \
  --cookie Session=Login \
  --data '{
	"uid": "[UID des Objekts]",
	"details-level": "full"
}'

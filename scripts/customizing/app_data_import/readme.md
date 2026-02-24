
# Customizing Script for Importing Application Data into FWO

## Importing from normalized CSV files

We provide a customizing script `get_owner_data_from_normalized_csvs.py` that reads owner/application data and related IPs from normalized CSV exports and converts them into the JSON format expected by the App Data Import.

### Preparation

#### Files

Copy the configuration file `customizingConfig.json` from the `$FWORCH/scripts/customizing/modelling` directory to the `/etc/fworch/secrets` directory and ensure only the fworch user can read it (`chmod 600`).

#### FWO Configuration 

##### Custom config file

Adjust `/etc/fworch/secrets/customizingConfig.json` as follows:

- `gitRepo`, `gitUser`, `gitPassword`: Git repository URL (without protocol), user, and password that host the CSV exports.
- `csvOwnerFilePattern`: regex for owner/app metadata files (e.g., `NeMo_..._meta.csv`).
- `csvAppServerFilePattern`: regex for server/IP files (e.g., `NeMo_..._IP.*?.csv`).
- `csvOwnerColumnPatterns` (optional): JSON object with regexes for the owner CSV headers (`name`, `app_id`, `owner_tiso`, `owner_kwita`); defaults match `col: Name`, `col: Alfabet-ID`, `TISO`, and `kwITA`.
- `csvIpColumnPatterns` (optional): JSON object with regexes for the IP CSV headers (`app_id`, `ip`); defaults match `col: Alfabet-ID` and `col: IP`.
- `ldapPath`: DN template containing `{USERID}` to expand main users.

You can bypass Git and read from a local folder by providing `--import_from_folder` when running the script.

#### Settings via UI

In the FWORCH Web UI, go to Settings - Further Settings - Modelling and enter the full path to the script (leave out the extension) in "Path and Name of App data import", e.g.:
`/usr/local/fworch/scripts/customizing/app-data-import/get_owner_data_from_normalized_csvs`

Then click "Add". You can configure multiple import sources the same way.

## Importing from Tufin RLM

We provide a customizing script that fetches all owner/application data via the Tufin RLM API and converts the data into the expected normalized json format for the App Data Import.
Additionally we provide a script to convert Network Object data (Areas) from a .csv-File to the json format expected by the 
Subnet Data Import.

### Preparation

#### Files

In order to protect your credentials, copy the configuration file customizingConfig.json from the $FWORCH/scripts/customizing/modelling directory to the /etc/fworch/secrets directory and make sure only the fworch user has access (0x600).

#### FWO Configuration 

##### Custom config file

Adjust the settings in the config file /etc/fworch/secrets/customizingConfig.json as follows:

- username should be an admin user for Tufin RLM
- under ldapPath enter your standard LDAP path and make sure the user id parameter [USERID] is at the right place
- finally enter the path to your Tufin installation, e.g. https://tufin.cactus.de/ without any path informatin just ending with a "/"
- for the Subnet Data import enter the path to the .csv-File

```json
{
    "username": "user1",
    "password": "pwd1",
    "ldapPath": "CN={USERID},OU=Benutzer,DC=CACTUS,DC=DE",
    "apiBaseUri": "https://tufin.cactus.de/",
    "subnetData": "/usr/local/fworch/scripts/customizing/modelling/NwObjDataOrigExample.csv"
}
```

For testing purposes (e.g. if you do not have access to the RLM API yet), you may also specify a local JSON file for importing as follows:

    "apiBaseUri": "/usr/local/fworch/scripts/customizing/modelling/sampleOwnerDataOrig.json"


#### Settings via UI

Go the the Web UI of FWORCH and login as an admin user. Go to Settings - Further Settings - Modelling and enter the full path of the appropriate script in directory $FWORCH/scripts/customizing/modelling/ (note: leave out the extension!) into the field "Path and Name of App data import". For example for importing from RLM this would be:
/usr/local/fworch/scripts/customizing/modelling/getOwnersFromTufinRlm

Then click the "Add" button.

Note: you can enter several scripts for multiple import sources

With the same logic a script path for the Subnet Data import can be entered in "Path and Name of Subnet data import".
An example script is delivered at $FWORCH/scripts/customizing/modelling/convertNwObjDataExample.py

Then enter suitable scheduling information and click "Save".

## Open Issues

- write alerts when import fails

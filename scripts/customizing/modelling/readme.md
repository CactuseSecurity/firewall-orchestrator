
# Customizing Script for Importing Application Data from Tufin RLM

We provide a customizing script that fetches all owner/application data via the Tufin RLM API and converts the data into the expected normalized json format for the App Data Import.
Additionally we provide a script to convert Network Object data (Areas) from a .csv-File to the json format expected by the 
Subnet Data Import.

## Preparation

### Files

In order to protect your credentials, copy the configuration file customizingConfig.json from the $FWORCH/scripts/customizing/modelling directory to the /etc/fworch/secrets directory and make sure only the fworch user has access (0x600).

### FWO Configuration 

#### Custom config file

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

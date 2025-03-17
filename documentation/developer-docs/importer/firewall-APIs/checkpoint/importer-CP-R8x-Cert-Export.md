# How to export certificates from Check Point R8x management server

Necessary for importing into FW-Orch importer to get rid of cert warnings

Source: https://sc1.checkpoint.com/documents/R81/WebAdminGuides/EN/CP_R81_CLI_ReferenceGuide/Topics-CLIG/MDSG/fwm-printcert.htm?tocpath=Multi-Domain%20Security%20Management%20Commands%7Cfwm%7C_____10


Example:

    fwm printcert -ca internal_ca -x509 out.cert -p >cp.crt
/*

    plan "Tufin SecureChange Request Module" - TUREM

    User Interface
        - modeller user can choose to request the current state of modelling for one app/owner
        - a button can be used within the modeller top level menu to do so
        - modelling which has already been requested and that which has not need to be displayed in NeMo so that each can be easily identified

    Automatic steps:
        - TUREM needs to store the last modelling state that was requested in order to be able to request the differences
        - database structure needs to be defined - if possible find a simple model which does not look like the current FWO change tracking
            (work with a flag - modelled/requested)
        - TUREM needs to request the creation of objects (network, service) as well as access requests based on these objects
        - TUREM will not take the rulebase of the actual firewalls into account - this will be done by SC
        - specifically will changes between two TUREM requests (not requested via TUREM) not be taken into account

    Open decisions/tests
    - do we also need to get feedback on the implementation state of the SC ticket? If so, what to do with it?
      - at least we should store the tufin ticket numbers in NeMo for reference
    - can we always just create a single SC ticket or do we need multiple tickets?
      - probably SC cannot deal with order of tasks so that in the first task objects are requested which are then Nused in the same ticket within an AR
      - if we need multiple SC tickets, we need to be prepared to store multiple ticket numbers in NeMo for a single TUREM request
    - for non-initial requests: do we have to create change requests or do we simply request the whole modelled rulebase?
      - same question for changes to (modelled) objects
      - what about changes to basic objects like NAs - do we requests these of just assume that they already have been implemented?
        - where to draw the line? 

    Preparations
    - get a technical user with SC create ticket rights on Tufin STEST system

    Not customer related:
        - develop in parallel: internal request module which requests the changes within the FWO request module

*/

insert into config (config_key, config_value, config_user) VALUES ('extTicketSystems', '[{"Url":"","TicketTemplate":"{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"@@WORKFLOW_NAME@@\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[@@TASKS@@]}}}}]}}}","TasksTemplate":"{\"@xsi.type\":\"multi_access_request\",\"name\":\"GewünschterZugang\",\"read_only\":false,\"access_request\":{\"order\":\"AR1\",\"verifier_result\":{\"status\":\"notrun\"},\"use_topology\":true,\"targets\":{\"target\":{\"@type\":\"ANY\"}},\"users\":{\"user\":@@USERS@@},\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"action\":\"@@ACTION@@\",\"labels\":\"\"}},{\"@xsi.type\":\"text_area\",\"name\":\"Grund für den Antrag\",\"read_only\":false,\"text\":\"@@REASON@@\"},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Regel Log aktivieren?\",\"selection\":\"@@LOGGING@@\"},{\"@xsi.type\":\"date\",\"name\":\"Regel befristen bis:\"},{\"@xsi.type\":\"text_field\",\"name\":\"Anwendungs-ID\",\"text\":\"@@APPID@@\"},{\"@xsi.type\":\"checkbox\",\"name\":\"Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt\",\"value\":@@COM_DOCUMENTED@@},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)\",\"selection\":\"Nein\"}"}]', 0) ON CONFLICT DO NOTHING;

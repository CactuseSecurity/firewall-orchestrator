# Customizing Script using the FWO API

If you want to customize via API you can use the customizing.py script in this directory.
Note: Do not change this script as it will be overwritten during next upgrade.

To customize workflow state matrices: The Master state matrix is not overwritten by default. If you want to use the same matrix as one of the task types - e.g. NewInterface - (and the Master state matrix is not already in use otherwise!), you can copy the value of the config Item of the task type state matrix (here: reqNewIntStateMatrix) to the config item of the master state matrix (reqMasterStateMatrix) in the config table. E.g:

    "config": [
        {
            "config_key": "reqMasterStateMatrix",
            "config_value": "{\"config_value\":{\"request\":{\"matrix\":{\"0\":[0,49,620]},\"derived_states\":{\"0\":0},\"lowest_input_state\":0,\"lowest_start_state\":0,\"lowest_end_state\":49,\"active\":true},\"approval\":{\"matrix\":{\"49\":[60],\"60\":[60,99,610],\"99\":[99],\"610\":[610]},\"derived_states\":{\"49\":49,\"60\":60,\"99\":99,\"610\":610},\"lowest_input_state\":49,\"lowest_start_state\":60,\"lowest_end_state\":99,\"active\":false},\"planning\":{\"matrix\":{\"99\":[110],\"110\":[110,120,130,149],\"120\":[120,110,130,149],\"130\":[130,110,120,149,610],\"149\":[149],\"610\":[610]},\"derived_states\":{\"99\":99,\"110\":110,\"120\":110,\"130\":110,\"149\":149,\"610\":610},\"lowest_input_state\":99,\"lowest_start_state\":110,\"lowest_end_state\":149,\"active\":false},\"verification\":{\"matrix\":{\"149\":[160],\"160\":[160,199,610],\"199\":[199],\"610\":[610]},\"derived_states\":{\"149\":149,\"160\":160,\"199\":199,\"610\":610},\"lowest_input_state\":149,\"lowest_start_state\":160,\"lowest_end_state\":199,\"active\":false},\"implementation\":{\"matrix\":{\"205\":[205,249],\"49\":[210],\"210\":[610,210,249]},\"derived_states\":{\"205\":205,\"49\":49,\"210\":210},\"lowest_input_state\":49,\"lowest_start_state\":205,\"lowest_end_state\":249,\"active\":true},\"review\":{\"matrix\":{\"249\":[249,205,299]},\"derived_states\":{\"249\":249},\"lowest_input_state\":249,\"lowest_start_state\":249,\"lowest_end_state\":299,\"active\":true},\"recertification\":{\"matrix\":{\"299\":[310],\"310\":[310,349,400],\"349\":[349],\"400\":[400]},\"derived_states\":{\"299\":299,\"310\":310,\"349\":349,\"400\":400},\"lowest_input_state\":299,\"lowest_start_state\":310,\"lowest_end_state\":349,\"active\":false}}}",
            "config_user": 0
        }
    ]

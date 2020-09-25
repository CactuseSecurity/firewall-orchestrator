# Testing

Structure: <https://martinfowler.com/articles/practical-test-pyramid.html#TheTestPyramid>

Tests are performed in the test role

These are either executed at the end of the install process or separately using the following command:

    ansible-playbook -i inventory test.yml -K


To ensure that all tests are run, an error does not interrupt the playbook.
Instead you can use the following command to only display errors:

    tim@ubu18test:~/firewall-orchestrator$ ansible-playbook -i inventory test.yml -K | grep -2 ERROR
    SUDO password: 

    TASK [test : auth get jwt test output] ***************************************************************************************************************************
    ok: [fworch-srv] => {
        "msg": "ERROR unexpected jwt test result (not equal 'OK'): "
    }

    --
    TASK [test : anonymous api access with JWT output] ***************************************************************************************************************
    ok: [fworch-srv] => {
        "msg": "ERROR unexpected version test result (does not contain text_msg_id): {\"errors\":[{\"extensions\":{\"path\":\"$\",\"code\":\"invalid-jwt\"},\"message\":\"Could not verify JWT: JWTExpired\"}]}"
    }

    tim@ubu18test:~/firewall-orchestrator$

# Testing

## Intro

Universal test structure: <https://martinfowler.com/articles/practical-test-pyramid.html#TheTestPyramid>

## Ansible integration testing
- <https://docs.ansible.com/ansible/latest/reference_appendices/test_strategies.html>
- <https://www.ansible.com/blog/five-questions-testing-ansible-playbooks-roles>
- <https://docs.ansible.com/ansible/latest/dev_guide/testing_units.html>
- <https://docs.ansible.com/ansible/latest/dev_guide/testing_units_modules.html#testing-units-modules>
- <https://pypi.org/project/placebo/>

## Documentation
Tests are performed in the test role. These are either executed at the end of the install process or separately using the following command:

    ansible-playbook site.yml -K --tags test
    
To ensure that all tests are run, an error does not interrupt the playbook.
Instead you can use the following command to only display errors:

    tim@ubu18test:~/firewall-orchestrator$ ansible-playbook site.yml -K --tags test | grep -2 ERROR
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


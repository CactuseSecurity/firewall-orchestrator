# remote development using remote-ssh

This enables us to develop, run and debug everything remotely on a single linux machine of our choice.

See <https://code.visualstudio.com/docs/remote/ssh> for instructions how to set this up.

After setup we need to 
- clone our fork repo on the remote machine
- install fworch
- stop the services we want to debug (middleware, ui)
- start the services remotely in vs (code) for debugging

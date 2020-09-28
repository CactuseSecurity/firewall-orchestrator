# how to use ansible vaults

- a vault is an encrypted yaml file
- it can be used to store sensitive information like passwords

## create

create a new vault password-vault.yml with

  ansible-vault create password-vault.yml
  
you have to provide a password to encrypt the vault

## edit

edit the vault password-vault.yml with

  ansible-vault edit password-vault.yml
  
## reference

to reference the content of the vault in the entire playbook use

  - hosts: all
    vars_files:
      - password-vault.yml
    roles:
      - common
      

## Project Structure

The Firewall Orchestrator automates firewall governance end-to-end. It inventories firewalls and firewall managements by normalizing vendor-specific device data to an abstracted data model. The normalized data is stored in a central database and accessible via an API. The middleware exposes policy, auth backed by LDAP, and workflow services that drive the UI and importer pipelines. Deployment is handled by the Ansible installer, which deploys databases, API, middleware, UI, importers, LDAP, and supporting test suites.

- **Installer (Ansible)**  
  - Location: `roles/installer`  
  - Provisions infrastructure, configures dependencies, and orchestrates deployment across the stack.

- **Firewall Importer (Python)**  
  - Location: `roles/importer`  
  - Normalizes firewall and firewall-manager configuration data from multiple vendors into the platform's common model.  
  - Backed by importer modules tailored to each manufacturer.

- **Data API (Hasura + PostgreSQL)**  
  - Location: `roles/database` (PostgreSQL provisioning), `roles/api` (Hasura GraphQL).  
  - Maintains normalized configuration and operational data in PostgreSQL.  
  - Exposes a GraphQL endpoint for read/write access and reporting queries.

- **Middleware Server (C# / ASP.NET)**  
  - Location: `roles/middleware`  
  - Presents REST services for authentication, authorization (role/group/tenant), scheduling, rule compliance checks, and change-request workflows.  
  - Generates JWTs and brokers communication between UI, data layer, and automation routines.

- **UI (Blazor / C#)**  
  - Location: `roles/ui`  
  - Delivers user-facing workflows for importing devices, rule and other reporting, managing change requests, scheduling compliance checks, and (re)certifying firewall rules.

- **Shared Libraries (C#)**
  - Location: `roles/lib`  
  - Provides common .NET assemblies consumed by both the middleware and UI layers.

- **Authentication Backend (OpenLDAP)**  
  - Location: `roles/openldap-server`  
  - Supplies internal LDAP directory services with optional federation to external LDAP providers.

- **Testing Suites**  
  - Unit tests (C#): `roles/tests-unit`  
  - Integration tests (Importer and end-to-end flows): `roles/tests-integration`

## Codex Workflow Expectations

1) **Identify relevant sources**
   - Identify the relevant projects (and files) to modify under the ones listed above.
   - Check out the relevant projects structure documentation which you can find under `agents/project-structure/x.md` where `x` is `authentication` / `csharp-libs` / `data` / `importer` / `installer` / `middleware` / `tests` / `ui`.

2) **Plan required steps**
   - Outline the minimal changes and checks needed to satisfy the request.
   - Identify affected components, dependencies, and likely tests or docs updates.
   - Call out unknowns or risks and how you will validate them.
   - In the following steps always confirm the current state matches the plan or revise the plan if needed.

3) **Apply changes**
   - Implement changes based on the plan in small, verifiable steps.
   - Keep changes scoped; avoid unrelated refactors.
   - Update or add tests and documentation as required.
   - Re-check requirements, side effects, and whether any steps need adjustment.

4) **Formatting & Linting**
   - Run the linter / formatter using `dotnet format roles/FWO.sln` in case C# code was changed.
   - Run the ruff linter / formatter using `ruff check --fix` and `ruff format` in case Python code was changed.

5) **Building**
  - Build the C# project using `dotnet build --configuration Debug roles/FWO.sln` in case C# code was changed. 
  - Identify syntactical errors in Python using `python -m compileall -q .` in case Python code was changed.

6) **Unit testing**
   - Always author unit tests for new code you introduce. In case of C# store them under `roles/tests-unit/files/FWO.Test/`. In case of Python store them under `roles/importer/files/importer/test`.
   - Run the unit tests with `dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj` after any C# code changes.
   - Run the unit tests with `pytest -q roles/importer/files/importer/test` after any Python code changes.

7) **Installer verification**
   - Execute `ansible-playbook site.yml -e "testkeys=yes" -b` if code changes were made to confirm the installer path succeeds end-to-end. The installation can take up to 10 minutes.
   - Do not execute the install unless you a certain to be on a virtual machine. Verifiy this using `systemd-detect-virt`.
   - `installation_mode` accepts `new`, `upgrade`, or `uninstall`
   - Override OS upgrade checks only when necessary via `-e force_install=true`.

If any error occurs in the validation steps 4, 5, 6, or 7, return to step 2 and plan the changes needed to fix the failure.

## Coding & Contribution Standards

- **General style**  
  - Follow `CODING_GUIDELINES.md`: self-explanatory CamelCase names, class names starting uppercase, constants prefixed with `k`.
  - Keep methods within 100 lines, complexity (`if`/`case`/`foreach` count) <= 10, parameters per method <= 7, and source files <= 1000 lines.  
  - Avoid magic numbers, prefer lists to arrays, limit recursion (default max 100), and remove dead/commented code.

- **C# specifics**  
  - Prevent null-reference issues and add XML documentation comments (`///`) ahead of methods.

- **Frontend specifics**  
  - Close all tags, avoid inline styles and `!important`, keep components SOLID-compliant, and rely on the Bootstrap grid for responsiveness.

- **Commits & branching**  
  - Use Conventional Commits (`type[:scope]: description`), keep summaries <= 50 characters, imperative voice, no trailing period.  
  - Work via fork-and-branch, sync with upstream before opening pull requests.

- **Documentation**  
  - Store new documentation under `documentation/` following the existing structure.
  - Extend / modify the documentation of the short summary project file structure for agents under `agents/project-structure/*.md` for each file where changes were made, if necessary. The documentation should capture the general essence of each relevant code file in 2-4 sentences as a quick reference for agents. Follow the existing structure.
  - Add or update help content in `roles/ui/files/Pages/Help/` for every UI feature change or new functionality so documentation stays aligned.

- **Security disclosures**  
  - Report vulnerabilities privately. Never commit secrets.

## Installer & Versioning

- Avoid usage of plays with module `run` and `command`; as well as Galaxy modules unless explicitly reuired. Prefer standard Ansible modules.
- Bump product versions whenever database schemas, LDAP structure, or major features change (`documentation/developer-docs/installer/upgrading.md`).
- Update `documentation/revision-history-*.md`, adjust `inventory/group_vars/all.yml`, and add idempotent upgrade scripts (under `roles/database/files/upgrade/<version>.sql`). Do not modify the old ones.
- The installer must support Debian version >= 12 and Ubuntu version >= 20.04. Make sure any changes to the installer consider this.

## Importer Module Practices

- Follow `documentation/developer-docs/importer/readme.md` when onboarding new firewall types: update `roles/database/files/sql/creation/fworch-fill-stm.sql`, create version-specific module directories under `roles/importer/files/importer`.  
- Use the entry point `roles/importer/files/importer/import_mgm.py` and emit normalized configs as defined in `documentation/developer-docs/importer/FWO-import-api.md`.

## Customization & Operations

- Tenant-specific automation lives in `roles/finalize/tasks/main.yml` and Python helpers under `scripts/customizing/api/`; review them before adding customer-specific behaviour.  
- Operations guidance: follow backup, logging, and monitoring suggestions in `documentation/operations.md`, and keep `/var/log/fworch` usage monitored.

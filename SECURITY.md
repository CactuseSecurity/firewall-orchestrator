# Security Policy

## Reporting a Vulnerability

We’re extremely grateful for security researchers and users that report vulnerabilities to the Firewall Orchestrator Community. All reports are thoroughly investigated by the Firewall Orchestrator team.

In case you discover a security issue/vulnerability, please contact security@cactus.de with all the details, attaching necessary information if possible.

**Do not open an issue!**

### When Should I Report a Vulnerability?

- You think you have discovered a potential security vulnerability in Firewall Orchestrator.
- You are unsure how a vulnerability affects Firewall Orchestrator.

### When Should I NOT Report a Vulnerability?

- You need help with tuning Firewall Orchestrator components for security.
- You need help applying security related updates.
- Your issue is not security related.

## Security Vulnerability Response

Each report is acknowledged and analyzed by the project's maintainers and the security team within 3 working days. 

The reporter will be kept updated at every stage of the issue's analysis and resolution (triage -> fix -> release).

## Public Disclosure Timing

A public disclosure date is negotiated by the security team and the bug submitter. We prefer to fully disclose the bug as soon as possible once a user mitigation is available. It is reasonable to delay disclosure when the bug or the fix is not yet fully understood, the solution is not well-tested, or for vendor coordination. The timeframe for disclosure is from immediate (especially if it's already publicly known) to a few weeks. We expect the time-frame between a report to a public disclosure to typically be in the order of 7 days. The Firewall Orchestrator maintainers and the security team will take the final call on setting a disclosure date.

## Security Advisories and CVEs

Fixed vulnerabilities are published as [GitHub security advisories](https://github.com/CactuseSecurity/firewall-orchestrator/security/advisories) of this repository. Each advisory names the affected versions, the first fixed version and the weakness (CWE).

Firewall Orchestrator is installed from this repository, not through a package registry. Its advisories therefore use the ecosystem `other` with the package name `Firewall Orchestrator`, and Dependabot does not alert on them. Use the release notes and the advisory list to learn about fixes (see *Informing Users*).

We request a CVE ID for an advisory (GitHub assigns it as CVE Numbering Authority) when all of the following apply:

- A released version is affected.
- Someone below admin or root level can trigger it: an ordinary Firewall Orchestrator role, anyone with network access to a Firewall Orchestrator host, whoever controls an imported firewall configuration, or someone with a network position between Firewall Orchestrator and a connected system.
- It affects confidentiality, integrity or availability, not only robustness.
- It is rated Medium or higher.

We do not request a CVE for:

- hardening findings rated Low or Info (each security review collects them in one advisory),
- defects without an attack path,
- code that was never part of a release,
- CI and development tooling.

The CVE ID is requested before the advisory is published, so that the advisory goes public with it. Advisories with a CVE also carry a CVSS vector.

## Informing Users

The release notes of every release that fixes a vulnerability contain a *Security* section. It lists the advisory IDs (and CVE IDs), their severity, and every action admins have to take in addition to the upgrade.

We also inform known operators and support customers directly when at least one of the following applies:

- The vulnerability is rated High or Critical, or it can be exploited without authentication on a default installation.
- Upgrading alone does not fix it, and admins have to act (e.g. change settings or register keys).
- It is exploited or publicly known before a fix is available.

For High and Critical vulnerabilities, this notice is sent once the fixed release is available, a few days before the advisory is published, so that operators can upgrade first. One notice covers all advisories of a release.

To be notified of new releases, watch this repository with *Watch > Custom > Releases*.

(Some sections have been inspired and adapted from [https://github.com/kubernetes/website/blob/master/content/en/docs/reference/issues-security/security.md](https://github.com/kubernetes/website/blob/master/content/en/docs/reference/issues-security/security.md)).

## Supported Versions

Security fixes are released for the latest minor version of the newest major version only. To receive them, upgrade to the latest release.

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :x:                |
| 2.x     | :x:                |
| 3.x     | :x:                |
| 4.x     | :x: (until 2022-12-31) |
| 5.x     | :x: (until 2023-12-31) |
| 6.x     | :x: (until 2024-12-31) |
| 7.x     | :x: (until 2025-12-31) |
| 8.x     | :x: (until 2026-01-13, last release 8.9.6) |
| 9.x     | :white_check_mark: (latest 9.x release) |

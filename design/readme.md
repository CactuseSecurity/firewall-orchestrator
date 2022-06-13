# Design topics

## competition

https://www.networkstraining.com/best-firewall-management-software-tools

## Design goals

- simple & enjoyable development in an easily expandable team
- Open Source
- High code quality (documentation, tests, code readability, coding best practices ...)
- Strict modularisation (among others using API), avoid logic in client as muchas possible
- secure code, include tenant concept, RBAC
- modern & "non-aging" GUI
- supportability (easy update mechanisms, build updater functionality?)

## Design, method, architecture decisions

- language:

  - develoopment in English
  - GUI language: parallel English, German, allow for others

- use agile approach (Rapid Prototyping, trial & error, MuP)

## Tool decisions

- using GitHub.com for
  - version control
  - project management
  - test automation

- PostgreSQL
- API: GraphQL

  - using hasura

- Client

  - browser-only

## Functional requirements (high-Level)

- low-cost alternative to core functionality of competition (Tufin, Algosec, Skybox, Firemon)
- fullfil regulatory requirements (documentation of config changes, recertification of config)
- "network CMDB"
- do not include high risk functionality (e.g. write config changes to firewalls) in core product at first
- offer full API for automation purposes
- include granular role-basd access model

## Architecture: "encapsulate everything"

- API

  - API modules (<https://medium.com/the-guild/why-is-true-modular-encapsulation-so-important-in-large-scale-graphql-projects-ed1778b03600>)
  - no direct DB access without API

- UI

  - UI display and data methods

- first impression, see <https://fwodemo.cactus.de>

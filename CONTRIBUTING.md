# Contributing to Firewall Orchestrator

_First_: if you feel insecure about how to start contributing, feel free to ask us (see [contact page](https://fwo.cactus.de/en/kontakt/)). You can also just go ahead with your contribution and we'll give you feedback. Don't worry - the worst that can happen is that you'll be politely asked to change something. We appreciate any contributions, and we don't want a wall of rules to stand in the way of that.

However, for those individuals who want a bit more guidance on the best way to contribute to the project, read on. This document will cover what we're looking for. By addressing the points below, the chances that we
can quickly merge or address your contributions will increase.

## Table of contents

[1. Code of conduct ](#code-of-conduct)

[2. Repo overview ](#overview)

[3. First time contributors welcome! ](#first-timers)

[4. Areas for contributing ](#areas)

[5. Ways of contributing ](#ways)

[6. Commit messages ](#commit-messages)

[7. Coding Guidelines ](#coding-guidelines)

<a name="code-of-conduct"></a>

## 1. Code of conduct

Please follow our [Code of conduct](code-of-conduct.md) in the context of any contributions made to Hasura.

<a name="overview"></a>

## 2. Repo overview

[Firewall Orchestrator](https://github.com/CactuseSecurity/firewall-orchestrator) has the following contributing guides:

For all contributions, a CLA (Contributor License Agreement) needs to be signed [here](https://cla-assistant.io/CactuseSecurity/firewall-orchestrator) before (or after) the pull request has been submitted. A bot will prompt contributors to sign the CLA via a pull request comment, if necessary.

<a name="first-timers"></a>

## 3. First time contributors welcome!

We appreciate first time contributors and we are happy to assist you in getting started. In case of questions, just reach out to us!

You find all issues suitable for first time contributors [here](https://github.com/CactuseSecurity/firewall-orchestrator/issues?q=is%3Aopen+is%3Aissue+label%3A%22good+first+issue%22).

<a name="areas"></a>

## 4. Areas for contributing

Of course, we appreciate contributions to all components of Firewall Orchestrator. However, we have identified some areas that are particularly suitable for open source contributions.

### Docs

Our goal is to keep our docs comprehensive and updated. If you would like to help us in doing so, we are grateful for any kind of contribution:

- Report missing content

- Fix errors in existing docs

- Help us in adding to the docs

### Tests

Our goal is to keep our Firewall Orchestrator stable and secure. If you would like to help us in doing so, we are grateful for any kind of contribution:

- Write tests

- Fix errors


<a name="ways"></a>

## 5. Ways of contributing

### Reporting an Issue

- Make sure you test against the latest released version. It is possible that we may have already fixed the bug you're experiencing.

- Provide steps to reproduce the issue

- Please include logs of the server (/var/log/fworch/*.log), if relevant.

### Working on an issue

- We use the [fork-and-branch git workflow](https://blog.scottlowe.org/2015/01/27/using-fork-branch-git-workflow/).

- Please make sure there is an issue associated with the work that you're doing.

- If you're working on an issue, please comment that you are doing so to prevent duplicate work by others also.

- Squash your commits and refer to the issue using `fix #<issue-no>` or `close #<issue-no>` in the commit message, at the end.
  For example: `resolve answers to everything (fix #42)` or `resolve answers to everything, fix #42`

- Rebase master with your branch before submitting a pull request.

<a name="commit-messages"></a>

## 6. Commit messages

- The first line should be a summary of the changes, not exceeding 50
  characters, followed by an optional body which has more details about the
  changes. Refer to [this link](https://github.com/erlang/otp/wiki/writing-good-commit-messages)
  for more information on writing good commit messages.

- Use the imperative present tense: "add/fix/change", not "added/fixed/changed" nor "adds/fixes/changes".

- Don't capitalize the first letter of the summary line.

- Don't add a period/dot (.) at the end of the summary line.


(Credits: Some sections are adapted from https://github.com/PostgREST/postgrest/blob/master/.github/CONTRIBUTING.md and
 https://github.com/hasura/graphql-engine/blob/master/CONTRIBUTING.md)


## 7. Coding Guidelines

Whenever contributing code, please adhere to our [Coding Guidelines](CODING_GUIDELINES.md)
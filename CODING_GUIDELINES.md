
# Code Guidelines

## Naming conventions
- variable and method names must be self-explanatory
- variable names must be in CamelCase
- class names start with capital letters
- variables start with small letters
- constants should start with "k"

## Complexity Limits
- lines per method <=100
- method complexity: number of control elements (if/case/foreach/...)) <=10 
- number of parameters per methods <= 7
- number of lines per source files <= 1000

## General
- write unit tests where useful and possible, at least for each fix
- no magic numbers (define global constants in one place instead)
- if statement must contain brackets at beginning of line
- commented code should be avoided
- lists should be used instead of arrays
- only use comment per line (//, #), no block comments
- continue style of existing code in source file
- all recursion needs to be limited (default max. value: 100) to avoid stack overflows

## C# specific
- avoid null references
- methods should be preceded with standard comment header (///)

## Conventional Commits
The Conventional Commits specification is a lightweight convention on top of commit messages. It provides an easy set of rules for creating an explicit commit history; which makes it easier to write automated tools on top of

### Why Use Conventional Commits
- Automatically generating CHANGELOGs.
- Automatically determining a semantic version bump (based on the types of commits landed).
- Communicating the nature of changes to teammates, the public, and other stakeholders.
- Triggering build and publish processes.
- Making it easier for people to contribute to your projects, by allowing them to explore a more structured commit history.

The commit message should be structured as follows:
```
<type>[optional scope]: <description>
[optional body]
[optional footer(s)]
```

The commit contains the following structural elements, to communicate intent to the consumers:

- fix: a commit of the type fix patches a bug in your codebase (this correlates with PATCH in Semantic Versioning).
- feat: a commit of the type feat introduces a new feature to the codebase (this correlates with MINOR in Semantic Versioning).
- BREAKING CHANGE: a commit that has a footer BREAKING CHANGE:, or appends a ! after the type/scope, introduces a breaking API change (correlating with MAJOR in Semantic Versioning). A BREAKING CHANGE can be part of commits of any type.
- types other than fix: and feat: are allowed, for example @commitlint/config-conventional (based on the Angular convention) recommends build:, chore:, ci:, docs:, style:, refactor:, perf:, test:, and others.
- footers other than BREAKING CHANGE: <description> may be provided and follow a convention similar to git trailer format.

### Examples
Commit message with description and breaking change footer
```
feat: allow provided config object to extend other configs

BREAKING CHANGE: `extends` key in config file is now used for extending other config files
```

Commit message with ! to draw attention to breaking change
```
feat!: send an email to the customer when a product is shipped
```

Commit message with scope and ! to draw attention to breaking change
```
feat(api)!: send an email to the customer when a product is shipped
```

Commit message with ! to draw attention to breaking change
```
chore!: drop support for Node 6

BREAKING CHANGE: use JavaScript features not available in Node 6.
```

Commit message with no body
```
docs: correct spelling of CHANGELOG
```

Commit message with multi-paragraph body and multiple footers
```
fix: prevent racing of requests

Introduce a request id and a reference to latest request. Dismiss
incoming responses other than from latest request.

Remove timeouts which were used to mitigate the racing issue but are
obsolete now.

Reviewed-by: Z
Refs: #123
```

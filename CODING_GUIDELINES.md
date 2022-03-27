
# Code Guidelines

## Naming conventions
- variable and method names must be self-explanatory
- variable names must be in CamelCase
- variables start with capital letters (only constants?)
- constants start with "k"

## General
- write unit tests where useful and possible
- lines per method <=100
- complexity (# of structure elements (if/case)) <=10 per method
- no magic numbers (define global constants in one place instead)
- if statement must contain brackets
- number of lines in source files <= 1000
- no commented code parts allowed
- only comment per line, no block comments
- use style of existing code in source file
- number of parameters per methods <= 7

## C# specific
- avoid null references
- methods should be preceded with standard comment header (///)

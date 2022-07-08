
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

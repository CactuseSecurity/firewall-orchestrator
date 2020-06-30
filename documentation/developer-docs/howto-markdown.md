# Clean Markdown (according to markdownlint)

- Every Element (headers, lists, codeblocks, etc.) should be surrounded by blank
  lines
- Don't use inline HTML (such as ```<br>```)
- "no-trailing-spaces" (no unnecessary whitespaces at end of line)
- "no-trailing-punctuation" (no punctuation (":", ".", ",") after header)
- No hard tabs
- Must have newline character at end of file

## Headers

- Only one top level header in the same document

  ```
  # Title

  text

  ## Section

  text
  ```

## Lists

- One or zero whitespaces after list marker

  ```
  - List Item
    - List Item
  ```

## URLs

- Example without text

  ```
  <https://www.apollographql.com/>
  ```

- Example with text:

  ```
  [Link to whatever](https://github.com/CactuseSecurity/firewall-orchestrator/blob/master/whatever/whatever.md)
```

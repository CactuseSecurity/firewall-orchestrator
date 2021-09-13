# all a developer needs to know ;-)

##
Edit architecture diagram (internal only) at https://xfer.cactus.de/index.php/f/18376

## tools & apps

- jiffy time tracking: android app, see play store
- nextcloud talk app: android app, see play store, alerts via beeps!


## git ettiquete
- changes in documemtation dir can be done directly without pull-request
- all other changes need to be reviewed via pull-request

## UI 

[Open Iconic Icons](https://useiconic.com/open)

## database

To manually rollback a hanging import of management with ID 1:

`sudo -u postgres psql -d fworchdb -c "select * from rollback_import_of_mgm(<management id>)"`

old, more invasive version:

`select * from rollback_import_of_mgm_without_cascade(<management id>);`

## clean code

### shell
- <https://github.com/koalaman/shellcheck/wiki/SC2148> (Use Shebang)
- <https://www.shellcheck.net/wiki/SC2086> (Double quote to prevent globbing and word splitting.)
- <https://www.shellcheck.net/wiki/SC2006> (Use $(...) notation instead of legacy backticked '...'.)
- <https://github.com/koalaman/shellcheck/wiki/SC2034> (Unused variables)

### documentation in markdown

Clean Markdown (according to markdownlint)

- Every Element (headers, lists, codeblocks, etc.) should be surrounded by blank
  lines
- Don't use inline HTML (such as ```<br>```)
- "no-trailing-spaces" (no unnecessary whitespaces at end of line)
- "no-trailing-punctuation" (no punctuation (":", ".", ",") after header)
- No hard tabs
- Must have newline character at end of file

####  MD040/fenced-code-language Fenced code blocks should have a language specified

Code blocks must contain language in first line. Either use three ~ or ` chars, e.g.
```console
~~~yaml
- test
  abc:
    def:
~~~
```

#### Headers

- Only one top level header in the same document

  ```console
  # Title

  text

  ## Section

  text
  ```

#### Lists

- One or zero whitespaces after list marker

  ```console
  - List Item
    - List Item
  ```

#### URLs

- Example without text

  ```console
  <https://www.apollographql.com/>
  ```

- Example with text:

  ```console
  [Link to whatever](https://github.com/CactuseSecurity/firewall-orchestrator/blob/master/whatever/whatever.md)
  ```



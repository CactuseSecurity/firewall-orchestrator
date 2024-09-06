
# Code Guidelines


## General
- Close your tag - Leaving some tags open is simply a bad practice. Only self-closing tags are valid. Normal elements can never have self-closing tags.
- Don't use inline styles(if possible) - When creating your markup, do not use inline styling because it would be very hard to override these styles in case you need to.
- Try not to use "!important" - Using the !important declaration is often considered bad practice because it has side effects that mess with one of CSS's core mechanisms: specificity. In many cases, using it could indicate poor CSS architecture.

## Components
- Organize files and components in a folder structure like this. This makes it easy to find the code related to a page, without having to browse the entire file explorer. Try, as much as possible, to respect the SOLID principles. Mainly by creating autonomous and extensible components: inject the smallest possible service or parameter, manage all the possibilities offered by the component. For example, a data modification page should display the data, check their values and save the data at the end of the process.


# UI Coding Guidelines

## General
- Close your tag - Leaving some tags open is simply a bad practice. Only self-closing tags are valid. Normal elements can never have self-closing tags.
- Don't use inline styles(if possible) - When creating your markup, do not use inline styling because it would be very hard to override these styles in case you need to.
- Try not to use "!important" - Using the !important declaration is often considered bad practice because it has side effects that mess with one of CSS's core mechanisms: specificity. In many cases, using it could indicate poor CSS architecture.

## Components
- Organize files and components in a folder structure like this. This makes it easy to find the code related to a page, without having to browse the entire file explorer. Try, as much as possible, to respect the SOLID principles. Mainly by creating autonomous and extensible components: inject the smallest possible service or parameter, manage all the possibilities offered by the component. For example, a data modification page should display the data, check their values and save the data at the end of the process.


## Responsiveness
- Use the bootstrap grid and it's column classes to have easy and responsive design. [Bootstrap](https://getbootstrap.com/docs/5.3/layout/columns/)
- Decide if you want to develop mobile or desktop design first and test respectively.

## CSS Guidelines for Clean Design

There are no mandatory CSS attributes for all divs, but some conventions help keep designs clean and consistent:
- Reset/normalize styles: Apply a reset or use box-sizing: border-box; universally (often via * { box-sizing: border-box; }).
- Spacing: Apply margins/paddings only where needed. Don’t force every div to have them.
- Flexbox/Grid: If a div is used as a layout container, give it display: flex; or display: grid;.
- Width & max-width: Constrain large content areas with something like:
```css
.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1rem;
}
```
- Consistent typography: Use global font rules in body, not in every div.
- Avoid redundancy: Don’t apply generic attributes (e.g., color, font-size) on all divs—cascade from body or semantic wrappers instead.

### Recommended Practices
- Use classes, not bare div styles: class="card", class="section", etc.
- Keep base styles minimal. For example:
```css
div {
  display: block; /* default, often unnecessary */
}
```
is redundant and shouldn’t be forced on all divs.
- Leverage utility-first CSS (like Tailwind) or your own utility classes to keep styles DRY.
- Semantic HTML first: div should be a fallback, not your default.

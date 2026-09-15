/* Fits the log data table (LogDataTable.razor) into the browser window: the component asks how
   many data rows it may put on one page, so the pager below the table stays reachable without
   the page having to grow beyond a window height for a single page.
   The result is only a proposal, LogDataTableLayout.ResolvePageSize decides the page size.
*/

// distance kept between the last row and the lower window edge. It leaves room for a horizontal
// scrollbar and keeps the last row from sitting flush against the edge.
const kLogDataTableReserve = 24;

function measureLogDataTableRows(containerId, fallbackRowHeight) {
    const container = document.getElementById(containerId);
    if (!container) {
        // not rendered (yet): the component keeps the page size it already has
        return 0;
    }

    // an already rendered row is the only reliable row height, the fallback is used for the very
    // first measurement of a table whose body is still empty
    const measuredRowHeight = Math.round(container.querySelector("tbody tr")?.getBoundingClientRect().height ?? 0);
    const rowHeight = measuredRowHeight > 0 ? measuredRowHeight : fallbackRowHeight;
    if (rowHeight <= 0) {
        return 0;
    }

    // everything the container shows besides the data rows: the column headers with their filter
    // row and the pager below the table. Taken as the difference to the rows instead of adding the
    // parts up, so it stays correct when a header wraps or the pager grows. It does not depend on
    // the number of rows, so applying the result does not change the next measurement.
    const containerHeight = container.getBoundingClientRect().height;
    const rowsHeight = container.querySelector("tbody")?.getBoundingClientRect().height ?? 0;
    const overhead = Math.max(0, containerHeight - rowsHeight);

    // the navbar is sticky at the top of the window (NavigationMenu.razor), so it keeps covering
    // the window while the user scrolls through the table
    const navbarHeight = document.getElementById("navbar")?.getBoundingClientRect().height ?? 0;

    const available = window.innerHeight - navbarHeight - overhead - kLogDataTableReserve;
    return Math.max(0, Math.floor(available / rowHeight));
}

/* Fits the log data table (LogDataTable.razor) into the browser window: the component asks how
   many data rows it may put on one page, so the pager below the table stays reachable without
   the page having to grow beyond a window height for a single page.
   The result is only a proposal, LogDataTableLayout.ResolvePageSize decides the page size.
*/

// distance kept between the last row and the lower window edge. It leaves room for a horizontal
// scrollbar and keeps the last row from sitting flush against the edge.
const kLogDataTableReserve = 24;

// answer for "there was nothing to measure", which is not the same as a window measured as too
// short for a single row - that one is a real zero. LogDataTableLayout.ResolvePageSize in C#
// treats the two differently, so they must not share a value.
const kLogDataTableCouldNotMeasure = -1;

function measureLogDataTableRows(containerId, fallbackRowHeight) {
    const container = document.getElementById(containerId);
    if (!container) {
        // not rendered (yet): the component keeps the page size it already has
        return kLogDataTableCouldNotMeasure;
    }

    // an already rendered row is the only reliable row height, the fallback is used for the very
    // first measurement of a table whose body is still empty
    const measuredRowHeight = Math.round(container.querySelector("tbody tr")?.getBoundingClientRect().height ?? 0);
    const rowHeight = measuredRowHeight > 0 ? measuredRowHeight : fallbackRowHeight;
    if (rowHeight <= 0) {
        return kLogDataTableCouldNotMeasure;
    }

    // everything the container shows besides the data rows: the column headers with their filter
    // row and the pager below the table. Taken as the difference to the rows instead of adding the
    // parts up, so it stays correct when a header wraps or the pager grows. It does not depend on
    // the number of rows, so applying the result does not change the next measurement.
    const containerRect = container.getBoundingClientRect();
    const containerHeight = containerRect.height;
    const rowsHeight = container.querySelector("tbody")?.getBoundingClientRect().height ?? 0;
    const overhead = Math.max(0, containerHeight - rowsHeight);

    // The table can appear after a large connection-edit form. Its top edge is therefore the
    // actual start of the space available to its rows, rather than the bottom of the navbar.
    // When the document has been scrolled far enough for the table to be underneath the sticky
    // navbar, the navbar's bottom edge is the effective start instead.
    const navbarBottom = document.getElementById("navbar")?.getBoundingClientRect().bottom ?? 0;
    const availableTop = Math.max(containerRect.top, navbarBottom);

    // a window too short for a single row answers zero, which is a measurement and not a failure:
    // the caller raises it to the smallest page it is willing to show
    const available = window.innerHeight - availableTop - overhead - kLogDataTableReserve;
    return Math.max(0, Math.floor(available / rowHeight));
}


function scrollIntoRSBView(htmlObjId) {
  let obj =  document.getElementById(htmlObjId)?.parentElement?.parentElement; // gets the tr element containing the obj
  if (!obj)
    return false;
  obj.scrollIntoView({behavior: "smooth", block: "center"});
  // Highlight the row
  obj.style.transition = "background-color 500ms linear";
  obj.style.backgroundColor = "#a4d7f5";
  // Remove highlight after 800ms
  setTimeout(() => obj.style.backgroundColor = "", 800);
  return obj.offsetParent !== null; // element visible?
}
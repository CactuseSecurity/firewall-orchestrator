﻿
function scrollIntoRSBView(htmlObjId) {
  let obj =  document.getElementById(htmlObjId)?.parentElement?.parentElement; // gets the tr element containing the obj
  if (!obj)
    return false;
  obj.scrollIntoView({behavior: "smooth", block: "center"});
  obj.classList.add("fade-bg");
  obj.classList.add("temp-highlight");
  setTimeout(() => obj.classList.remove("temp-highlight"), 800)
  return obj.offsetParent !== null; // alement visible?
}
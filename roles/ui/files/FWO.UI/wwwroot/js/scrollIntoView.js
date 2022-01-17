
function scrollIntoRSBView(htmlObjId) {
  let obj = document.getElementById(htmlObjId);
  document.getElementById("rsb").scrollTop = obj.offsetTop - window.innerHeight / 2 + obj.getBoundingClientRect().height / 2;
}
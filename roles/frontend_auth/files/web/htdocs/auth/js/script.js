/* $Id: script.js,v 1.1.2.4 2012-03-07 11:44:42 tim Exp $ */
/* $Source: /home/cvs/iso/package/web/htdocs/js/Attic/script.js,v $ */
//Allgemeine Funktionen

function getInnerHeight () {
	var x,y;
	if (self.innerHeight) // all except Explorer
	{
		y = self.innerHeight - 17;  // Ausgleich, da IE etwas geringere Werte zurueckliefert
	}
	else if (document.documentElement && document.documentElement.clientHeight)
	// Explorer 6 Strict Mode
	{
		y = document.documentElement.clientHeight;
	}
	else if (document.body) // other Explorers
	{
		y = document.body.clientHeight;
	}
	return y;
}

function PopUp(Datei){
 window.open(Datei,'neugierig','width=320,height=360,toolbar=no,menubar=no,location=no,status=no,scrollbars=no');
}

function changeColor(ID,farbe){
 farbe="#"+farbe;
 document.getElementById(ID).style.backgroundColor=farbe;
}

function changeColor1(ID){
 farbe="#C00";
 document.getElementById(ID).style.backgroundColor=farbe;
}

function Hilfe(ID) {
  var Hilfe = "Hilfe"+ID;
  document.getElementById(Hilfe).style.visibility="visible";
}
function closeHilfe(ID) {
  var Hilfe = "Hilfe"+ID;
  document.getElementById(Hilfe).style.visibility="hidden";
  }

// Hide Show Columns
function hideColumn (FormName,SelectName,TableName) {
  var Laenge = eval("document."+FormName+"."+SelectName+".length");
  for (i=0;i<Laenge;i++) {
    if(eval("document."+FormName+"."+SelectName+".options["+i+"].selected==true")) {
      eval("document."+FormName+"."+SelectName+".options["+i+"].style.backgroundColor='#FF6600'");
      j=i+1;
      var table = document.getElementById(TableName);
      for (var r = 0; r < table.rows.length; r++) {
        table.rows[r].cells[j].style.display = 'none';
      }
    }
  }
  eval("document."+FormName+"."+SelectName+".blur()");
}

function showColumn (FormName,SelectName,TableName) {
  var Laenge = eval("document."+FormName+"."+SelectName+".length");
  for (i=0;i<Laenge;i++) {
    if(eval("document."+FormName+"."+SelectName+".options["+i+"].selected==true")) {
      eval("document."+FormName+"."+SelectName+".options["+i+"].style.backgroundColor=''")
      j=i+1;
      var table = document.getElementById(TableName);
      for (var r = 0; r < table.rows.length; r++) {
        table.rows[r].cells[j].style.display = '';
      }
    }
  }
  eval("document."+FormName+"."+SelectName+".blur()");
}

function Sort(updown,id) {
  if(updown==1) document.getElementById(id).src='img/aufsteigend_ro.gif';
  if(updown==11) document.getElementById(id).src='img/aufsteigend.gif';
  if(updown==2) document.getElementById(id).src='img/absteigend_ro.gif';
  if(updown==22) document.getElementById(id).src='img/absteigend.gif';
}

/*
function openMenue(ID) {
 (document.getElementById(ID).style.display=='none') ? document.getElementById(ID).style.display='' : document.getElementById(ID).style.display='none';
}

function closeMenue(ID) {
  for (var i = 1; i <= 5; i++) {
    IDneu = ID + i;
    if (!document.getElementById(IDneu)=="") {openMenue(IDneu);}
  }
}
*/


function ResetFields() {
  document.reporting.quellname.value="";
  document.reporting.quell_ip.value="";

  document.reporting.zielname.value="";
  document.reporting.ziel_ip.value="";

  document.reporting.regelkommentar.value="";

  document.reporting.negrules.checked=true;
  document.reporting.getElementById('anyrules').checked=true;
  document.reporting.inactive.checked=true;
  document.reporting.notused.checked=true;

  document.reporting.ben_name.value="";
  document.reporting.ben_vor.value="";
  document.reporting.ben_id.value="";

  document.reporting.dienstname.value="";
  document.reporting.dienst_ip.selectedIndex=0;
  document.reporting.reportingFormat.selectedIndex=0;
  document.reporting.dienstport.value="";
}

function ShowHideLeer(Zustand) {
  if(Zustand="hide") document.getElementById('leer').style.visibility='hidden';
  if(Zustand="zeige") document.getElementById('leer').style.visibility='visible';
}

//Hoehe des iFrames setzen
function Hoehe_Frame_setzen(var_Frame_name) {
	if(document.getElementById) {
		if (is_ie) {
			var Fenster = document.getElementsByTagName('body')[0].offsetHeight;
			var Fenster_Breite = document.getElementsByTagName('body')[0].offsetWidth;
		}
		if (is_nav) {
			var Fenster = window.innerHeight;
			var Fenster_Breite = window.innerWidth;
		}
		var derInhalt=document.getElementById("inhalt");
		var I_Frame=document.getElementById(var_Frame_name);
		var hoeheInhalt	= 0;
		var topInhalt = 0;
		if (derInhalt)  {
			hoeheInhalt	= derInhalt.offsetHeight;
			topInhalt	= derInhalt.offsetTop;
		}
		iFrameTop=topInhalt + hoeheInhalt;
		iFrameHeight=Fenster-iFrameTop;
		iFrameWidth=Fenster_Breite-document.getElementById(var_Frame_name).style.left.slice(0,-2);
	
		I_Frame.style.top=iFrameTop;
		I_Frame.style.height=iFrameHeight-40;
		I_Frame.style.width=iFrameWidth-210;
	}
}

//da sich der Inhalt in Reporting in der Höhe ändern kann, dieses Script
function position_iframe(varFrameName) {
	var derInhalt=document.getElementById("inhalt");
	var I_Frame=document.getElementById(varFrameName);
	if (derInhalt) {
		var hoeheInhalt=derInhalt.offsetHeight;
		var topInhalt=derInhalt.offsetTop;
		var iFrameTop = topInhalt + hoeheInhalt;
//		alert("iFrameTop: " + iFrameTop);
		I_Frame.style.top = iFrameTop;
	}
}
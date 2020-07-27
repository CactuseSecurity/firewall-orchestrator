// Ultimate client-side JavaScript client sniff.
// (C) Netscape Communications 1999.  Permission granted to reuse and distribute.
// Revised 17 May 99 to add is_nav5up and is_ie5up (see below).
/* $Id: client.js,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $ */
/* $Source: /home/cvs/iso/package/web/htdocs/js/Attic/client.js,v $ */

// convert all characters to lowercase to simplify testing
var agt=navigator.userAgent.toLowerCase();

// *** BROWSER VERSION ***
var is_major = parseInt(navigator.appVersion);
var is_minor = parseFloat(navigator.appVersion);

// Note: Opera and WebTV spoof Navigator.  We do strict client detection.
// If you want to allow spoofing, take out the tests for opera and webtv.
var is_nav  = ((agt.indexOf('mozilla')!=-1) && (agt.indexOf('spoofer')==-1)
            && (agt.indexOf('compatible') == -1) && (agt.indexOf('opera')==-1)
            && (agt.indexOf('webtv')==-1));
var is_nav2 = (is_nav && (is_major == 2));
var is_nav3 = (is_nav && (is_major == 3));
var is_nav4 = (is_nav && (is_major == 4));
var is_nav4up = (is_nav && (is_major >= 4));
var is_nav5 = (is_nav && (is_major == 5));
var is_nav5up = (is_nav && (is_major >= 5));
var is_nav45up = (is_nav && (is_minor >= 4.5));
var is_nav47down = (is_nav && (is_minor < 4.7));

var is_ie   = (agt.indexOf("msie") != -1);
var is_ie3  = (is_ie && (is_major < 4));
var is_ie4  = (is_ie && (is_major == 4) && (agt.indexOf("msie 5")==-1) );
var is_ie4up  = (is_ie  && (is_major >= 4));
var is_ie5  = (is_ie && (is_major == 4) && (agt.indexOf("msie 5")!=-1) );
var is_ie5up  = (is_ie  && !is_ie3 && !is_ie4);
var is_ie6=agt.indexOf("msie 6")!=-1;
var is_ie60b=agt.indexOf("msie 6.0b")!=-1;

var is_opera = (agt.indexOf('opera')!=-1);
var is_opera4 = (agt.indexOf('opera 4')!=-1);
var is_opera5 = (agt.indexOf('opera 5')!=-1);

/* $Id: tree.js,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $ */
/* $Source: /home/cvs/iso/package/web/htdocs/js/Attic/tree.js,v $ */
/* Variablen */

//Layer für Menu
objMenu             = document.getElementById('menu');

//Dateityp Icons
strIcoType          = '.gif';

//Icon-Verzeichnis
strIcoDir           = '/img/tree/';

//Grafikname für Datei
strFile             = 'file';

//Grafiknamen für Ordner
arrFolders          = new Array('closed', 'open');

//Zweig
strBranch           = 'branch';

//vertikale Linie
strLine             = 'line';

//Space
strSpace            = 'space';

//Icons zum Expandieren/kollabieren
arrFolderEntries    = new Array('plus', 'minus');

//Zielfenster zum ermitteln des aktuellen Menupunktes
objTargetWindow     = 'self';

//GET-Parameter beim Ermitteln des aktuellen Links ignorieren
blnIgnoreQuery      = true;

//Anker beim Ermitteln des aktuellen Links ignorieren
blnIgnoreAnchor     = true;

//Display-Eigenschaften für Menupunkte
arrDisplay=new Array('none','inline');


arrTree=new Array();

/** ist objImg eine Schaltfläche zum kollabieren/expandieren */
function is_entry(objImg)
{
  return(objImg.tagName == 'IMG' && objImg.name == 'entry')
}

/** Ausgabe img-Code für strSrc */
function img_html(strSrc, blnFunction)
{
  strFunction = (blnFunction)
                  ? 'onclick="expand(this.parentNode)"name="entry"'
                  : '';
  return('<img src="' + strIcoDir
                      + strSrc
                      + strIcoType
                      + '"border="0"align="top"' + strFunction + '>');
}

function expand(obj)
{
intEvent = (obj.lastChild.style.display == 'none' || expand.arguments.length > 1) ? 1 : 0;

arrRegExp=new Array();
arrRegExp.push(new RegExp(arrFolderEntries[1]));
arrRegExp.push(new RegExp(arrFolderEntries[0]));

obj.lastChild.style.display=arrDisplay[intEvent];

for(i=0;i<obj.childNodes.length;++i)
  {
  if(is_entry(obj.childNodes[i]))
    {
    obj.childNodes[i].src =  obj.childNodes[i].src.replace(arrRegExp[intEvent],arrFolderEntries[intEvent]);
    obj.childNodes[i].nextSibling.src = strIcoDir + arrFolders[intEvent] + strIcoType;
    }
  }
}

/** "Parsen" der Listeneinträge und erzeugen der Baumstruktur */
function build_tree()
{
  if( arrTree.length <= intDimension)
    {
      arrTree.push(strBranch);
    }

  strIco = (!is_file(objItem)) ? arrFolders[0] : strFile;
  strTree = '<br>';
  strEntry = (!is_file(objItem)) ? arrFolderEntries[0] : strBranch;
  strEntry += (is_end()) ? '_end' : '';

  for (v = 0; v < intDimension; ++v)
    {
      strTree+=img_html(arrTree[v]);
    }

  objItem.innerHTML = strTree
                      + img_html(strEntry,!is_file(objItem))
                      + img_html(strIco)+objItem.innerHTML;

  if (!is_file(objItem))
    {
      arrTree[intDimension] = 'line';
      if (is_end(objItem))
        {
          arrTree[intDimension] = 'space';
        }
    }
}

// Dimensionen > 1 verstecken und Listen-Eigenschaften entfernen

function collapse_menu()
{
  objItem.style.listStyleType = 'none';
  objItem.style.display = 'inline';
  objItem.style.padding = 0;
  objItem.parentNode.style.display = (get_dimension(objItem) == 0)
                                          ? 'inline'
                                          : 'none';
}

/** Befindet sich Objekt innerhalb des Menues */
function in_menu(obj)
{
objParentNode = obj.parentNode;
while(objParentNode != objMenu && objParentNode.tagName != 'BODY')
  {
    objParentNode = objParentNode.parentNode;
  }
return(objParentNode == objMenu);
}

/** Enthält objekt eine UL-Liste */
function is_file()
{
  return(!objItem.hasChildNodes()||objItem.lastChild.tagName!='UL')
}

function get_dimension()
{
  intDimension=-1;
  objParentNode=objItem.parentNode;
  while(objParentNode!=objMenu)
    {
      if(objParentNode.tagName=='UL'){intDimension++;}
      objParentNode=objParentNode.parentNode;
    }
  return intDimension;
}

function is_end()
{
(objItem.parentNode.lastChild.tagName);
return(objItem==objItem.parentNode.lastChild);
}

function strip_spaces(str)
{
strOut=str.replace(/>\s+</gm,'><');
strOut=str.replace(/>\s+/gm,'>');
strOut=str.replace(/\s+</gm,'<');
return strOut;
}
/** Durchalufen alle Li-Elemente */
function init_menu()
{
if (!document.getElementsByTagName
  || typeof document.getElementsByTagName('html')[0].innerHTML != 'string')
  {
    return;
  }

objMenu.innerHTML = strip_spaces(objMenu.innerHTML);

for (l = 0; l < document.getElementsByTagName('li').length; ++l)
  {
  objItem = document.getElementsByTagName('li')[l];

  if (in_menu(objItem))
    {
      intDimension = get_dimension(objItem);
      collapse_menu(objItem);
      build_tree(objItem);
    }
  }
objMenu.innerHTML = strip_spaces(objMenu.innerHTML);
opening();
}

/** Aktuellen Link ermitteln und Menu expandieren */
function opening()
{
for(a = 0; a < document.links.length; ++a)
  {
  if (in_menu(document.links[a]))
    {

    //document.links[a].style.textDecoration = 'none';
    objFolder = document.links[a].parentNode;

    if (is_active_link(document.links[a].href))
      {
      while(objFolder.parentNode != objMenu)
        {
        if(objFolder.tagName == 'UL')
          {
            expand(objFolder.parentNode,1);
            //document.links[a].style.fontWeight = 'bold';
          }

        objFolder=objFolder.parentNode;
        }
      }
    }
  }
}

/** Prüfen zweier Links auf Übereinstimmung */
function is_active_link(strUrl)
{
arrUrls = new Array(strUrl,String(eval(objTargetWindow + '.location')));

for(u = 0; u < arrUrls.length; ++u)
  {
  if (blnIgnoreAnchor)
    {
      arrUrls[u] = arrUrls[u].replace(/#.*?$/, '');
    }

  if (blnIgnoreQuery)
    {
      arrUrls[u]=arrUrls[u].replace(/\?[^#]*/g, '');
    }
  }

return(arrUrls[0]==arrUrls[1]);
}

// Starten. Body-Onload, nach dem laden der Tree
init_menu();
//Navi wird erst dargestellt, wenn komplett geladen.
document.getElementById("menu").style.display="block";
<?php


/*
 * $Id: display-table.php,v 1.1.2.14 2012-02-28 12:08:51 tim Exp $
 * $Source: /home/cvs/iso/package/web/include/Attic/display-table.php,v $
 * Created on 29.10.2005
 *
 */

class DisplayTable {

	var $headers;
	var $name;
	var $filter;
	var $error;

	function __construct($name, $headers) {
		$this->name = $name;
		$this->headers = $headers;
	}
	function displayTableOpen($report_format) {
		if ($this->isHtmlFormat($report_format))
			return "\n" . '<table id="' . $this->name . '" cellpadding="0" cellspacing="0" class="tab-border tab-fixed-width" style="margin:0px 10px;">';
		else
			return '';
	}
	function displayTableClose($report_format) {
		if ($this->isHtmlFormat($report_format))
			return "</table>\n";
		else
			return '';
	}
	function displayRuleDeviceHeading($rule, $report_format) {
		$lastElement = count($this->headers);
		$ii = 1;
		if ($this->isHtmlFormat($report_format))
			$result = "<tr>";
		else
			$result = '';
		foreach ($this->headers as $header) {
			if ($header == "Nr") {
				if ($this->isHtmlFormat($report_format)) {
					$result .= '<td class="fw_headerdev_device" height="21">' .
					'<div style="position:relative;top:1px;left:3px;width:100%;height:21px;">' .
					'<div style="position:absolute;white-space:nowrap;border:0px none;">' .
					'<b class="fw_headerdev_device_inner_div">' . $rule->getDevString() . '</b>' . '</div></div></td>';
				} else {
					if ($report_format<>'json') $result .= $this->displayCommentLine($rule->getDevString(), $report_format);
				}
			}
			elseif ($ii == $lastElement) {
				if ($this->isHtmlFormat($report_format))
					$result .= '<td class="fw_headerdev_device_right">&nbsp;</td></tr>';
			} else {
				if ($this->isHtmlFormat($report_format))
					$result .= '<td class="fw_headerdev_device">&nbsp;</td>';
			}
			$ii++;
		}
		return $result;
	}

	function displayTableHeaders($report_format) {
		if ($this->isHtmlFormat($report_format)) {
			$headersString = "<tr>";
			foreach ($this->headers as $header) {
				if ($header == "")
					$header = "&nbsp;";
				$headersString .= '<td class="headerdev">' . $header . '</td>';
			}
			$headersString .= '</tr>';
			return $headersString;
		} else
			return '';
	}
	function displayShowHideColumn($name, $report_format) {
		$headerString = "";
		if (!isset ($report_format) or $report_format == 'html') {
			$headerString .= '<select name="' . $name . '" multiple size="4" style="margin:10px;vertical-align:middle;"><script language="JavaScript" type="text/javascript">';
			$headerString .= 'var header= new Array(';
			$index = 0;
			foreach ($this->headers as $header) {
				$headerString .= "\"" . $header . "\"";
				if ($index < count($this->headers) - 1) {
					$headerString .= ",";
				}
				$index++;
			}
			$headerString .= ');';
			$headerString .= 'for (var i = 1; i < header.length; i++) {';
			$headerString .= "  document.write('<option value=\"' + i + '\">' + header[i]);}</script></select>";
			$headerString .= "<b>Spalte:&nbsp;&nbsp;</b><input type=\"button\" value=\"ausblenden\" class=\"button\" style=\"margin-right:15px;\" onclick=\"hideColumn('reporting_result','" . $name . "','" . $this->name . "');\">";
			$headerString .= "<input type=\"button\" value=\"einblenden\" class=\"button\" onclick=\"showColumn('reporting_result','" . $name . "','" . $this->name . "');\">";
		}
		return $headerString;
	}
	function displayTableHideShow($stamm, $id, $id_min, $name, $report_format) {
		if (!isset ($report_format) or $report_format == 'html') {
			$script = "document.getElementById('" . $id . "').style.display='none';document.getElementById('" . $id_min . "').style.display='inline';";
			$hideStr = '<div id="' . $id . '" style="display:inline;"><br>' .
			'<table width="450" border="0" cellspacing="0" cellpadding="0"><tr>' .
			'<td><img src="' . $stamm . 'img/icon_min2.gif" width="16" height="16" alt="Tabelle ausblenden" title="Tabelle ausblenden" align="absmiddle"  onClick="' . $script . '">&nbsp;&nbsp;<b>' . $name . '</b></td>' .
			'</tr></table>';
		}
		elseif ($report_format == 'simple.html') $hideStr = '<div id="' . $id . '" style="display:inline;"><br>&nbsp;&nbsp;<b>' . $name . '</b>';
		else
			$hideStr = '';
		return $hideStr;
	}
	function displayTableShowHide($stamm, $id, $id_min, $name, $report_format) {
		if (!isset ($report_format) or $report_format == 'html') {
			$script = "document.getElementById('" . $id . "').style.display='inline';document.getElementById('" . $id_min . "').style.display='none';";
			$hideStr = '<br></div>' .
			'<div id="' . $id_min . '" style="display:none;"><br>' .
			'<table width="450" border="0" cellspacing="0" cellpadding="0"><tr>' .
			'<td><img src="' . $stamm . 'img/icon_plus2.gif" width="16" height="16" alt="Tabelle einblenden" title="Tabelle einblenden" align="absmiddle"  onClick="' . $script . '">&nbsp;&nbsp;<b>' . $name . '</b><br><br></td>' .
			'</tr></table></div>';
		}
		elseif ($report_format == 'simple.html') $hideStr = '<br></div><div id="' . $id_min . '" style="display:none;<br>">&nbsp;&nbsp;<b>' . $name . '</b><br><br></td>' . '</div>';
		else
			$hideStr = '';
		return $hideStr;
	}
	function displayColumn($column, $report_format) {
		if ($this->isHtmlFormat($report_format))
			return ("<td class='celldev'>" . (!is_null($column) ? preg_replace("/\n/", "<br>", $column) : "&nbsp;") . "</td>");
		else {
			return ('"' . $column . '",');
		}
	}
	function displayColumnNoHtmlTags($column) {
		return ("<td class='celldev'>$column</td>");
	}
	function showDotted($number) {
		// TODO
		$len = strlen($number);
		if ($len <= 3)
			return $number;
		else {
			$start = substr($number, 0, $len -3);
			$end = substr($number, $len -3);
			return $this->showDotted($start) . '.' . $end;
		}
	}
	function displayColumnNum($column, $report_format) {
		if ($this->isHtmlFormat($report_format))
			return ("<td class='celldevAlignRight'>" . (!is_null($column) ? preg_replace("/\n/", "<br>", $this->showDotted($column)) : "&nbsp;") . "</td>");
		else
			return $this->displayColumn($column, $report_format);
	}
	function displayColumnNoBorder($column, $report_format) {
		if ($this->isHtmlFormat($report_format))
			return ("<td class='celldev_noborder'>" . (!is_null($column) ? preg_replace("/\n/", "<br>", $column) : "&nbsp;") . "</td>");
		else
			return $this->displayColumn($column, $report_format);
	}
	function displayColumn_wrap($column, $report_format) {
		if ($this->isHtmlFormat($report_format))
			return ("<td class='celldev_wrap'>" . (!is_null($column) ? preg_replace("/\n/", "<br>", $this->remove_extra_linebreaks($column)) : "&nbsp;") . "</td>");
		else
			return $this->displayColumn($column, $report_format);
	}
	function displayRow($chNr, $report_format) {
		if ($this->isHtmlFormat($report_format))
			return ("\n" . '<tr id="' . $chNr . '" onMouseOver="changeColor(\'' . $chNr . '\',\'FFD\');" onMouseOut="changeColor(\'' . $chNr . '\',\'FFF\');">');
		else
			return "\n";
	}
	function displayRowSimple($report_format) {
		if ($this->isHtmlFormat($report_format))
			return ("<tr>");
		else
			return $this->displayRow($report_format);
	}
	function displayReference($refTyp, $refId, $refName) {
		return ("<a href='#" . $refTyp . $refId . "'>" . $refName . "</a>");
	}

	function displayAnchor($refTyp, $refId, $refName) {
		return ("<a id='" . $refTyp . $refId . "'>" . $refName . "</a>");
	}
	function displayChangedColumn($oldcolumn, $newcolumn) {
		return "<td class='celldev'>" .
		'<table cellpadding="0" cellspacing="0" width="100%">' .
		'<tr><td class="olddate">' . $oldcolumn . '</td></tr>' .
		'<tr><td class="newdate">' . $newcolumn . '</td></tr>' .
		'</table></td>';
	}

	function displayInsertedColumn($newcolumn) {
		return "<td class='celldev'>" .
		'<table cellpadding="0" cellspacing="0" width="100%">' .
		'<tr><td>&nbsp;</td></tr>' .
		'<tr><td class="newdate">' . $newcolumn . '</td></tr>' .
		'</table></td>';
	}

	function displayDeletedColumn($oldcolumn) {
		return "<td class='celldev'>" .
		'<table cellpadding="0" cellspacing="0" width="100%">' .
		'<tr><td class="olddate">' . $oldcolumn . '</td></tr>' .
		'<tr><td class="newdatedel">' . $oldcolumn . '</td></tr>' .
		'</table></td>';
	}

	function display_user_comment_only($no_of_rules, $rule_comment) {
		return '<td class="celldev_wrap">' . '<textarea name="user_eingabe" cols="20" rows="2" class="diff_nosize" readonly>' . $rule_comment . '</textarea>' . '</td>';
	}
	function display_change_comments_readonly($stamm, $abs_change_id, $comment, $client_request_str, $change_type, $no_of_rules) {
		$icon = "icon_min.gif";
		if (!isset ($client_request_str)) {
			$client_request_str = "&nbsp;";
		}
		if (!isset ($comment)) {
			$comment = "&nbsp;";
		}
		if ($change_type == 'D') {
			$icon = "icon_min.gif";
		}
		if ($change_type == 'I') {
			$icon = "icon_plus.gif";
		}
		if ($change_type == 'C') {
			$icon = "icon_diff.gif";
		}
		return '<td class="celldev" style="text-align:center;font-weight:bold;">' . ($no_of_rules +1) . '</td>' .
		'<td class="celldev" style="text-align:center;"><img src="' . $stamm . 'img/' . $icon . '"></td>' .
		'<td class="celldev_wrap">' . '<textarea name="user_eingabe" cols="20" rows="2" class="diff_nosize" readonly>' . $comment . '</textarea>' . '</td>' .
		'<td class="celldev">' . $client_request_str . '</td>';
	}
	function display_change_comment($stamm, $absId, $change_type) {
		$commentString = '<td class="celldev" style="text-align:center;font-weight:bold;">' . $absId . '</td>' . '<td class="celldev" style="text-align:center;">';
		if ($change_type == 'D') {
			$icon = "icon_min.gif";
		}
		if ($change_type == 'I') {
			$icon = "icon_plus.gif";
		}
		if ($change_type == 'C') {
			$icon = "icon_diff.gif";
		}

		return ($commentString . '<img src="' . $stamm . '/img/' . $icon . '"></td>');
	}
	function remove_extra_linebreaks($string) {
		$new_string = urlencode($string);
		$new_string = preg_replace("/%0D/", '', $new_string);
		$new_string = urldecode($new_string);
		return $new_string;
	}
	function getCommentChar($report_format) {
		switch ($report_format) {
			case "phion" :
				$comment_char = '#';
				break;
			case "junos" :
				$comment_char = '#';
				break;
			default :
				$comment_char = '#';
		}
		return $comment_char;
	}
	function getCommentStartChar($report_format) {
		switch ($report_format) {
			case "phion" :
				$comment_char = '# ';
				break;
			case "junos" :
				$comment_char = '/* ';
				break;
			default :
				$comment_char = '# ';
		}
		return $comment_char;
	}
	function getCommentEndChar($report_format) {
		switch ($report_format) {
			case "phion" :
				$comment_char = '';
				break;
			case "junos" :
				$comment_char = ' */';
				break;
			default :
				$comment_char = '';
		}
		return $comment_char;
	}
	function displayComment($text, $report_format) {
		if ($this->isHtmlFormat($report_format))
			$comment = $text;
		else
			$comment = "# $text"; # good for junos, screenos
		return $comment;
	}
	function displayCommentLine($line, $report_format) {
		$maxline_len = 256;
		if ($this->isHtmlFormat($report_format))
			return '<td class="celldev" colspan="' . count($this->headers) . '">' . $line . '</td>' . "\n";
		else {
			if (strlen($line)>$maxline_len) {
				$result = '';
				while (strlen($line)>$maxline_len) {
					$start_of_line = substr($line,  0, $maxline_len);
					$line = substr($line, $maxline_len);
					$result .= $this->getCommentStartChar($report_format) . $start_of_line . $this->getCommentEndChar($report_format) . "\n";
				}
				$result .= $this->getCommentStartChar($report_format) . $line . $this->getCommentEndChar($report_format) . "\n";
				return $result;
			} else return $this->getCommentStartChar($report_format) . $line . $this->getCommentEndChar($report_format) . "\n";	
		}
	}
	function displayCommentLineSeparator($report_format) {
/*
		$length = 75;
		$result = '';
		$comment_char = $this->getCommentChar($report_format);
		for ($i = 0; $i < $length; $i++)
			$result .= "$comment_char";
		return "$result\n";
*/
		return '';
		}

	function isHtmlFormat($report_format) {
		return (!isset ($report_format) or $report_format == 'html' or $report_format == 'simple.html');
	}
	function displayJunosToken($str) {
		$forbidden_chars = array(',', ' ', '+', '/', '#', '&', '!', '$', '%', "\\", ';', ':', '<', '>', '|', "\"", '§', '(', ')', '='. '?', '´', '`', "\'", "*", '^', 'ß');
		$result = str_replace($forbidden_chars, "_", $str);
		while (!preg_match("/^([a-zA-Z0-9])(.*)/", $result, $treffer)) $result = substr($result,1);	// removing illegal chars from beginning of token
		return $result;
	}
	function displayJunosNWObjLine($type, $elements) {
		$result = "\t\t\t\t\t$type ";
		if (count($elements)>1) {
			$result .= "[ ";
			foreach ($elements as $element) $result .= $this->displayJunosToken($element->getObjectName()) . " ";
			$result .= "]";
		} elseif (count($elements)==1) {
			$element = $elements[0];
			$result .= $this->displayJunosToken($element->getObjectName());	
		} else {  // count=0
			$this->displayCommentLine("warning: found no NWObj elements for type $type", 'junos');
		}
		$result .= ";\n"; 			
		return $result;
	}
	function displayJunosSvcObjLine($type, $elements) {
		$result = "\t\t\t\t\t$type ";
		if (count($elements)>1) {
			$result .= "[ ";
			foreach ($elements as $element) $result .= $this->displayJunosToken($element->getName()) . " ";
			$result .= "]";
		} else {
			$element = $elements[0];
			$result .= $this->displayJunosToken($element->getName());	
		}
		$result .= ";\n"; 			
		return $result;
	}
}
?>
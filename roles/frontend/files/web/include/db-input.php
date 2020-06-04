<?php
// $Id: db-input.php,v 1.1.2.7 2012-06-06 09:36:37 tim Exp $
// $Source: /home/cvs/iso/package/web/include/Attic/db-input.php,v $
class DbInput {

        function __construct () {
        }
        function clean ($input, $maxlength) {
           if (is_array($input)) 
        		for ($i = 0;  $i<count($input); $i++) {
        		    if (isset($input[$i]))
            			$input[$i] = $this->clean($input[$i], $maxlength);
        		}
           else {
	           if (!is_numeric($input)) {
	                if (is_null($maxlength)) $maxlength=255;
	                $input = substr($input, 0, $maxlength);
	                $x = 'UmL878aUt';
	                $ctrl_zeichen = array("<",">",";","'",'"');
	//                $sonderzeichen = array("�","�","�","�","�","�","�","�");
	//                $sonderzeichen = array("&auml","&ouml","&uuml","&szlig","&Auml","&Ouml","&Uuml","&euro");
	                $sonderzeichen = array('\0xE4','\0xF6','\0xFC','\0xDF',"0xC4","0xD6","0xDC",'\0x80');
	                $replacements  = array($x."ae",$x."oe",$x."ue",$x."ss",$x."AE",$x."OE",$x."UE",$x."Euro");
					$input = str_replace ($ctrl_zeichen, "", $input);		// get rid of java script tags
	                $input = str_replace ($sonderzeichen, $replacements, $input);
	                $input = escapeshellarg($input);
					$input = str_replace ($replacements, $sonderzeichen, $input);
					$input = trim($input, "'");
	           }
           } 	
           return $input;
        }
        function clean_password ($input) {
        	$output = $input;
        	if (!is_numeric($output)) {
                $maxlength=255;
                $output = substr($output, 0, $maxlength);
                $ctrl_zeichen = array("<",">",";","'",'"');
				$output = str_replace ($ctrl_zeichen, "", $input);		// get rid of java script tags, SQL code, etc
				$output = trim($output, "'");
				if (!($output == $input)) {
					echo "WARNING: password contains control chars!";
				}
           }
           return $output;
        }
        function clean_allow_linebreak ($input, $maxlength) {
                if (!is_numeric($input)) {
                        if (is_null($maxlength)) $maxlength=10000;
                        $nl_sub = 'KerridschRitoern4711';
                        $input = str_replace(array(chr(13).chr(10),chr(13),chr(10),"\n"), $nl_sub, $input);
 						$this->clean($input, $maxlength);
                        $input = str_replace($nl_sub, "\n", $input);
                }
                return $input;
        }
        function clean_structure ($struct) {
        	$s = $struct;
        	setlocale(LC_CTYPE, "de_DE.UTF-8");
			reset($s); 
			while (list($key, $val) = each($s)) {
				if ($s["$key"] !== 'dbpw' && $s["$key"] !== 'Passwort' && $s["$key"] !== 'oldpassword' &&
					$s["$key"] !== 'newpassword' && $s["$key"] !== 'newpassword2') {
						$s["$key"] = $this->clean_allow_linebreak($val,10000); 
					} else {
						$s["$key"] = $this->clean_password($val); 
					}
			}
			reset ($s);
			return $s;
        }
}
?>

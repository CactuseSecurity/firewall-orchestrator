<?php
// $Id: config_user.php,v 1.1.2.3 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_user.php,v $
	$stamm="/";	$page="config";
	require_once("check_privs.php");
	setlocale(LC_CTYPE, "en_US.UTF-8");	
	if (!$allowedToConfigureUsers) { header("Location: ".$stamm."config/configuration.php"); }
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
	<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
	<title>fworch Benutzerkonfiguration</title>
	<script type="text/javascript" src="<?php echo $stamm ?>js/browser.js"></script>
	<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
	<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
	<script language="javascript" type="text/javascript">
		if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
		
		function changeuiuser(User,UserId) {
			document.getElementById("headlineSys").innerHTML = User;
			document.forms.configuration.userId.value=UserId;
		}
		
		function SubmitForm(Aktion) {
			if (Aktion=='cancel') {
				document.forms.configuration.action="configuration.php";
				document.forms.configuration.target="_self";
				document.forms.configuration.method="post";
			} else {
				if (Aktion=='change') document.getElementById("save_button").style.visibility="visible";
				if (!document.forms.configuration.devId.value && !document.forms.configuration.mgm_id.value &&  !(Aktion=='new_dev' || Aktion=='new_mgm' || Aktion=='save')) {
					document.forms.configuration.action="/no_device.php";
					document.forms.configuration.target="Change_Config_Frame";
					document.forms.configuration.method="post";
					document.getElementById("save_button").style.visibility="hidden";
				} else {
					document.forms.configuration.aktion.value = Aktion;
					document.forms.configuration.action="config_single_user.php";
					if (Aktion=='save') {
						document.forms.configuration.username.value			= top.Change_Config_Frame.document.forms.user_form.username.value;							
						document.forms.configuration.first_name.value		= top.Change_Config_Frame.document.forms.user_form.first_name.value;							
						document.forms.configuration.last_name.value		= top.Change_Config_Frame.document.forms.user_form.last_name.value;							
						document.forms.configuration.start_date.value		= top.Change_Config_Frame.document.forms.user_form.start_date.value;							
						document.forms.configuration.end_date.value			= top.Change_Config_Frame.document.forms.user_form.end_date.value;							
						document.forms.configuration.email.value			= top.Change_Config_Frame.document.forms.user_form.email.value;							
						document.forms.configuration.is_uiuser.value		= top.Change_Config_Frame.document.forms.user_form.is_uiuser.value;							
					}
					document.forms.configuration.target="Change_Config_Frame";
					document.forms.configuration.method="post";
					if (Aktion=='new_user') {
						document.getElementById("save_button").style.visibility="visible";
						document.forms.configuration.userId.value = '';
 						document.getElementById("headlineSys").innerHTML = '';
 					}
 					if (Aktion=='new_user' || (Aktion=='display' && document.forms.configuration.userId.value)) document.forms.configuration.action="config_single_user.php";
				}
			}
		}
	</script>
</head>

<body onLoad="changeColor1('n4');javascript:parent.document.getElementById('save_button').style.visibility='hidden';">

	<?php
		include("header.inc.php");
		include("navi_head.inc.php");
		include("navi_hor.inc.php");
		include("navi_vert_config_user.inc.php");
	?>
	<FORM id="configuration" name="configuration" action="" target="" method="post">
		<input type="hidden" name="userId" value=""/>
		<input type="hidden" name="aktion" value=""/>
		<input type="hidden" name="username" value=""/>
		<input type="hidden" name="first_name" value=""/>
		<input type="hidden" name="last_name" value=""/>
		<input type="hidden" name="start_date" value=""/>
		<input type="hidden" name="end_date" value=""/>
		<input type="hidden" name="email" value=""/>
		<input type="hidden" name="is_uiuser" value=""/>
		<div id="inhalt">&nbsp;
			<table>
			<tr><td>
			<table>
				<tr><td>&nbsp;</td>
					<td>&nbsp;</td>
					<td><input type="submit" value="Abbrechen" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('cancel');"/></td>
					<div id="save_button">
						<td>&nbsp;</td>
						<td><input type="submit" value="Speichern" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('save');"/></td>
					</div>
				</tr>
			</table>
			</td></tr>
			<tr><td>
			<br>
			<br>
			<table>
				<tr><td>&nbsp;</td>
					<td><input type="submit" value="Neu anlegen" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('new_dev');"/></td>
					<td>&nbsp;</td>
					<td><input type="submit" value="Anzeigen (Zur&uuml;cksetzen)" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('display');"/></td>
					<td>&nbsp;</td>
					<td><input type="submit" value="Bearbeiten" class="button" style="margin-right:15px;" onClick="javascript:SubmitForm('change');"/></td>
				</tr>
			</table>
			<br>
			</td></tr>
			</table>
		</div>
	</FORM>
	
	<iframe id="Change_Config_Frame" name="Change_Config_Frame" src="/leer.php">
		[Your user agent does not support frames or is currently configured not to display frames.]
	</iframe>
	
	<?php include ("leer.inc.php"); ?>
	
	<script language="javascript" type="text/javascript">
		Hoehe_Frame_setzen("Change_Config_Frame");
		position_iframe("Change_Config_Frame");
		/* for (var i = 0; i < top.frames.length; i++) alert(top.frames[i].name); */		
	</script>
</body>
</html>
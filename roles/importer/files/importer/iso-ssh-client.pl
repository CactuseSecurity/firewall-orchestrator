#! /usr/bin/perl -w
# $Id: iso-ssh-client.pl,v 1.1.2.8 2011-11-29 19:01:53 tim Exp $
# $Source: /home/cvs/iso/package/importer/Attic/iso-ssh-client.pl,v $
#program: sendcommand.pl 2009-2011
#author: Youri Reddy-Girard (melk0r101@yahoo.com - http//sendcommand.sourceforge.net/)
#version: 0.1.6 (20110213)
# SendCommand is a perl script using expect library. It permits the execution of remote
# command via telnet or ssh on different network devices (Cisco switch, Juniper Netscreen,
# Blue Coat proxySG, TippingPoint IPS, Linux). Output is highly customizable.
# IMPORTANT: You need the expect.pm module installed.
#   - on debian type "aptitude install libexpect-perl"
#   - on centos type "cpan" and then type "install Expect"
#

# calling syntax: ./netscreen-ssh-client.pl -z 89.19.225.167 -t netscreen -i .ssh/id_rsa -c "get config" -u cadmin -d 0 -o /tmp/publikat_fw02.cfg

#external librairies
use strict;
use warnings;
use diagnostics;
use File::Path;
use POSIX qw(strftime);
use Expect;
# $Expect::Debug = 9;
use Getopt::Long qw(:config no_ignore_case bundling);

#############################################################
# GLOBAL VARIABLE DECLARATION
#############################################################
#global constants
use constant PROGRAM => 'iso-ssh-client.pl';                      # name of this program / script
use constant VERSION => "1.1";                                    # version of the program
use constant SSH => "/usr/bin/ssh";                               # path to the ssh binary
use constant TELNET => "/usr/bin/telnet";                         # path to the telnet binary
use constant TIMEOUT_COMMAND => 600;                              # timeout in seconds after sending the command if no prompt is seen
use constant TIMEOUT_CONNECT => 120;                              # timeout in seconds after opening the socket to get the login prompt
use constant TIMEOUT_GENERIC => 10;                               # timeout in seconds used for all remaining expect commands
use constant MAXVAR => 9;                                         # Number of variables (columns) read in the input file
use constant NBTRY => 1;                                          # Default number of retry if connection fails
use constant DISPLAYLEVEL => 1;                                   # Default display level
use constant METHOD => "ssh";                                     # Default method
use constant OUTPUTFILEAPPEND => 0;                               # Default mode for output file (0=erase 1=append)
use constant PROMPT_REGEX_DEBIAN => "\r\n[^\r\n ]+:[^\r\n]+[\\\$#] ";
use constant PROMPT_REGEX_IPSO => "\r\n[^\r\n ]+\\\[[^\r\n]+\\\]#";
use constant PROMPT_REGEX_SPLAT => "\r\n\\\[[^\r\n ]+@[^\r\n]+\\\]#";
use constant PROMPT_REGEX_REDHAT => "\r\n\\\[[^\r\n ]+@[^\r\n]+\\\]#";
#use constant PROMPT_REGEX_GENTOO => "[^\n]+@[^\n]+ [^\n]+ \\\$|[^\n]+ [^\n]+ #";
use constant PROMPT_REGEX_POSIX => PROMPT_REGEX_DEBIAN."|".PROMPT_REGEX_IPSO."|".PROMPT_REGEX_SPLAT."|".PROMPT_REGEX_REDHAT;
use constant PROMPT_REGEX_BLUECOAT => "\r\n[^\r\n ]+>";
use constant PROMPT_REGEX_BLUECOATENABLE => "\r\n[^\r\n ]+#";
use constant PROMPT_REGEX_BLUECOATCONFIG => "\r\n[^\r\n ]+#([^)]+)";
use constant PROMPT_REGEX_CISCO => "\r\n[^\r\n# ]+>";
use constant PROMPT_REGEX_CISCOENABLE => "\r\n[^\r\n# ]+#";
#use constant PROMPT_REGEX_NETSCREEN => qr/\r\n.+?\(.\)\-\>/;	# das funktioniert mit clustern
use constant PROMPT_REGEX_NETSCREEN => 
	qr/\r\n(\-\-\-\smore\s\-\-\-)?([^\s]+?)\-\>/;		# annahme: prompt-Zeile enthält kein Whitespace vor ->
#use constant PROMPT_REGEX_NETSCREEN => "\r?\n?[^\r\n ]+([^\r\n ]+)->";
#use constant PROMPT_REGEX_TIPPINGPOINT => "\r\n[^\r\n ]+#";	# original
use constant PROMPT_REGEX_TIPPINGPOINT => "[\r\n]+[^\r\n ]+#";    #TippingPoint is bugged and returned tons of \r when no rows and columns are sent (TTY: telnet and ssh)


#global expect
$Expect::Log_Stdout = 0;

#global variables
my $PROMPT_REGEX;
my $LOGFILE;
my $DISPLAYLEVEL;



#############################################################
# FUNCTIONS DECLARATION
#############################################################

# append_array_to_file()
# Append an array to a file
# IN:
#   $file = filename
#   $parr = pointer to array to append
# OUT:
#   1 = OK
#   0 = NOK
sub append_array_to_file(){
  my $file = $_[0];
  my $parr = $_[1];
  my $result = 0;
  if (open(FILE, ">>", $file)){
    if (print FILE @$parr){
      $result = 1;
    }
    close(FILE);
  } 
}

# append_string_to_file()
# Append a string to a file
# IN:
#   $file = filename
#   $string = string to append
# OUT:
#   1 = OK
#   0 = NOK
sub append_string_to_file(){
  my $file = $_[0];
  my $string = $_[1];
  my $result = 0;
  if (open(FILE, ">>", $file)){
    if (print FILE $string){
      $result = 1;
    }
    close(FILE);
  } 
}

# exp_connect()
# Connects to any known equipment type 
# IN:
#   $exp = Expect object
#   $equipmenttype = Equipment type (must be supported)
#   $method = Method (must be supported)
#   $port = Port
#   $parrEquipment = Array containing equipmentname, hostname, username, password, enablepassword
# OUT:
#   1 = OK
#   0 = NOK
sub exp_connect(){
  my $exp = $_[0];
  my $equipmenttype = $_[1];
  my $method = $_[2];
  my $port = $_[3];
  my $parrEquipment = $_[4];
  my $identity_file = $_[5];
  my $result = 0; 
  if (($equipmenttype eq "posix") && ($method eq "ssh")){
    $result = &exp_connect_ssh($exp, @$parrEquipment[0], @$parrEquipment[1], @$parrEquipment[2], @$parrEquipment[3], $port, PROMPT_REGEX_POSIX, $identity_file);
  }elsif (($equipmenttype eq "posix") && ($method eq "telnet")){
    $result = &exp_connect_telnet($exp, @$parrEquipment[0], @$parrEquipment[1], @$parrEquipment[2], @$parrEquipment[3], $port, PROMPT_REGEX_POSIX);
  }elsif (($equipmenttype eq "netscreen") && ($method eq "ssh")){
    $result = &exp_connect_ssh($exp, @$parrEquipment[0], @$parrEquipment[1], @$parrEquipment[2], @$parrEquipment[3], $port, PROMPT_REGEX_NETSCREEN, $identity_file);
  }elsif (($equipmenttype eq "bluecoat")&&($method eq "ssh")){
    $result = &exp_connect_ssh($exp, @$parrEquipment[0], @$parrEquipment[1], @$parrEquipment[2], @$parrEquipment[3], $port, PROMPT_REGEX_BLUECOAT, $identity_file);
    if ($result){
      $result = &exp_enter_enable_mode($exp, @$parrEquipment[0], @$parrEquipment[4], PROMPT_REGEX_BLUECOATENABLE);
      if ($result){
        $result = &exp_enter_config_mode($exp, @$parrEquipment[0], PROMPT_REGEX_BLUECOATCONFIG);
        if ($result){
          my $commandresult = "";
          my $sendcommandresult = 0;
          $sendcommandresult = &exp_send_command($exp, $equipmenttype, @$parrEquipment[0], "line-vty ;mode", \$commandresult);
          if ($sendcommandresult){
            $sendcommandresult = &exp_send_command($exp, $equipmenttype, @$parrEquipment[0], "no length", \$commandresult);            
            if ($sendcommandresult){
              $sendcommandresult = &exp_send_command($exp, $equipmenttype, @$parrEquipment[0], "exit", \$commandresult);            
              if (!$sendcommandresult){
                &log_msg("@$parrEquipment[0]: error when exiting line-vty mode");
              }
            }else{
              &log_msg("@$parrEquipment[0]: error when entering line-vty mode");          
            }
          }else{
            &log_msg("@$parrEquipment[0]: error when entering line-vty mode");          
          }
          $result = $sendcommandresult;
          if ($result){
            $result = &exp_exit_config_mode($exp, @$parrEquipment[0], PROMPT_REGEX_BLUECOATENABLE);
          }
        }
      }
    }
  }elsif (($equipmenttype eq "cisco")&&($method eq "ssh")){
    $result = &exp_connect_ssh($exp, @$parrEquipment[0], @$parrEquipment[1], @$parrEquipment[2], @$parrEquipment[3], $port, PROMPT_REGEX_CISCO);
    if ($result){
      $result = &exp_enter_enable_mode($exp, @$parrEquipment[0], @$parrEquipment[4], PROMPT_REGEX_CISCOENABLE);
      if ($result){
        my $commandresult = "";
        my $sendcommandresult = &exp_send_command($exp, $equipmenttype, @$parrEquipment[0], "term length 0", \$commandresult);
        if (!$sendcommandresult){
          &log_msg("@$parrEquipment[0]: error when setting term length");
        }
        $result = $sendcommandresult; 
      }
    }
  }elsif (($equipmenttype eq "cisco")&&($method eq "telnet")){
    $result = &exp_connect_telnet($exp, @$parrEquipment[0], @$parrEquipment[1], @$parrEquipment[2], @$parrEquipment[3], $port, PROMPT_REGEX_CISCO);
    if ($result){
      $result = &exp_enter_enable_mode($exp, @$parrEquipment[0], @$parrEquipment[4], PROMPT_REGEX_CISCOENABLE);
      if ($result){
        my $commandresult = "";
        my $sendcommandresult = &exp_send_command($exp, $equipmenttype, @$parrEquipment[0], "term length 0", \$commandresult);
        if (!$sendcommandresult){
          &log_msg("@$parrEquipment[0]: error when setting term length");
        }
        $result = $sendcommandresult;              
      }
    }
  }elsif (($equipmenttype eq "tippingpoint") && ($method eq "ssh")){
    $result = &exp_connect_ssh($exp, @$parrEquipment[0], @$parrEquipment[1], @$parrEquipment[2], @$parrEquipment[3], $port, PROMPT_REGEX_TIPPINGPOINT);
  }else{
    &log_msg("Connection for equipmenttype and method combination undefined. (equipmenttype=$equipmenttype method=$method)");
  }
  return $result;
}

# exp_connect_ssh
# Expect sequences to connect to a system via ssh
# IN:
#   $exp = Expect object
#   $equipmentname = equipment name / id / label
#   $hostname = hostname or ip address
#   $username = remote username
#   $password = remote password
#   $port = remote ssh port
#   $prompt_regex = prompt expected
# OUT:
#   1 = OK
#   0 = NOK
sub exp_connect_ssh(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $hostname = $_[2];
  my $username = $_[3];
  my $password = $_[4];
  my $port = $_[5];
  my $prompt_regex = $_[6]; 
  my $identity_file = $_[7]; 
  my $result=0;
  
	if (defined($identity_file) && $identity_file ne '') {
		$identity_file = " -i $identity_file";
	} else {
		$identity_file = '';
	}

  my $port_txt = ' ';
  if ($port ne "") { $port_txt = " -p $port "; }
  $exp->spawn(SSH." $identity_file -T $port_txt $username\@$hostname");
  &log_msg("$equipmentname: connecting via ssh $port_txt to $username\@$hostname...");
  $exp->notransfer(1);
  $exp->expect(TIMEOUT_CONNECT,[
    "-re","Terminal type",sub{  #ipso specific
      $exp->set_accum($exp->after);
      &log_msg("$equipmentname: terminal type requested. sending CR");    
      $exp->send("\r");
      $exp->exp_continue;
    }
  ],[
    "-re",$prompt_regex,sub{
      $exp->notransfer(0);
      $PROMPT_REGEX = $prompt_regex;
      &log_msg("$equipmentname: login successfully");
      $result=1;
    }
  ],[
    "-re","[^\n]+assword:",sub{      
      $exp->notransfer(0);
      $exp->set_accum($exp->after);
      if($password){
        $exp->send("$password\r");
        $exp->notransfer(1);
        $exp->expect(TIMEOUT_GENERIC,[
            "-re","Terminal type",sub{  #ipso specific
            $exp->set_accum($exp->after);
            &log_msg("$equipmentname: terminal type requested. sending CR");    
            $exp->send("\r");
            $exp->exp_continue;
          }
        ],[
          "-re",$prompt_regex,sub{
            $exp->notransfer(0);
            $PROMPT_REGEX = $prompt_regex;
            &log_msg("$equipmentname: login successfully");
            $result=1;
          }
        ],[
          "-re","[^\n]+assword:",sub{
            $exp->set_accum($exp->after);
            $exp->notransfer(0);
            &log_error("$equipmentname: invalid username/password");
            $exp->hard_close();
            $result=0;
          }
        ],[
          timeout => sub{
            $exp->set_accum($exp->after);
            $exp->notransfer(0);
            &log_error("$equipmentname: timeout due to unknown input after login");
            $exp->hard_close();
            $result=0;
          }      
        ]);
      }else{
        &log_error("$equipmentname: password requested but no password supplied. disconnecting");
        $exp->hard_close();
        $result=0;      
      }
    }  
  ],[
    "-re","[^\n]+not known",sub{
      $exp->notransfer(0);
      $exp->set_accum($exp->after);
      &log_error("$equipmentname: unknown hostname $hostname");
      $exp->soft_close();
      $result=0;
    }
  ],[
    "-re","[^\n]+refused",sub{
      $exp->notransfer(0);
      $exp->set_accum($exp->after);
      &log_error("$equipmentname: connection refused to $hostname");
      $exp->soft_close();
      $result=0;
    }
  ],[
    "-re","Host key verification failed",sub{
      $exp->notransfer(0);
      $exp->set_accum($exp->after);
      &log_error("$equipmentname: Host $hostname key verification failed");
      $exp->soft_close();
      $result=0;
    }
  ],[
    "Are you sure you want to continue connecting (yes/no)?",sub{
      $exp->set_accum($exp->after);
      &log_msg("$equipmentname: new key fingerprint");
      $exp->send("yes\r");
      $exp->exp_continue;
    }
  ],[
    timeout => sub{
      if ($exp->before() eq ""){
        &log_error("$equipmentname: connection timeout");
      }else{
        &log_error("$equipmentname: timeout due to unknown input before login");
      }
      $result=0;
    }
  ]);
  return $result;
}


# exp_connect_telnet
# Expect sequences to connect to a system via telnet
# IN:
#   $exp = Expect object
#   $equipmentname = equipment name / id / label
#   $hostname = hostname or ip address
#   $username = remote username
#   $password = remote password
#   $port = remote ssh port
#   $prompt_regex = prompt expected
# OUT:
#   1 = OK
#   0 = NOK
sub exp_connect_telnet(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $hostname = $_[2];
  my $username = $_[3];
  my $password = $_[4];
  my $port = $_[5];
  my $prompt_regex = $_[6]; 
  my $result=0;
  
  if ($port ne ""){
    $exp->spawn(TELNET." $hostname $port");
    &log_msg("$equipmentname: connecting via telnet to $hostname on port $port...");
  }else{
    $exp->spawn(TELNET." $hostname");
    &log_msg("$equipmentname: connecting via telnet to $hostname...");
  }
  
  $exp->expect(TIMEOUT_CONNECT,[
    "-re","[^\n]+incorrect",sub{
      $exp->set_accum($exp->after);
      $exp->notransfer(0);
      &log_error("$equipmentname: invalid username");
      $exp->hard_close();
      $result=0;
    }
  ],[
    "-re","[^\n]+ogin:|Username:",sub{
      $exp->send("$username\r");
      &log_msg("$equipmentname: login prompt detected. sending username (username=$username)");      
      $exp->exp_continue;
    }
  ],[
    "-re","[^\n]+assword:",sub{      
      if($password){
        $exp->send("$password\r");
        $exp->notransfer(1);
        $exp->expect(TIMEOUT_GENERIC,[
          "-re","Terminal type",sub{  #ipso specific
            $exp->set_accum($exp->after);
            &log_msg("$equipmentname: terminal type requested. sending CR");    
            $exp->send("\r");
            $exp->exp_continue;
          }
        ],[
          "-re",$prompt_regex,sub{
            $exp->notransfer(0);
            $PROMPT_REGEX = $prompt_regex;
            &log_msg("$equipmentname: login successfully");
            $result=1;
          }
        ],[
          "-re","[^\n]+incorrect|[^\n]+invalid",sub{
            $exp->set_accum($exp->after);
            $exp->notransfer(0);
            &log_error("$equipmentname: invalid username/password");
            $exp->hard_close();
            $result=0;
          }
        ],[
          "-re","[^\n]+assword:",sub{
            $exp->set_accum($exp->after);
            $exp->notransfer(0);
            &log_error("$equipmentname: invalid username/password");
            $exp->hard_close();
            $result=0;
          }
        ],[
          timeout => sub{
            $exp->set_accum($exp->after);
            $exp->notransfer(0);
            &log_error("$equipmentname: timeout due to unknown input after login");
            $exp->hard_close();
            $result=0;
          }      
        ]);
      }else{
        &log_error("$equipmentname: password requested but no password supplied. disconnecting");
        $exp->hard_close();
        $result=0;      
      }
    }  
  ],[
    "-re","[^\n]+not known",sub{
      $exp->notransfer(0);
      $exp->set_accum($exp->after);
      &log_error("$equipmentname: unknown hostname $hostname");
      $exp->soft_close();
      $result=0;
    }
  ],[
    "-re","[^\n]+refused",sub{
      $exp->notransfer(0);
      $exp->set_accum($exp->after);
      &log_error("$equipmentname: connection refused to $hostname");
      $exp->soft_close();
      $result=0;
    }
  ],[
    timeout => sub{
      if ($exp->before() eq ""){
        &log_error("$equipmentname: connection timeout");
      }else{
        &log_error("$equipmentname: timeout due to unknown input before login");
      }
      $result=0;
    }
  ]);
  return $result;
}

# exp_exit_config_mode
# Expect sequences to exit config mode
# IN:
#   $exp = Expect object
#   $equipmentname = equipment name / id / label
#   $promptenable_regex = prompt expected in enable mode
# OUT:
#   1 = OK
#   0 = NOK
sub exp_exit_config_mode(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $promptenable_regex = $_[2];
  my $result=0;
  
  $exp->expect(TIMEOUT_GENERIC,[
    "-re",$PROMPT_REGEX,sub{
      $exp->send("exit\r");
      $exp->notransfer(1);
      $exp->expect(TIMEOUT_GENERIC,[
        "-re",$promptenable_regex,sub{
          $exp->notransfer(0);
          $PROMPT_REGEX=$promptenable_regex;
          &log_msg("$equipmentname: exiting config mode successfully");
          $result=1;
        }
      ],[
        timeout => sub{
          $exp->notransfer(0);
          &log_error("$equipmentname: timeout due to unknown input after trying to exit config mode");
          $exp->hard_close();
          $result=0;
        }      
      ]);    
    }
  ],[
    timeout => sub{
      $exp->notransfer(0);
      &log_error("$equipmentname: timeout due to unknown input before trying to exit config mode");
      $exp->hard_close();
      $result=0;
    }      
  ]);
  return $result;
}

# exp_enter_config_mode
# Expect sequences to enter config mode
# IN:
#   $exp = Expect object
#   $equipmentname = equipment name / id / label
#   $promptconfig_regex = prompt expected in config mode
# OUT:
#   1 = OK
#   0 = NOK
sub exp_enter_config_mode(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $promptconfig_regex = $_[2];
  my $result=0;
  
  $exp->expect(TIMEOUT_GENERIC,[
    "-re",$PROMPT_REGEX,sub{
      $exp->send("configure terminal\r");
      $exp->notransfer(1);
      $exp->expect(TIMEOUT_GENERIC,[
        "-re",$promptconfig_regex,sub{
          $exp->notransfer(0);
          $PROMPT_REGEX=$promptconfig_regex;
          &log_msg("$equipmentname: entering config mode successfully");
          $result=1;
        }
      ],[
        timeout => sub{
          $exp->notransfer(0);
          &log_error("$equipmentname: timeout due to unknown input after trying to enter config mode");
          $exp->hard_close();
          $result=0;
        }      
      ]);    
    }
  ],[
    timeout => sub{
      $exp->notransfer(0);
      &log_error("$equipmentname: timeout due to unknown input before trying to enter config mode");
      $exp->hard_close();
      $result=0;
    }      
  ]);
  return $result;
}

# exp_enter_enable_mode
# Expect sequences to enter enable mode
# IN:
#   $exp = Expect object
#   $equipmentname = equipment name / id / label
#   $enablepassword = remote enablepassword
#   $promptenable_regex = prompt expected in enable mode
# OUT:
#   1 = OK
#   0 = NOK
sub exp_enter_enable_mode(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $enablepassword = $_[2];
  my $promptenable_regex = $_[3];
  my $result=0;
  
  $exp->expect(TIMEOUT_GENERIC,[
    "-re",$PROMPT_REGEX,sub{
      $exp->send("enable\r");          
      $exp->expect(TIMEOUT_GENERIC,[
        "-re","[^\n]+assword:",sub{
            $exp->send("$enablepassword\r");
            $exp->notransfer(1);
            $exp->expect(TIMEOUT_GENERIC,[
              "-re",$promptenable_regex,sub{
                $exp->notransfer(0);
                $PROMPT_REGEX=$promptenable_regex;
                &log_msg("$equipmentname: entering enable mode successfully");
                $result=1;
              }
            ],[
              "-re","[^\n]+assword:",sub{
                $exp->notransfer(0);
                &log_error("$equipmentname: invalid enable password");
                $exp->hard_close();
                $result=0;
              }
            ],[
              timeout => sub{
                $exp->notransfer(0);
                &log_error("$equipmentname: timeout due to unknown input after enable password has been sent");
                $exp->hard_close();
                $result=0;
              }      
            ]);    
          }
      ],[
        timeout => sub{
          &log_error("$equipmentname: timeout due to unknown input after enable command");
          $exp->hard_close();
          $result=0;
        }      
      ]);          
    }
  ],[
    timeout => sub{
      $exp->notransfer(0);
      &log_error("$equipmentname: timeout due to unknown input before sending enable command");
      $exp->hard_close();
      $result=0;
    }      
  ]);
  return $result;
}

# exp_disconnect()
# Disconnect from equipment 
# IN:
#   $exp = Expect object
#   $equipmenttype = Equipment type (must be supported)
#   $method = Method (must be supported)
#   $parrEquipment = Array containing equipmentname, hostname, username, password, enablepassword
# OUT:
#   1 = OK
#   0 = NOK
sub exp_disconnect(){
  my $exp = $_[0];
  my $equipmenttype = $_[1];
  my $method = $_[2];
  my $parrEquipment = $_[3];
  my $result; 
  if (($equipmenttype eq "posix")||($equipmenttype eq "bluecoat")||($equipmenttype eq "cisco")||($equipmenttype eq "netscreen")){
    $result = &exp_disconnect_generic_exit($exp, @$parrEquipment[0]);  
  }elsif (($equipmenttype eq "tippingpoint")){
    $result = &exp_disconnect_generic_quit($exp, @$parrEquipment[0]);
  }else{
    &log_msg("Disconnection for equipmenttype and method combination undefined. (equipmenttype=$equipmenttype method=$method)");
  }
  return !$result;
}

# exp_disconnect_generic_exit
# Expect sequences - disconnect from generic system (ie: send "exit")
# IN:
#   $equipmentname = equipment name / id / label
# OUT:
#   1 = OK
#   0 = NOK
sub exp_disconnect_generic_exit(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $result=0;
  $exp->expect(TIMEOUT_GENERIC,[
    "-re",$PROMPT_REGEX,sub{
      $exp->send("exit\r");
      $exp->expect(TIMEOUT_GENERIC,[
        "-re","Configuration modified, save?",sub{  #netscreen specific
          &log_msg("$equipmentname: equipment wants to save config, sending no");    
          $exp->send("n\r");
          $exp->exp_continue;
        }
      ],[
        "-re","onnection[^\n]+closed",sub{
          &log_msg("$equipmentname: clean disconnect");
          $exp->soft_close();
          $result=1;
        }
      ],[
        timeout => sub{
          &log_error("$equipmentname: timeout due to unknown input after disconnect");
          $exp->soft_close();
          $result=0;
        }
      ]);
    }
  ],[
    timeout => sub{
      log_error("$equipmentname: timeout due to unknown input before disconnect");
      $exp->soft_close();
      $result=0;
    }
  ]);
  return $result;
}

# exp_disconnect_generic_quit
# Expect sequences - disconnect from generic system (ie: send "quit")
# IN:
#   $equipmentname = equipment name / id / label
# OUT:
#   1 = OK
#   0 = NOK
sub exp_disconnect_generic_quit(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $result=0;
  $exp->expect(TIMEOUT_GENERIC,[
    "-re",$PROMPT_REGEX,sub{
      $exp->send("quit\r");
      $exp->expect(TIMEOUT_GENERIC,[
        timeout => sub{
          &log_error("$equipmentname: timeout due to unknown input after disconnect");
          $exp->soft_close();
          $result=0;
        }
      ]);
    }
  ],[
    timeout => sub{
      log_error("$equipmentname: timeout due to unknown input before disconnect");
      $exp->soft_close();
      $result=0;
    }
  ]);
  return $result;
}

# exp_send_command
# Send command to equipment 
# IN:
#   $exp = Expect object
#   $equipmenttype = Type of equipment
#   $equipmentname = Equipment name
#   $command = command to be sent
#   $pcommandresult = pointer to commandresult variable in order to bring back result
# OUT:
#   1 = OK
#   0 = NOK
sub exp_send_command(){
  my $exp = $_[0];
  my $equipmenttype = $_[1];
  my $equipmentname = $_[2];
  my $command = $_[3];
  my $pcommandresult = $_[4];
  my $result; 
  if (($equipmenttype eq "posix")||($equipmenttype eq "bluecoat")||($equipmenttype eq "cisco")||($equipmenttype eq "netscreen")||($equipmenttype eq "tippingpoint")){
    $result = &exp_send_command_generic($exp, $equipmentname, $command, $pcommandresult);  
  }else{
    &log_msg("Send command for equipmenttype undefined. (equipmenttype=$equipmenttype)");
  }
  return $result;
}

  sub print_debug_expect {
  	my $before = shift;
  	my $match = shift;
  	my $after = shift;
          print("\n\n>>>>>TESTING-START<<<<<<\n");
          print("\n>>>BEFORE:###".$before."###\n");
          print("\n>>>MATCH:###".$match."###\n");
          print("\n>>>AFTER:###".$after."###\n");
          print("\n>>>>>TESTING-END<<<<<<\n");
  }

# exp_send_command_generic
# Expect sequences to send command to remote equipment
# IN:
#   $exp = Expect object
#   $equipmentname = Equipment name
#   $command = command to be sent
#   $pcommandresult = pointer to commandresult variable in order to bring back result
# OUT:
#   1 = OK
#   0 = NOK
sub exp_send_command_generic(){
  my $exp = $_[0];
  my $equipmentname = $_[1];
  my $command = $_[2];
  my $pcommandresult = $_[3];
  my $result=0;
  my $more = "--- more --- ";
  
  $exp->expect(TIMEOUT_GENERIC,[
    "-re",$PROMPT_REGEX,sub{      
      $exp->send("$command\r");
#      $exp->notransfer(1);
      $exp->expect(TIMEOUT_COMMAND,[
        "-re",$PROMPT_REGEX,sub{
#          $exp->notransfer(0);
          log_msg("$equipmentname: command sent successfully (cmd = $command)");
          ${$pcommandresult} .= $exp->before();         
#          ${$pcommandresult} .= substr($exp->before(),index($exp->before(),"\n")+1)."\r\n";         
#          print("\n\n>>>>>TESTING-START<<<<<<\n");
#          my $before=$exp->before();
#          $before=~s/\r/\\r/g;
#          $before=~s/\n/\\n/g;
#          my $match=$exp->match();
#          $match=~s/\r/\\r/g;
#          $match=~s/\n/\\n/g;
#          my $after=$exp->after();
#          $after=~s/\r/\\r/g;
#          $after=~s/\n/\\n/g;
#          print("\n>>>BEFORE:###".$before."###\n");
#          print("\n>>>MATCH:###".$match."###\n");
#          print("\n>>>AFTER:###".$after."###\n");
#          print("\n>>>>>TESTING-END<<<<<<\n");
          $result=1;
          $exp->send("\r\n");  # sending line break for exit match
        }
      ],[
        "-re",$more,sub{
#          $exp->notransfer(0);
          log_msg("$equipmentname: found --- more --- ");
#          ${$pcommandresult} .= substr($exp->before(),index($exp->before(),"\n")+1);
           ${$pcommandresult} .= $exp->before();
#          &print_debug_expect ($exp->before(), $exp->match(), $exp->after());
          $exp->send(" \n");
#         $exp->notransfer(1);
          $exp->exp_continue;
        }
      ],[
        "-re","onnection[^\n]+closed",sub{
#          $exp->notransfer(0);
          log_msg("$equipmentname: connection closed while waiting for prompt");
          $exp->soft_close();         
          $result=1;
        }
#      ],[
#        "-re",$PROMPT_REGEX,sub{
#          $exp->notransfer(0);
#          print("\n\n>>>>>TESTING-START<<<<<<\n");
#          my $before=$exp->before();
#          $before=~s/\r/\\r/g;
#          $before=~s/\n/\\n/g;
#          my $match=$exp->match();
#          $match=~s/\r/\\r/g;
#          $match=~s/\n/\\n/g;
#          my $after=$exp->after();
#          $after=~s/\r/\\r/g;
#          $after=~s/\n/\\n/g;
#          print("\n>>>BEFORE:###".$before."###\n");
#          print("\n>>>MATCH:###".$match."###\n");
#          print("\n>>>AFTER:###".$after."###\n");
#          print("\n>>>>>TESTING-END<<<<<<\n");
#          print("\n\n");
#          $result=0;
#        }        
      ],[
        timeout => sub{
#          $exp->notransfer(0);
          log_error("$equipmentname: timeout due to unknown input after command");
#          print("\n>>>PROMPT_REGEX:###".$PROMPT_REGEX."###\n");
#          print("\n>>>BEFORE:###".$exp->before()."###\n");
#          print("\n>>>MATCH:###".$exp->match()."###\n");
#          print("\n>>>AFTER:###".$exp->after()."###\n");
          $exp->soft_close();
          $result=0;
        }
      ]);
    }
  ],[
    timeout => sub{
      log_error("$equipmentname: timeout due to unknown input before command");
      $exp->soft_close();
      $result=0;
    }
  ]);
  return $result;
}

# fill_arrCommandFile_from_dir()
# Fill array of commandfiles, outputfiles from the commandirectory
# IN:
#   $passCommandFileOutputFileFile = Pointer to associative array of commandfiles and outputfiles
#   $commanddir = directory containing commandfiles
# OUT:
#   1 = OK
#   0 = NOK
sub fill_arrCommandFile_from_dir(){
  my $passCommandFileOutputFileFile = $_[0];
  my $commanddir = $_[1];
  my @arrCommandFile;
  my @arrOutputFileFile;    
  my $numcommandfiles = 0;
  my $result = 0;
  opendir(DIR,$commanddir);
  @arrCommandFile = grep {/.*\.C/ && -f "$commanddir/$_"} readdir(DIR);
  closedir(DIR);
  opendir(DIR,$commanddir);
  @arrOutputFileFile  = grep {/.*\.O/ && -f "$commanddir/$_"} readdir(DIR);
  closedir(DIR);
  for (@arrCommandFile) {
    $_ = $commanddir."/".$_;
    $_ =~ s/\/\//\//g;        # replace // with /
  }
  for (@arrOutputFileFile) {
    $_ = $commanddir."/".$_;
    $_ =~ s/\/\//\//g;        # replace // with /
  }
  
  for (@arrCommandFile) {
    my $commandfile = $_;
    my $commandfileprefix = $commandfile;
    my $outputfilematched = "";
    $commandfileprefix =~ s/.C$//g;   
    for (@arrOutputFileFile) {
      my $outputfile = $_;
      my $outputfileprefix = $outputfile;
      $outputfileprefix =~ s/.O$//g;
      if ($commandfileprefix eq $outputfileprefix){
        $outputfilematched = $outputfile; 
        last;
      }
    }
    $$passCommandFileOutputFileFile{$commandfile} = $outputfilematched;
  }
  $numcommandfiles = keys(%$passCommandFileOutputFileFile);  
  if ($numcommandfiles ne ""){
    $result = 1;
  }
  return $result;
}

# get_directory()
# Returns the directory part of a fullpath
# IN:
#   $fullpath = path to file 
# OUT:
#   $directory = directory containing $fullpath 
sub get_directory(){
  my $fullpath = $_[0];
  my $directory = $fullpath;
  $directory =~ s/[^\/]*$//g;
  return $directory; 
}


# get_timestamp()
# Return timestamp
# IN:
#   $msg = error message
sub get_timestamp(){
  return (strftime "%Y%m%d %H:%M:%S", localtime);
}


# is_directory()
# Returns true if $directory is a real directory
# IN:
#   $directory = directory to verify
# OUT:
#   1 = TRUE
#   0 = FALSE
sub is_directory(){
  my $directory = $_[0];
  my $result = 0;
  if (opendir(DIR,$directory)){
    $result = 1;
    closedir(DIR);
  }
  return $result; 
}



# log_error()
# Log error message using default settings
# IN:
#   $msg = error message
sub log_error(){
  my $msg = $_[0];
  my $timestamp = &get_timestamp();
  if ($DISPLAYLEVEL>=1 && $DISPLAYLEVEL<=4){
    print("[LOGFILE: $timestamp $msg]\n");
  }elsif ($DISPLAYLEVEL>=5){
    print("\n[LOGFILE: $timestamp $msg]\n");
  }
  if ($LOGFILE ne ""){
    &append_string_to_file($LOGFILE, "$timestamp $msg\n");  
  } 
}

# log_msg()
# Log message using default settings
# IN:
#   $msg = error message
sub log_msg(){
  my $msg = $_[0];
  my $timestamp = &get_timestamp();
  if ($DISPLAYLEVEL==4){
    print("[LOGFILE: $timestamp $msg]\n");
  }elsif ($DISPLAYLEVEL>=5){
    print("\n[LOGFILE: $timestamp $msg]\n");
  }
  if ($LOGFILE ne ""){
    &append_string_to_file($LOGFILE, "$timestamp $msg\n");  
  } 
}

sub normalize_commandresult {
	my $pinput = $_[0];
	my $prompt = $_[1];
	my $result = '';
	my $line_count = 0;
	foreach my $line (split(/\n/, ${$pinput})) {
		$line =~ s/\r//g; 
		$line =~ s/\s+$//;
		if ($line =~ /(\010)+(.*?)$/) { $line = $2;}
		if ($line_count>0 && $line !~ /^.+?\(\w\)\-\>/ && $line !~ /^\-+$/ && $line !~ /^--- more/) { $result .= "$line\n"; }
		# remove first line which contains the command
		# remove trailing line if it contains prompt
		# remove trailing line if it contains --------------------
		# remore --- more relict which only happens if last line contains more when paging config
		$line_count++;
	}
	${$pinput} = $result;
}

# process_commandresult()
# Display to stdout depending of settings and send to outputfile
# IN:
#   $parrEquipment = array containing equipmentname, hostname, username, password, enablepassword 
#   $command = command sent to equipment
#   $commandresult = command result 
#   $outputfile = file to put command result in 
#   $outputfileappend = if true then append instead of writing.
# OUT:
#   1 = OK
#   0 = NOK
sub process_commandresult(){
  my $parrEquipment = $_[0];
  my $command = $_[1];
  my $commandresult = $_[2];
  $commandresult =~ s/\r\r\n\r\n\r\n\r/\r\n/g; #special replacement for tippingpoint columns and rows display bug 
  my $outputfile = $_[3];
  my $outputfileappend = $_[4];
  if ($DISPLAYLEVEL==0){
  }elsif ($DISPLAYLEVEL==1){
    print($commandresult);
  }elsif ($DISPLAYLEVEL==2){
    print(@$parrEquipment[0]." ".$commandresult);
  }elsif ($DISPLAYLEVEL>=3){
    print("--------------------------------------------------------------\n");
    print("EQUIPMENT: ".@$parrEquipment[0]."\n");
    print("COMMAND: ".$command."\n");
    print("RESULT: ".$commandresult);
    if ($outputfile ne ""){
      print("OUTPUTFILE: ".$outputfile."\n");
    } 
    print("--------------------------------------------------------------\n");
    print("\n");
  }
  if ($outputfile ne ""){
    my $directory = &get_directory($outputfile);
    if ($directory ne "" && !&is_directory($directory)){
      &log_msg("@$parrEquipment[0]: creating $directory because it does not exist");
      mkpath($directory);
    }
    #if outputfileappend is true then we append to existing files
    if ($outputfileappend){
      &log_msg("@$parrEquipment[0]: appending command result to output file $outputfile");
      &append_string_to_file($outputfile, $commandresult);
    
    } else {
      &log_msg("@$parrEquipment[0]: writing command result to output file $outputfile");
      &write_string_to_file($outputfile, $commandresult);
    }
  }
}

# show_help()
# Print out usage with complete details
sub show_help() {
  show_version();
  show_usage();
  print "Parameter details:
-i inputfile : File containing the list of equipment with credentials. Expected format:
   equipmentname1 hostname1 username1 password1 enablepassword1
   equipmentname2 hostname2 username2 password2 enablepassword2
   (...)
-t equipmenttype : Type of equipment. Valid values: posix, bluecoat, cisco, netscreen, tippingpoint.
-c command : Command to run on remote equipment.
-C commandir : Directory containing many commands to be run in one connection on a remote equipment. It must have the following content:
   Files named *.C : Contain command(s) to run.
   Files named *.O : Contain name of output file for corresponding commands.
-m method : Method used to access equipment. Valid values: ssh, telnet. Default is ssh.
-p port : Specify port in order to access equipment. Default is using standard port for service.
-r numretry : Number of times the script will try to connect to the equipment on timeout. Default is ".NBTRY."
-d displaylevel : Level of information displayed on stdout. Expected level are:
   0 : Nothing displayed.
   1 : Result of command displayed. DEFAULT.
   2 : Equipment's name and command results displayed on one line.
   3 : Equipment's name, command and command results all displayed on multiple lines.
   4 : Equipment's name, command and command results all displayed on multiple lines. Logs displayed.
   5 : Equipment's name, command and command results all displayed on multiple lines. Logs displayed. Session logs displayed.
-o outputfile : File where results of command will be saved.
-a : append to output file instead of writing new content.
-l logfile : File where logs will be saved.
-L sessionlogfile : File where session logs (the entire remote session) will be saved.
-v : show version.
-h : show this help.

User variables:
It is possible to use variables in the output file and in the command string. Use the following syntax:
   \%VAR0\% = equipmentname
   \%VAR1\% = hostname
   (...)
   \%VAR".MAXVAR."\% = custom variables
   
Concrete examples:
   ".PROGRAM." -i login.txt -t posix -m ssh -c \"uptime\"
   ".PROGRAM." -i login.txt -t posix -m ssh -c \"uptime\" -d 0 -o \"%VAR0%-uptime.txt\"

"; 
}

# show_usage()
# Print out usage
sub show_usage() {
  print("usage: ".PROGRAM." -z zielhost -u username -t equipmenttype -i identity-file -c command [-C commanddir] [-m method] [-p port] [-r numretry] [-d displaylevel] [-o outputfile] [-a] [-l logfile] [-L sessionlogfile] [-v] [-h]\n\n");
}

# show_version()
# Print out usage
sub show_version() {
  print(PROGRAM." v".VERSION." by Youri Reddy-Girard http://sendcommand.sourceforge.net/\n\n");
}

# validate_arguments()
# Validate arguments received by command line argument
# IN:
#   $inputfile = Filename containing the list of equipment with credentials.
#   $equipmenttype = Type of equipment (posix, bluecoat, cisco or netscreen).
#   $command = Command to run on remote equipment.
#   $commanddir = Directory containing commands and output files.
#   $method = Method used to access equipment (ssh, telnet).
#   $port = Port to connect to
#   $numretry = Number of times the script will try to connect to the equipment on timeout
#   $displaylevel = Level on information displayed on stdout.
#   $outputfile = File that will contain the result of the command.
#   $outputfileappend = Append to output file instead of writing new content.
#   $logfile = File where logging will take place.
#   $sessionlogfile = File where session logging will take place.
#   $version = if variable defined, show version.
#   $help = if variable defined, show help.
# OUT:
#   1 = OK
#   0 = NOK 
sub validate_arguments(){
  my $inputfile = $_[0];
  my $equipmenttype = $_[1];
  my $command = $_[2];
  my $commanddir = $_[3];
  my $method = $_[4];
  my $port = $_[5];
  my $numretry = $_[6];
  my $displaylevel = $_[7];
  my $outputfile = $_[8];
  my $outputfileappend = $_[9];
  my $logfile = $_[10];
  my $sessionlogfile = $_[11];
  my $version = $_[12];
  my $help = $_[13];
  my $result = 1;
  if ($inputfile eq ""){
    printf("ERROR - missing inputfile.\n");
    $result = 0;
  }
  if ($equipmenttype eq ""){
    printf("ERROR - missing equipment type.\n");
    $result = 0;
  }elsif ( $equipmenttype ne "posix" && $equipmenttype ne "bluecoat" && $equipmenttype ne "cisco" && $equipmenttype ne "netscreen" && $equipmenttype ne "tippingpoint"){
    printf("ERROR - Unknown equipment type. Valid values are posix, bluecoat, cisco, netscreen and tippingpoint.\n");
    $result = 0;
  }
  if (($method ne "ssh") && ($method ne "telnet")){
    printf("ERROR - unreconized method $method.\n");
    $result = 0;
  }  
  if (($port ne "")&&(!(($port>0)&&($port<65535)))){
    printf("ERROR - port must be numeric between 0 and 65534.\n");
    $result = 0;
  }
  if (($command eq "") && ($commanddir eq "")){
    printf("ERROR - missing command.\n");
    $result = 0;
  }
  if ($commanddir ne ""){
    if (&is_directory($commanddir)){
    }else{
      printf("ERROR - commanddir $commanddir is not a directory.\n");
      $result = 0;  
    }    
  }
  return $result;
}  

# validate_equipment()
# Validate equipment input file
# IN:
#   equipmenttype : Type of equipment (posix, bluecoat, ...)
#   arrEquipment : Array containing equipmentname, hostname, username, password, enablepassword
# OUT:
#   1 = OK
#   0 = NOK
sub validate_equipment(){
  my $equipmenttype = $_[0];
  my $parrEquipment = $_[1];
  my $result = 0;  
  my $size = @$parrEquipment;
  #if (($equipmenttype eq "posix") && ($size==3 || $size==4)){
  if (($equipmenttype eq "posix") && ($size>=3)){
    $result = 1;
  #}elsif ($equipmenttype eq "bluecoat" && $size==5){
  }elsif ($equipmenttype eq "bluecoat" && $size>=5){
    $result = 1;  
  #}elsif ($equipmenttype eq "cisco" && $size==5){
  }elsif ($equipmenttype eq "cisco" && $size>=5){
    $result = 1;  
  #}elsif ($equipmenttype eq "netscreen" && $size==4){
  }elsif ($equipmenttype eq "netscreen" && $size>=4){
  $result = 1;  
  #}elsif ($equipmenttype eq "tippingpoint" && $size==4){
  }elsif ($equipmenttype eq "tippingpoint" && $size>=4){
    $result = 1;  
  }else{
    $result = 0;
  }
  return $result;
}

# write_array_to_file()
# Write an array to a file
# IN:
#   $file = filename
#   $parr = pointer to array to write
# OUT:
#   1 = OK
#   0 = NOK
sub write_array_to_file(){
  my $file = $_[0];
  my $parr = $_[1];
  my $result = 0;
  if (open(FILE, ">", $file)){
    if (print FILE @$parr){
      $result = 1;
    }
    close(FILE);
  } 
}

# write_string_to_file()
# Write a string to a file
# IN:
#   $file = filename
#   $string = string to write
# OUT:
#   1 = OK
#   0 = NOK
sub write_string_to_file(){
  my $file = $_[0];
  my $string = $_[1];
  my $result = 0;
  if (open(FILE, ">", $file)){
    if (print FILE $string){
      $result = 1;
    }
    close(FILE);
  } 
}

sub get_prompt {
	my $equipmenttype = shift;
	
	if ($equipmenttype eq "netscreen") { return PROMPT_REGEX_NETSCREEN; }
	if ($equipmenttype eq "bluecoat") { return PROMPT_REGEX_BLUECOAT; }
	return PROMPT_REGEX_POSIX;
}

#############################################################
# MAIN PROGRAM
#############################################################
#   (command line arguments : -i inputfile -t equipmenttype -c command [-C commanddir] [-m method] [-p port] [-r numretry] [-d displaylevel] [-o outputfile] [-l logfile] [-L sessionlogfile])
  my $inputfile = "";
  my $equipmenttype = "netscreen";
  my $command = "";
  my $commandformatted = "";
  my $commanddir = "";
  my $method = METHOD;
  my $port = "";
  my $numretry = NBTRY;
  my $displaylevel = DISPLAYLEVEL;    
  my $outputfile = "";
  my $outputfileformatted = "";
  my $outputfileappend = OUTPUTFILEAPPEND;
  my $outputfileappendcontext = "";
  my $logfile = "";
  my $sessionlogfile = "";
  my $version = "";
  my $help = ""; 
  my $identity_file = "";
  my $username= "";
  my $zielhost = "";
  my $return_code = 0;
  my $connect_check = 0;
  my $send_command_check = 0;

  #Get command line arguments
	GetOptions("i:s"=>\$identity_file, "z:s"=>\$zielhost, "u:s"=>\$username, "t:s"=>\$equipmenttype, "c:s"=>\$command, 
		"C:s"=>\$commanddir, "m:s"=>\$method, "p:i"=>\$port, "n:i"=>\$numretry, "d:i"=>\$displaylevel, "o:s"=>\$outputfile, "a"=>\$outputfileappend,
		"l:s"=>\$logfile, "v" => \$version, "L:s"=>\$sessionlogfile, "help" => \$help, "h" => \$help);
  my $prompt = &get_prompt($equipmenttype);

  if ($help){ show_help(); exit 0; } #if -h is specified
  if ($version){ show_version(); exit 0; }   #if -v is specified
  #validate command line arguments
  if (!&validate_arguments("dummy_input_file", $equipmenttype, $command, $commanddir, $method, $port, $numretry, $displaylevel, $outputfile, $outputfileappend, $logfile, $sessionlogfile, $version, $help)){
    show_usage();
    exit 1;
  }
  #Fill global variables
  $LOGFILE = $logfile;
  $DISPLAYLEVEL = $displaylevel;
        my @arrEquipment = ($zielhost, $zielhost, $username, 'dummy_password', 'dummy_enable_password');
        if(&validate_equipment($equipmenttype, \@arrEquipment)){
          my $iTry=0;
          my $bConnected=0;
          while($iTry<$numretry && $bConnected==0){
            my $objExpect = Expect->new();
            if ($sessionlogfile ne ""){
              $objExpect->log_file($sessionlogfile);
            }   
            if ($DISPLAYLEVEL>=5){
              $objExpect->log_user(1);
            }
            $bConnected=&exp_connect($objExpect, $equipmenttype, $method, $port, \@arrEquipment, $identity_file);
            if ($bConnected) {
              if ($command ne ""){
                #replace user variables
                $commandformatted = $command;
                $outputfileformatted = $outputfile;
                foreach my $i(0..MAXVAR) {
                  $commandformatted =~ s/%VAR$i%/$arrEquipment[$i]/g;
                  $outputfileformatted =~ s/%VAR$i%/$arrEquipment[$i]/g;
                }
                my $commandresult = "";
                $send_command_check = &exp_send_command($objExpect, $equipmenttype, $arrEquipment[0], $commandformatted, \$commandresult);
                if ($send_command_check){
                  &normalize_commandresult(\$commandresult, $prompt);
                  &process_commandresult(\@arrEquipment, $commandformatted, $commandresult, $outputfileformatted, $outputfileappend);
                  $return_code = 0;
                } else{
                  &log_msg("$arrEquipment[0]: Error when sending command. (command=$commandformatted)");
                  $return_code = 1;
                }              
              }
              if (&exp_disconnect($objExpect, $equipmenttype, $method, \@arrEquipment)) { $return_code = 1; }
            }
            $iTry++;
          }
          $return_code = $return_code || (!$bConnected);
  } else { $return_code = 1; }
  exit ($return_code);
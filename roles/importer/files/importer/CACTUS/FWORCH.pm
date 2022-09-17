package CACTUS::FWORCH;

use strict;
use warnings;
use DBI;
require DBD::Pg;
use IO::File;
use Getopt::Long;
use File::Basename;
use CGI qw(:standard);
require Exporter;
require Sys::Syslog;
use CACTUS::read_config;

our @ISA = qw(Exporter);

our %EXPORT_TAGS = (
    'basic' => [ qw(
        &is_numeric &is_empty &calc_md5_of_files &file_exists &iconv_config_files_2_utf8 &iconv_2_utf8
        &replace_special_chars &remove_quotes &remove_space_at_end &print_syslog
        &remove_literal_carriage_return
        &output_txt &print_txt &print_linebreak &print_txt_with_linebreak &print_header &print_bold &print_error
        &print_html_header &print_html_footer &print_html_only
        &error_handler &error_handler_add &error_handler_get
        $output_method $dbdriver
        $echo_bin $chmod_bin $scp_bin $ssh_bin $scp_batch_mode_switch $ssh_client_screenos
        $fworch_database $fworch_srv_host $fworch_srv_user $fworch_srv_user $fworch_srv_port $fworch_srv_pw $psql_exe $psql_params
        &get_client_filter &get_device_ids_for_mgm
        &eval_boolean_sql &exec_pgsql_file &exec_pgsql_cmd &exec_pgsql_cmd_no_result
        &exec_pgsql_cmd_return_value &exec_pgsql_cmd_return_array_ref &exec_pgsql_cmd_return_table_ref
        &copy_file_to_db &get_rulebase_names &get_ruleset_name_list &get_local_ruleset_name_list &get_global_ruleset_name_list &evaluate_parameters &replace_import_id_in_csv
    ) ]);

our @EXPORT = (@{$EXPORT_TAGS{'basic'}});
our $VERSION = '0.3';

# globale Variablen bzw. Konstanten
our $echo_bin = CACTUS::read_config::read_config('echo_bin');
our $chmod_bin = CACTUS::read_config::read_config('chmod_bin');
our $scp_bin = CACTUS::read_config::read_config('scp_bin');
our $ssh_bin = CACTUS::read_config::read_config('ssh_bin');
our $ssh_client_screenos = CACTUS::read_config::read_config('ssh_client_screenos');
our $scp_batch_mode_switch = CACTUS::read_config::read_config('scp_batch_mode_switch');
our $output_method = CACTUS::read_config::read_config('output_method');
our $syslog_type = CACTUS::read_config::read_config('syslog_type');
our $syslog_ident = CACTUS::read_config::read_config('syslog_ident');
our $syslog_facility = CACTUS::read_config::read_config('syslog_facility');

our $webuser;
our $fworch_srv_pw = '';
our $fworch_srv_host = &CACTUS::read_config::read_config("fworch database hostname");
our $fworch_database = &CACTUS::read_config::read_config("fworch database name");
our $fworch_srv_port = &CACTUS::read_config::read_config("fworch database port");
our $csv_delimiter = &CACTUS::read_config::read_config("csv_delimiter");
our $group_delimiter = &CACTUS::read_config::read_config("group_delimiter");
our $csv_user_delimiter = &CACTUS::read_config::read_config("csv_user_delimiter");
our $fworch_srv_user = &CACTUS::read_config::read_config("fworch_srv_user");
our $psql_exe = &CACTUS::read_config::read_config("psql_exe");
our $psql_params = &CACTUS::read_config::read_config("psql_params");
our $dbdriver = "Pg";
our $ssh_id_basename = 'import_user_secret';

############################################################
# getnum
# hilfsfunktion fuer is_numeric
############################################################
sub getnum {
    use POSIX qw(strtod);
    my $str = shift;
    if (!defined($str)) {return undef;}
    #	$str =~ s/^\s+//;
    #	$str =~ s/\s+$//;
    $str =~ s/\./\,/g;
    $! = 0;
    my ($num, $unparsed) = strtod($str);
    if (($str eq '') || ($unparsed != 0) || $!) {
        return undef;
    }
    else {
        return $num;
    }
}

############################################################
# is_numeric(scalar)
# liefert true wenn scalar ein numerischer Wert ist
############################################################
sub is_numeric {defined getnum($_[0])}

############################################################
# is_empty(scalar)
# liefert true wenn scalar leer ist (nicht definiert oder leerer String)
############################################################
sub is_empty {return (!defined($_[0]) || $_[0] eq '');}

##############################################################################################
# package output;

############################################################
# print_syslog(text, prio)
# Ausgabe des Textes an syslog mit prio
############################################################
sub print_syslog {
    my $txt = shift;
    my $syslog_priority = shift;

    if ($^O eq 'linux') {

        # syslog does not work properly for windows
        &Sys::Syslog::setlogsock($syslog_type) or die $!;
        &Sys::Syslog::openlog($syslog_ident, 'cons', $syslog_facility)
            or die $!;
        &Sys::Syslog::syslog($syslog_priority, $txt)
            or die "syslog failed $!";
        &Sys::Syslog::closelog();
    }
    return;
}

############################################################
# print_txt(text)
# Ausgabe des Textes
############################################################
sub print_txt {
    my $txt = shift;
    print($txt);
    return;
}

sub output_txt {
    my $txt = shift;
    my $errlvl = shift;

    print($txt);
    if (defined($errlvl) && $errlvl > 0) {
        print_syslog($txt, 'err');

    }
    else {
        print_syslog($txt, 'notice');
    }
    return;
}

############################################################
# print_linebreak
# Ausgabe eines linebreaks
############################################################
sub print_linebreak {
    our $output_method;
    if ($output_method eq 'html') {print_txt("<br>\n");}
    else {print_txt("\n");}
    return;
}

############################################################
# print_txt_with_linebreak(text)
# Ausgabe des Textes mit anschliessendem linebreak
############################################################
sub print_txt_with_linebreak {
    my $txt = shift;
    print_txt($txt);
    print_linebreak();
    return;
}

############################################################
# print_header(text)
# Ausgabe des Textes, bei html als <H3>
############################################################
sub print_header {
    my $txt = shift;
    our $output_method;
    print_syslog($txt, 'info');
    if ($output_method eq 'html') {print_txt("<h3>$txt</h3>");}
    else {print_txt($txt);}
    return;
}

############################################################
# print_bold(text)
# Ausgabe des Textes in fett, wenn html
############################################################
sub print_bold {
    my $txt = shift;
    our $output_method;
    print_syslog($txt, 'notice');
    if ($output_method eq 'html') {print_txt("<B>$txt</B>");}
    else {print_txt($txt);}
    return;
}

############################################################
# print_error(text)
# Ausgabe des Textes in rot (wenn HTML)
############################################################
sub print_error {
    my $txt = shift;
    our $output_method;
    print_syslog($txt, 'warning');
    if ($output_method eq 'html') {
        print_txt("<FONT color=\"#CD3326\">");
        print_bold($txt);
        print_txt("</FONT>");
    }
    else {print_txt($txt);}
    return;
}

############################################################
# print_html_only($html_txt)
# Ausgabe von HTML-Code, falls html
############################################################
sub print_html_only {
    my $htmlcode = shift;
    our $output_method;
    if ($output_method eq 'html') {print_txt("$htmlcode");}
    return;
}

############################################################
# print_html_header()
# Ausgabe eines HTML-Headers, falls html
############################################################
sub print_html_header {
    our $output_method;
    if ($output_method eq 'html') {
        print_txt("Content-Type: text/html\n\n");
        print_txt("<html><head>");
        print_txt(
            "<script type=\"text/javascript\" src=\"/js/client.js\"></script>");
        print_txt(
            "<script type=\"text/javascript\" src=\"/js/script.js\"></script>");
        print_txt(
            "<link rel=\"stylesheet\" type=\"text/css\" href=\"/css/firewall.css\">"
        );
        print_txt("<script language=\"javascript\" type=\"text/javascript\">");
        print_txt(
            "if(is_ie) document.write(\"<link rel='stylesheet' type='text/css' href='/css/firewall_ie.css'>\");"
        );
        print_txt(
            "</script></head><body class=\"iframe\" onLoad=\"JavaScript:parent.document.getElementById('leer').style.visibility='hidden';window.scrollTo(0,1000000);\">"
        );
    }
    return;
}

############################################################
# print_html_footer()
# Ausgabe der schliessenden TAGS
############################################################
sub print_html_footer {
    our $output_method;
    if ($output_method eq 'html') {
        print_txt("</body></html>");
    }
    return;
}

############################################################
# iconv_config_files_2_utf8 ($file,$tmpdir)
# convert latin1 to utf-8
############################################################
sub iconv_config_files_2_utf8 {
    my $str_of_files_to_sum = shift;
    my $tmpdir = shift;
    my $file;
    my @file_ar;

    if (@file_ar) {
    @file_ar = split(/,/, $str_of_files_to_sum);
        foreach $file (@file_ar) {
            if ($file !~ /^fwauth\.NDB/) {
                iconv_2_utf8($file, $tmpdir);
            }
        }
    }
    return;
}

############################################################
# iconv_2_utf8 ($file)
# convert latin1 to utf-8
############################################################
sub iconv_2_utf8 {
    my $file_to_conv = shift;
    my $tmpdir = shift;
    my $md5sum_file = "$tmpdir/cfg_hash.md5";
    my $cmd = "/usr/bin/iconv --from-code latin1 --to-code utf-8 $file_to_conv --output $file_to_conv.utf-8";
    system($cmd);
    $cmd = "/bin/mv $file_to_conv.utf-8 $file_to_conv";
    system($cmd);
}


############################################################
# calc_md5_of_files ($file,$tmpdir)
# gibt den kontaktenierten MD5-Hash-Wert aller Files zurueck
############################################################
sub calc_md5_of_files {
    my $str_of_files_to_sum = shift;
    my $tmpdir = shift;
    my $file;
    my $total = '';
    my @file_ar;

    @file_ar = split(/,/, $str_of_files_to_sum);
    foreach $file (@file_ar) {
        $total .= calc_md5_of_file($file, $tmpdir);
    }
    return $total;
}

############################################################
# calc_md5_of_file ($file)
# gibt den MD5-Hash-Wert zurueck
############################################################
sub calc_md5_of_file {
    my $file_to_sum = shift;
    my $tmpdir = shift;
    my $md5sum_file = "$tmpdir/cfg_hash.md5";
    my $cmd = "/usr/bin/md5sum $file_to_sum | cut --bytes=-32 >$md5sum_file";
    system($cmd);
    open(MD5SUM, "<$md5sum_file");
    my $md5sum = <MD5SUM>;
    close MD5SUM;
    $cmd = "/bin/rm -f $md5sum_file";
    system($cmd);

    if (defined($md5sum)) {
        return substr($md5sum, 0, 32);
    }
    else {
        return "error-in-md5-sum-calc";
    }
}

############################################################
# file_exists ($file)
# true, wenn das file $file existiert
############################################################
sub file_exists {
    return (-e $_[0]);
}

# package error_handling;
############################################################
# error_handler(string)
# fehlerbehandlung
# in der einfachsten Fassung Ausgabe des Fehlerstrings
# und exit 1
############################################################
sub error_handler {
    my $err_str = $_[0];

    #    print ("Fehler: $err_str\n");
    return $err_str;

    #    exit 1;
}

############################################################
# error_handler_get
# fehlermeldungen auslesen
############################################################
sub error_handler_get {
    my $current_import_id = shift;

    if (defined($current_import_id)) {
        my $existing_error_str = &exec_pgsql_cmd_return_value("SELECT import_errors FROM import_control WHERE control_id=$current_import_id");
        return $existing_error_str;
    }
    else {
        return undef;
    }
}

############################################################
# error_handler_update
# fehlermeldungen anhaengen in import_control
############################################################
sub error_handler_update {
    my $current_import_id = shift;
    my $new_error_str = shift;

    if (defined($current_import_id)) {
        my $existing_error_str = &error_handler_get($current_import_id);
        if (!defined($existing_error_str) || $existing_error_str eq '') {$existing_error_str = '';}
        else {$existing_error_str .= "; ";}
        &exec_pgsql_cmd_no_result("UPDATE import_control SET import_errors='$existing_error_str$new_error_str', successful_import=FALSE WHERE control_id=$current_import_id");
    }
}

############################################################
# error_handler_add
# fehlermeldungen an string anhaengen, wenn definiert
############################################################
sub error_handler_add {
    my $current_import_id = shift;
    my $error_level = shift;
    my $current_error_str = shift;
    my $current_error_count = shift;
    my $previous_errors_count = shift;
    my $error_tag = 'DEBUG';

    my $previous_errors_string = &error_handler_get($current_import_id);
    #	print ("debug error handler: lvl=$error_level, curr_err_str=$current_error_str, glob_err_str=$previous_errors_string, #curr_errors=$current_error_count, #total_errs=$previous_errors_count\n");
    my $total_error_str = (defined($previous_errors_string) ? "$previous_errors_string" : '');
    my $total_error_count = $previous_errors_count;
    if ($error_level) {
        if ($error_level == 1) {$error_tag = 'INFO';}
        if ($error_level == 2) {$error_tag = 'WARN';}
        if ($error_level == 3) {$error_tag = 'ERR';}
        if ($error_level > 3) {$error_tag = 'FATAL-ERR';}
        if (!defined($previous_errors_string)) {$previous_errors_string = '';}
        if ($previous_errors_string ne '') {$previous_errors_string .= "\n";}
        if ($current_error_count) {if ($current_error_str ne '') {$current_error_str = "$error_tag-$current_error_str";}}
        if ($error_level > 1 && $current_error_count) {$total_error_str = "$previous_errors_string$current_error_str";}
        #		print ("current_error_count: $current_error_count\n");
        $total_error_count = $previous_errors_count + $current_error_count;
        if ($error_level > 1 && $current_error_count) {
            output_txt("$current_error_str\n", $error_level);
            &error_handler_update($current_import_id, $current_error_str);
        }
        if ($error_level > 3 && $current_error_count) {exit $error_level;}
    }
    return $total_error_count;
}

# package string_manipulation;
############################################################
# remove_quotes(string)
# entfernt aus string alle Anfuehrungszeichen
############################################################
sub remove_quotes {
    my $str = $_[0];
    $str =~ s/"//g;
    $str =~ s/'//g;
    $str =~ s/`//g;
    $str =~ s/´//g;
    return $str;
}

############################################################
# remove_space_at_end(string)
# entfernt aus string das letzte Zeichen, wenn es ein space ist
############################################################
sub remove_space_at_end {
    my $str = $_[0];
    $str =~ s/\s$//g;
    return $str;
}

############################################################
# remove_literal_carriage_return(filename)
# entfernt jedes Auftreten eines literal_carriage_returns inenrhalb des Files
############################################################
sub remove_literal_carriage_return {
    my $in_file = $_[0];
    my $out_file = "$in_file.no_cr";
    my $line;

    open(IN, $in_file) || die "$in_file konnte nicht geoeffnet werden.\n";
    open(OUT, ">$out_file")
        || die "$out_file konnte nicht geoeffnet werden.\n";

    while (<IN>) {
        $line = $_;            # Zeileninhalt merken
        $line =~ s/\r\n/\n/;   # dos2unix
        $line =~ s/\x0D/\\r/g; # literal carriage return entfernen
        print OUT $line;
    }
    close(IN);
    close(OUT);
    rename($in_file, "$in_file.orig");
    rename($out_file, $in_file);
    return;
}

############################################################
# replace_special_chars_in_string(string)
# ersetzt in string alle Sonderzeichen durch Standard ASCII-Zeichen
# hier fehlt noch ein Mechanismus, der gefundene Ersetzungen protokolliert
############################################################
sub replace_special_chars_in_str {
    my $str = $_[0];
    $str =~ s/ä/ae/g;
    $str =~ s/ö/oe/g;
    $str =~ s/ü/ue/g;
    $str =~ s/Ä/Ae/g;
    $str =~ s/Ö/Oe/g;
    $str =~ s/Ü/Ue/g;
    $str =~ s/ß/ss/g;
    $str =~ s/é/e/g;
    $str =~ s/è/e/g;
    $str =~ s/á/a/g;
    $str =~ s/à/a/g;
    $str =~ s/í/i/g;
    $str =~ s/ì/i/g;
    return $str;
}

############################################################
# replace_special_chars(file_name)
# ersetzt in file_name alle Sonderzeichen durch ASCII-Zeichen
############################################################
sub replace_special_chars {
    my ($orig_line, $line, $input, $file, $output, @text);
    @text = qw();

    $input = new IO::File("< $_[0]")
        or die "Cannot open file $_[0] for reading: $!";

    while (<$input>) {
        $line = $_;
        $orig_line = $line;
        $line = replace_special_chars_in_str($line);
        push @text, ($line);

        #    print $output $line;
        if ($orig_line ne $line) {print "changed $orig_line to $line\n";}
    }
    $input->close;
    $output = new IO::File("> $_[0]")
        or die "Cannot open file $_[0] for writing: $!";
    foreach $line (@text) {
        print $output $line;
    }
    $output->close;
}


############################################################
# replace_import_id_in_csv
# 
############################################################
sub replace_import_id_in_csv {
    my $csv_file = shift;
    my $import_id = shift;
    my $result = 0; 
    my $line;

    # s/^\"(\d+)\"/\"$import_id\"/g' $csv_file;
    my $CSVFILE = new IO::File("< $csv_file");
    my $NEWCSVFILE = new IO::File("> $csv_file.tmp");
    if ($CSVFILE && $NEWCSVFILE)
    {
        while (<$CSVFILE>) {
            $line = $_;
            $line =~ s/^\"(\d+)\"/\"$import_id\"/;
            #print ("line=$line");
            print $NEWCSVFILE $line;
        }
        $CSVFILE->close;
        $NEWCSVFILE->close;
        system("mv '$csv_file.tmp' '$csv_file'");
    } 
    else 
    {
        output_txt("Cannot open file $csv_file for reading: $!", 3);
    }
    return (!$result);
}

############################################################
# copy_file_to_db
# 
############################################################
sub copy_file_to_db {
    my $sqlcode = shift;
    my $csv_file = shift;
    my ($dbh, $sth);
    my $result = 0;

    my $CSVFILE = new IO::File("< $csv_file");
    if ($CSVFILE) {
        $dbh = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
        if (!defined $dbh) {
            output_txt("Cannot connect to database!\n", 3);
        }
        else {
            $dbh->do($sqlcode);
            while (<$CSVFILE>) {
                $dbh->pg_putline($_);
            }
            $result = $dbh->pg_endcopy;
            $dbh->disconnect;
            $CSVFILE->close;
        }
    }
    else {
        output_txt("Cannot open file $csv_file for reading: $!", 3);
    }
    return (!$result);
}

############################################################
# eval_boolean_sql(sql-befehl)
# wertet das Ergebnis des sql-befehls aus (true oder false
############################################################
sub eval_boolean_sql {
    my ($sqlcode);
    my ($rc, $dbh, $sth);
    my (@result);
    our ($fworch_srv_host, $fworch_database, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw, $dbdriver);

    $sqlcode = $_[0];
    $dbh = DBI->connect("dbi:$dbdriver:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
    if (!defined $dbh) {die "Cannot connect to database!\n";}
    $sth = $dbh->prepare($sqlcode);
    if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
    $sth->execute;
    @result = $sth->fetchrow_array;
    $sth->finish;
    $dbh->disconnect;
    return $result[0];
}

############################################################
# exec_pgsql_cmd_return_value(cmd)
# fuehrt den SQL-Befehl cmd aus und gibt das Resultat des SQL-Befehls zurueck
############################################################
sub exec_pgsql_cmd_return_value {
    return eval_boolean_sql($_[0]);
}

############################################################
# exec_pgsql_cmd_return_array_ref(cmd)
# fuehrt den SQL-Befehl cmd aus und gibt das Resultat des SQL-Befehls 
# als Verweis auf ein Array zurueck
############################################################
sub exec_pgsql_cmd_return_array_ref {
    my ($res, $err_str, $err_flag, $sqlcode, $result);
    # $result: references the result array
    my ($rc, $dbh, $sth);
    our ($fworch_srv_host, $fworch_database, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw, $dbdriver);

    $sqlcode = $_[0];
    $err_flag = 0;
    $dbh = DBI->connect("dbi:$dbdriver:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
    if (!defined $dbh) {die "Cannot connect to database!\n";}
    if ($sqlcode !~ /^$/) {
        #    print "$sqlcode\n";
        $sth = $dbh->prepare($sqlcode);
        if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
        $res = $sth->execute;
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {
            $err_flag = 1;
        }
    }
    $result = $sth->fetchrow_arrayref;
    if (!$err_flag) {
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {
            $err_flag = 1;
        }
    }
    $sth->finish;
    $dbh->disconnect;
    #  print("locally: result[0][0]=$result->[0][0]");
    return $result;
}

############################################################
# exec_pgsql_cmd_return_table_ref(cmd)
# fuehrt den SQL-Befehl cmd aus und gibt das Resultat des SQL-Befehls 
# als Verweis auf eine Tabelle (2-dim-Array) zurueck
############################################################
sub exec_pgsql_cmd_return_table_ref {
    my ($res, $err_str, $err_flag, $sqlcode, $result);
    # $result: references the result array
    my ($rc, $dbh, $sth);
    our ($fworch_srv_host, $fworch_database, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw);

    $sqlcode = $_[0];
    my $keyfield = $_[1];
    $err_flag = 0;
    $dbh = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
    if (!defined $dbh) {die "Cannot connect to database!\n";}
    if ($sqlcode !~ /^$/) {
        #    print "$sqlcode\n";
        $sth = $dbh->prepare($sqlcode);
        if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
        $res = $sth->execute;
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {
            $err_flag = 1;
        }
    }
    #  $result = $sth->fetchrow_arrayref;
    #  $result = $sth->fetchall_arrayref;
    $result = $sth->fetchall_hashref($keyfield);
    if (!$err_flag) {
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {
            $err_flag = 1;
        }
    }
    $sth->finish;
    $dbh->disconnect;
    #  print("locally: result[0][0]=$result->[0][0]");
    return $result;
}

############################################################
# exec_pgsql_cmd(cmd)
# fuehrt den SQL-Befehl cmd aus und gibt error_code zurueck
# das Resultat des SQL-Befehls wird ueber $_[1] gemeldet
############################################################
sub exec_pgsql_cmd {
    my ($res, $err_str, $err_flag, $sqlcode, @result);
    my ($rc, $dbh, $sth);
    our ($fworch_srv_host, $fworch_database, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw);

    $sqlcode = $_[0];
    $err_flag = 0;
    $dbh = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
    if (!defined $dbh) {die "Cannot connect to database!\n";}
    if ($sqlcode !~ /^$/) {
        #    print "$sqlcode\n";
        $sth = $dbh->prepare($sqlcode);
        if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
        $res = $sth->execute;
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {
            $err_flag = 1;
        }
    }
    @result = $sth->fetchrow_array;
    if (!$err_flag) {
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {
            $err_flag = 1;
        }
    }
    $sth->finish;
    $dbh->disconnect;
    #  return $result[0];
    if (defined($_[1])) {
        $_[1] = $result[0];
        if (!defined($result[0])) {
            return error_handler('EMPTY_SQL_RESULT');
        }
    }
    if (!$err_flag) {return 0;}
    else {return $err_str;}
}

############################################################
# exec_pgsql_cmd_no_result(cmd)
# fuehrt den SQL-Befehl cmd aus und gibt kein Resultat zurueck
############################################################
sub exec_pgsql_cmd_no_result {
    my ($res, $err_str, $sqlcode, $err_flag);
    my ($dbh, $sth);
    our ($fworch_srv_host, $fworch_database, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw);

    $err_flag = 0;
    $sqlcode = $_[0];
    $dbh = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
    if (!defined $dbh) {die "Cannot connect to database!\n";}
    if ($sqlcode !~ /^$/) {
        #    print "$sqlcode\n";
        $sth = $dbh->prepare($sqlcode);
        if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
        $res = $sth->execute;
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {
            $err_flag = 1;
        }
    }
    $sth->finish;
    $dbh->disconnect;
    if (!$err_flag) {return 0;}
    else {return $err_str;}
}

############################################################
# exec_pgsql_file(file_name)
# fuehrt die SQL-Befehle in file_name aus
############################################################
sub exec_pgsql_file {
    my ($line, $input, $file, $sqlcode, $res, $err_str, $err_flag);
    my ($dbh, $sth);
    our ($fworch_srv_host, $fworch_database, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw);

    $err_flag = 0;
    $input = new IO::File("< $_[0]") or die "Cannot open file $_[0] for reading: $!";
    $dbh = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
    if (!defined $dbh) {die "Cannot connect to database!\n";}
    while (<$input>) {
        $sqlcode = $_;
        if ($sqlcode !~ /^$/) {
            #    print "$sqlcode\n";
            $sth = $dbh->prepare($sqlcode);
            if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
            $res = $sth->execute;
            $err_str = $sth->errstr;
            if (defined($err_str) && length($err_str) > 0) {
                $err_flag = 1;
                last;
            }
        }
    }
    $input->close;
    $sth->finish;
    $dbh->disconnect;
    if (!$err_flag) {return 0;}
    else {return $err_str;}
}

############################################################
############################################################

sub get_device_ids_for_mgm {
    my $mgm_id = $_[0];
    my $aref = exec_pgsql_cmd_return_table_ref("SELECT dev_id FROM device WHERE mgm_id=$mgm_id", 'dev_id');
    #	print("aref0: $aref->[0], aref1: $aref->[1]");
    return $aref;
}

############################################################
# get_client_filter(client_id)
# zu einem Tenant einen Filter generieren
# Ergebnis ist ein String: zB. "ip<<'1.0.0.0/8' AND ip<<'2.0.0.0/16'"
# Sonderfall: wenn client_id=0 (nicht existent): RETURN "TRUE" --> kein Filter
# parameter1: client_id
############################################################
sub get_client_filter {
    my $client_id = $_[0];
    my ($client_net_ip, $filter, $dbh, $sth, $relevant_import_id, $err_str, $sqlcode);
    our ($fworch_database, $fworch_srv_host, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw);

    $filter = "TRUE";
    if (defined($client_id) && $client_id ne '' && $client_id != 0) {
        $sqlcode = "SELECT client_net_ip FROM client_network WHERE client_id=$client_id";
        $dbh = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
        if (!defined $dbh) {die "Cannot connect to database!\n";}
        $sth = $dbh->prepare($sqlcode);
        if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
        $sth->execute;
        $err_str = $sth->errstr;
        if (defined($err_str) && length($err_str) > 0) {error_handler($err_str);}
        $filter = "(";
        while (($client_net_ip) = $sth->fetchrow()) {
            $filter .= " obj_ip<<='$client_net_ip' OR '$client_net_ip'<<=obj_ip OR";
            # sollte eigentlich ein noch zu definierenden Operator sein: ip1 # ip2 ip1 und ip2 haben eine nicht leere Schnittmenge
        }
        $filter .= " FALSE)";
        $sth->finish;
        $dbh->disconnect;
    }
    return $filter;
}

sub get_rulebase_names {
    # getting device-info for all devices of the current mgmt
    my $mgm_id = shift;
    my $dbdriver = shift;
    my $fworch_database = shift;
    my $fworch_srv_host = shift;
    my $fworch_srv_port = shift;
    my $fworch_srv_user = shift;
    my $fworch_srv_pw = shift;

    my $dbh = DBI->connect("dbi:$dbdriver:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port", "$fworch_srv_user", "$fworch_srv_pw");
    if (!defined $dbh) {die "Cannot connect to database!\n";}
#    my $sth = $dbh->prepare("SELECT dev_id,dev_name,local_rulebase_name,global_rulebase_name FROM device WHERE mgm_id=$mgm_id AND NOT do_not_import");
    my $sth = $dbh->prepare("SELECT dev_id,dev_name,local_rulebase_name FROM device WHERE mgm_id=$mgm_id AND NOT do_not_import");
    if (!defined $sth) {die "Cannot prepare statement: $DBI::errstr\n";}
    $sth->execute;
    my $rulebases = $sth->fetchall_hashref('dev_id');
    $sth->finish;
    $dbh->disconnect;
    return $rulebases;
}

#  convert hash to comma separated string
# sub get_ruleset_name_list {
# 	my $href_rulesetname = shift;
# 	my $result = '';
	
# 	while ( (my $key, my $value) = each %{$href_rulesetname}) {
#         $result .= $value->{'dev_rulebase'} . ',';
#     }
#     if ($result =~ /^(.+?)\,$/) {   # stripping off last comma
#     	return $1;
#     }
#     return $result;
# }

#  convert hash to comma separated string
sub get_local_ruleset_name_list {
	my $href_rulesetname = shift;
	my $result = '';
	
	while ( (my $key, my $value) = each %{$href_rulesetname}) {
        $result .= $value->{'local_rulebase_name'} . ',';
    }
    if ($result =~ /^(.+?)\,$/) {   # stripping off last comma
    	return $1;
    }
    return $result;
}

#  convert hash to comma separated string
sub get_global_ruleset_name_list {
	my $href_rulesetname = shift;
	my $result = '';
	
    if (defined($href_rulesetname)) {
        while ( (my $key, my $value) = each %{$href_rulesetname}) {
            if (defined($value->{'global_rulebase_name'})) {
                $result .= $value->{'global_rulebase_name'} . ',';
            } else {
                $result .= ',';
            }
        }
        if ($result =~ /^(.+?)\,$/) {   # stripping off last comma
            return $1;
        }
        return $result;
    } else {
        return "";
    }
}

sub evaluate_parameters {
    my $mgm_id = shift;
    my $mgm_name = shift;

    if (!defined($mgm_id) || $mgm_id eq '') {
        if (defined($mgm_name) && $mgm_name ne '') {
            $mgm_id = exec_pgsql_cmd_return_value("select mgm_id from management where mgm_name='$mgm_name'");
        }
        else {&error_handler_add(undef, my $error_level = 5, "fworch-importer-single.pl: missing argument (mgm_id || mgm_name)", 1, 0);} # no valid input given
    }
    if (!defined($mgm_id) || $mgm_id eq '') {
        &error_handler_add(undef, my $error_level = 5, 'Management ' . (($mgm_name ne '') ? $mgm_name . ' ' : '') . 'not found', 1, 0);
    }
    return ($mgm_id, $mgm_name);
}

#####################################################################################

1;
__END__

=head1 NAME

FWORCH - Perl extension for fworch

=head1 SYNOPSIS

  use FWORCH;
  the function read_basic_fworch_data() must be called first to
  generate the hashes before using the DB-lookup functions

=head1 DESCRIPTION

fworch Perl Module
support for
- importing configs into fworch Database
- basic functions to access fworch DB

=head2 EXPORT

  global variables
    %managementID
    %object_typID
    %IP_protocolID

  DB functions
    read_basic_fworch_data()
    these functions perform the lookups into the above hashes:
      get_mgmtID(management-name)
      get_object_typID(objekt-name)
      get_ip_protoID(IP-protocol-name)

  Basic functions
    is_numeric(scalar)

=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Tim Purschke, tmp@cactus.de

=cut

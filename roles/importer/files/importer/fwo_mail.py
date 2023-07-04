import json
import jsonpickle
from fwo_data_networking import InterfaceSerializable, RouteSerializable
import fwo_globals
from fwo_const import max_objs_per_chunk, csv_delimiter, apostrophe, line_delimiter
from fwo_log import getFwoLogger, getFwoAlertLogger
from copy import deepcopy
import smtplib
from email.message import EmailMessage


def send_mail(recipient_list, subject, body, fwo_config):
    # Create a text/plain message
    msg = EmailMessage()
    msg.set_content(body)
    msg['Subject'] = subject
    msg['From'] = fwo_config['emailSenderAddress']
    msg['To'] = recipient_list

    try:
        smtp_server = smtplib.SMTP(fwo_config['emailServerAddress'], fwo_config['emailPort'])
        smtp_server.ehlo() #setting the ESMTP protocol
        if 'emailTls' in fwo_config and fwo_config['emailTls']=='StartTls':
            smtp_server.starttls() #setting up to TLS connection
            smtp_server.ehlo() #calling the ehlo() again as encryption happens on calling startttls()
        if 'emailUser' in fwo_config and 'emailPassword' in fwo_config and fwo_config['emailUser']!="":
            smtp_server.login(fwo_config['emailUser'], fwo_config['emailPassword']) #logging into out email id

        #sending the mail by specifying the from and to address and the message
        smtp_server.send_message(msg)
        smtp_server.quit() #terminating the server
    except Exception as e:
        logger = getFwoLogger()
        logger.error("error while sending import change notification email: " + str(e))


def send_change_notification_mail(fwo_config, number_of_changes, mgm_name, mgm_id):
    if 'impChangeNotifyActive' in fwo_config and bool(fwo_config['impChangeNotifyActive']) and 'impChangeNotifyRecipients' in fwo_config:
        body = ""
        if 'impChangeNotifyBody' in fwo_config:
            body += fwo_config['impChangeNotifyBody'] + ": "
        body += str(number_of_changes) + ", Management: " + mgm_name + " (id=" + mgm_id + ")"
        send_mail(
            fwo_config['impChangeNotifyRecipients'].split(','),
            fwo_config['impChangeNotifySubject'] if 'impChangeNotifySubject' in fwo_config else "firewall orchestrator change notification",
            body,
            fwo_config
        )

using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Middleware.Client;
using FWO.Mail;


namespace FWO.Ui.Services
{
    public class EmailHelper
    {
        private readonly ApiConnection apiConnection;
        private readonly MiddlewareClient middlewareClient;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> displayMessageInUi;
        private List<UserGroup> ownerGroups = new ();
        private List<UiUser> uiUsers = new ();


        public EmailHelper(ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            this.apiConnection = apiConnection;
            this.middlewareClient = middlewareClient;
            this.userConfig = userConfig;
            this.displayMessageInUi = displayMessageInUi;
        }

        public async Task Init()
        {
            ownerGroups = await GroupAccess.GetGroupsFromInternalLdap(middlewareClient, userConfig, displayMessageInUi, true);
            uiUsers = await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUsers);
        }

        public async Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body)
        {
            EmailConnection emailConnection = new EmailConnection(userConfig.EmailServerAddress, userConfig.EmailPort,
                userConfig.EmailTls, userConfig.EmailUser, userConfig.EmailPassword, userConfig.EmailSenderAddress);
            MailKitMailer mailer = new (emailConnection);
            return await mailer.SendAsync(new MailData(CollectEmailAddresses(owner), subject, body), emailConnection, new CancellationToken(), true);
        }

        private List<string> CollectEmailAddresses(FwoOwner owner)
        {
            List<string> tos = new ();
            UserGroup? ownerGroup = ownerGroups.FirstOrDefault(x => x.Dn == owner.GroupDn);
            if(ownerGroup != null)
            {
                foreach(var user in ownerGroup.Users)
                {
                    UiUser? uiuser = uiUsers.FirstOrDefault(x => x.Dn == user.Dn);
                    if(uiuser != null && uiuser.Email != null && uiuser.Email != "")
                    {
                        tos.Add(uiuser.Email);
                    }
                }
            }
            return tos;
        }
    }
}
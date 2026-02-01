using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;

namespace FWO.Services
{
    public partial class ModellingConnectionHandler
    {
        public void CreateServiceGroup()
        {
            DisplaySvcGrpMode = false;
            AddSvcGrpMode = true;
            HandleServiceGroup(new ModellingServiceGroup() { });
        }

        public void EditServiceGroup(ModellingServiceGroup? serviceGroup)
        {
            if (serviceGroup != null)
            {
                DisplaySvcGrpMode = false;
                AddSvcGrpMode = false;
                HandleServiceGroup(serviceGroup);
            }
        }

        public void DisplayServiceGroup(ModellingServiceGroup? serviceGroup)
        {
            if (serviceGroup != null)
            {
                DisplaySvcGrpMode = true;
                AddSvcGrpMode = false;
                HandleServiceGroup(serviceGroup);
            }
        }

        private void HandleServiceGroup(ModellingServiceGroup serviceGroup)
        {
            try
            {
                SvcGrpHandler = new ModellingServiceGroupHandler(apiConnection, userConfig, Application, AvailableServiceGroups,
                    serviceGroup, AvailableServices, AvailableSvcElems, AddSvcGrpMode, DisplayMessageInUi, ReInit, IsOwner, DisplaySvcGrpMode);
                EditSvcGrpMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service_group"), "", true);
            }
        }

        public async Task RequestDeleteServiceGrp(ModellingServiceGroup? serviceGroup)
        {
            if (serviceGroup != null)
            {
                actServiceGroup = serviceGroup;
                DeleteAllowed = !await CheckServiceGroupIsInUse();
                Message = DeleteAllowed ? userConfig.GetText("U9004") + serviceGroup.Name + "?" : userConfig.GetText("E9008") + serviceGroup.Name;
                DeleteSvcGrpMode = true;
            }
        }

        private async Task<bool> CheckServiceGroupIsInUse()
        {
            try
            {
                if (SvcGrpToAdd.FirstOrDefault(s => s.Id == actServiceGroup.Id) == null)
                {
                    List<ModellingConnection> foundConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionIdsForServiceGroup, new { serviceGroupId = actServiceGroup.Id });
                    if (foundConnections.Count == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("is_in_use"), "", true);
                return true;
            }
        }

        public async Task DeleteServiceGroup()
        {
            try
            {
                if ((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteServiceGroup, new { id = actServiceGroup.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.ServiceGroup, actServiceGroup.Id,
                        $"Deleted Service Group: {actServiceGroup.Display()}", Application.Id);
                    AvailableServiceGroups.Remove(actServiceGroup);
                    AvailableSvcElems.Remove(AvailableSvcElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ModObjectType.ServiceGroup && x.Value == actServiceGroup.Id));
                    DeleteSvcGrpMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service_group"), "", true);
            }
        }

        public void ServiceGrpsToConn(List<ModellingServiceGroup> serviceGrps)
        {
            foreach (var grp in serviceGrps)
            {
                if ((ActConn.ServiceGroups.FirstOrDefault(w => w.Content.Id == grp.Id) == null) && (SvcGrpToAdd.FirstOrDefault(g => g.Id == grp.Id) == null))
                {
                    SvcGrpToAdd.Add(grp);
                }
            }
        }


        public void CreateService()
        {
            AddServiceMode = true;
            HandleService(new ModellingService() { });
        }

        public void EditService(ModellingService? service)
        {
            if (service != null)
            {
                AddServiceMode = false;
                HandleService(service);
            }
        }

        private void HandleService(ModellingService service)
        {
            try
            {
                ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service,
                    AvailableServices, AvailableSvcElems, AddServiceMode, DisplayMessageInUi, IsOwner);
                EditServiceMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }

        public async Task RequestDeleteService(ModellingService? service)
        {
            if (service != null)
            {
                await RequestDeleteServiceBase(service);
                DeleteServiceMode = true;
            }
        }

        public async Task DeleteService()
        {
            try
            {
                DeleteServiceMode = await DeleteService(AvailableServices, AvailableSvcElems);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service"), "", true);
            }
        }

        public void ServicesToConn(List<ModellingService> services)
        {
            foreach (var svc in services)
            {
                if ((ActConn.Services.FirstOrDefault(w => w.Content.Id == svc.Id) == null) && (SvcToAdd.FirstOrDefault(s => s.Id == svc.Id) == null))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }
    }
}

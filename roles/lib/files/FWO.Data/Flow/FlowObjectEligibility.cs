namespace FWO.Data.Flow
{
    /// <summary>
    /// Decides whether a Flow catalog entry may be offered in the request module and attached to a
    /// workflow element.
    /// The same predicate is expressed as a Hasura select filter on the flow catalog tables and as a
    /// Hasura insert/update check on request.reqelement, so that an entry which is hidden, retired or
    /// reserved for internal use cannot be reached by a direct API call that bypasses the UI either.
    /// Two strengths are distinguished: <see cref="IsLive"/> asks whether an entry is still usable at
    /// all, <see cref="IsRequestable(FlowSvcObject)"/> additionally rejects the internal representations
    /// that only the platform itself may reference.
    /// </summary>
    public static class FlowObjectEligibility
    {
        /// <summary>
        /// States a catalog entry may still be used in. A denied or removed entry is left alone so that a
        /// new request does not silently inherit an earlier rejection or a retired definition.
        /// </summary>
        private static readonly List<string> kLiveStates = [FlowState.Requested, FlowState.Implemented];

        /// <summary>
        /// Whether the protocol id is an internal representation rather than a real IP protocol.
        /// Negative ids - currently the canonical ANY protocol <see cref="FWO.Basics.GlobalConst.kAnyIpProtocolId"/> -
        /// are written by the platform itself and must never be selectable in a request.
        /// </summary>
        /// <param name="protoId">The IP protocol id to classify.</param>
        /// <returns>True when the id is reserved for internal use.</returns>
        public static bool IsInternalProtocolId(int protoId)
        {
            return protoId < 0;
        }

        /// <summary>
        /// Whether the protocol id of a request element may be written by a user role.
        /// </summary>
        /// <param name="protoId">The IP protocol id, or null when the element carries no protocol.</param>
        /// <returns>True when the id is unset or names a real IP protocol.</returns>
        public static bool IsRequestableProtocolId(int? protoId)
        {
            return !protoId.HasValue || !IsInternalProtocolId(protoId.Value);
        }

        /// <summary>
        /// Whether a Flow network object is still offered and live, so that it may be attached to a
        /// workflow element.
        /// </summary>
        /// <param name="nwObject">The Flow network object to check.</param>
        /// <returns>True when the object is visible and live.</returns>
        public static bool IsLive(FlowNwObject? nwObject)
        {
            return nwObject != null && HasLiveLifecycle(nwObject.ShowInRequestModule, nwObject.State, nwObject.RemovedDate);
        }

        /// <summary>
        /// Whether a Flow service object is still offered and live, so that it may be attached to a
        /// workflow element. The canonical ANY service passes here: the platform attaches it itself when
        /// it turns a protocol-agnostic request into a flow, and only user roles are barred from
        /// referencing it - by the Hasura permissions rather than by this predicate.
        /// </summary>
        /// <param name="svcObject">The Flow service object to check.</param>
        /// <returns>True when the object is visible and live.</returns>
        public static bool IsLive(FlowSvcObject? svcObject)
        {
            return svcObject != null && HasLiveLifecycle(svcObject.ShowInRequestModule, svcObject.State, svcObject.RemovedDate);
        }

        /// <summary>
        /// Whether a Flow time object is still offered and live.
        /// </summary>
        /// <param name="timeObject">The Flow time object to check.</param>
        /// <returns>True when the object is visible and live.</returns>
        public static bool IsLive(FlowTimeObject? timeObject)
        {
            return timeObject != null && HasLiveLifecycle(timeObject.ShowInRequestModule, timeObject.State, timeObject.RemovedDate);
        }

        /// <summary>
        /// Whether a Flow group is still offered and live. A group without a name cannot be referenced by
        /// name and is therefore not offered.
        /// </summary>
        /// <param name="group">The Flow network or service group to check.</param>
        /// <returns>True when the group is named, visible and live.</returns>
        public static bool IsLive(FlowGroup? group)
        {
            return group != null
                && !string.IsNullOrWhiteSpace(group.Name)
                && HasLiveLifecycle(group.ShowInRequestModule, group.State, group.RemovedDate);
        }

        /// <summary>
        /// Whether a Flow network object may be picked in the request module.
        /// </summary>
        /// <param name="nwObject">The Flow network object to check.</param>
        /// <returns>True when the object is visible and live.</returns>
        public static bool IsRequestable(FlowNwObject? nwObject)
        {
            return IsLive(nwObject);
        }

        /// <summary>
        /// Whether a Flow service object may be picked in the request module.
        /// </summary>
        /// <param name="svcObject">The Flow service object to check.</param>
        /// <returns>True when the object is visible, live and not an internal protocol representation.</returns>
        public static bool IsRequestable(FlowSvcObject? svcObject)
        {
            return IsLive(svcObject) && !IsInternalProtocolId(svcObject!.ProtoId);
        }

        /// <summary>
        /// Whether a Flow time object may be picked in the request module.
        /// </summary>
        /// <param name="timeObject">The Flow time object to check.</param>
        /// <returns>True when the object is visible and live.</returns>
        public static bool IsRequestable(FlowTimeObject? timeObject)
        {
            return IsLive(timeObject);
        }

        /// <summary>
        /// Whether a Flow group may be picked in the request module.
        /// </summary>
        /// <param name="group">The Flow network or service group to check.</param>
        /// <returns>True when the group is named, visible and live.</returns>
        public static bool IsRequestable(FlowGroup? group)
        {
            return IsLive(group);
        }

        /// <summary>
        /// Applies the lifecycle part of the eligibility predicate that every flow catalog table shares.
        /// </summary>
        /// <param name="showInRequestModule">Whether the entry is offered in the request module.</param>
        /// <param name="state">The entry state.</param>
        /// <param name="removedDate">When the entry was retired, null while it is live.</param>
        /// <returns>True when the entry is visible and live.</returns>
        private static bool HasLiveLifecycle(bool showInRequestModule, string state, DateTime? removedDate)
        {
            return showInRequestModule && removedDate == null && kLiveStates.Contains(state);
        }
    }
}

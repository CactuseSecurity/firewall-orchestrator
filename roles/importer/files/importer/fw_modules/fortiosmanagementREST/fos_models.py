from typing import Any, Literal

from pydantic import BaseModel, Field

# ============================================================================
# Network Object Models
# ============================================================================


class NameRef(BaseModel):
    """Generic name reference used in member lists"""

    name: str
    q_origin_key: str


class NwObjAddress(BaseModel):
    """firewall/address - IPv4 network address objects"""

    name: str
    q_origin_key: str
    uuid: str
    type: Literal["ipmask", "iprange", "fqdn", "wildcard", "geography", "wildcard-fqdn", "dynamic", "interface-subnet"]

    # ipmask type fields
    subnet: str | None = None

    # iprange type fields
    start_ip: str | None = Field(None, alias="start-ip")
    end_ip: str | None = Field(None, alias="end-ip")

    # fqdn type fields
    fqdn: str | None = None

    # interface-subnet type fields
    interface: str | None = None

    # dynamic type fields
    sdn: str | None = None
    obj_tag: str | None = Field(None, alias="obj-tag")
    filter_: str | None = Field(None, alias="filter")

    # Common optional fields
    sub_type: str | None = Field(None, alias="sub-type")
    clearpass_spt: str | None = Field(None, alias="clearpass-spt")
    macaddr: list[Any] = []
    country: str | None = None
    cache_ttl: int | None = Field(None, alias="cache-ttl")
    fsso_group: list[Any] = Field(default=[], alias="fsso-group")
    obj_type: str | None = Field(None, alias="obj-type")
    tag_detection_level: str | None = Field(None, alias="tag-detection-level")
    tag_type: str | None = Field(None, alias="tag-type")
    dirty: str | None = None
    comment: str | None = None
    associated_interface: str | None = Field(None, alias="associated-interface")
    color: int | None = None
    sdn_addr_type: str | None = Field(None, alias="sdn-addr-type")
    node_ip_only: str | None = Field(None, alias="node-ip-only")
    obj_id: str | None = Field(None, alias="obj-id")
    list_: list[Any] = Field(default=[], alias="list")
    tagging: list[Any] = []
    allow_routing: str | None = Field(None, alias="allow-routing")
    fabric_object: str | None = Field(None, alias="fabric-object")


class NwObjAddress6(BaseModel):
    """firewall/address6 - IPv6 network address objects"""

    name: str
    q_origin_key: str
    uuid: str
    type: Literal["ipprefix", "iprange", "fqdn", "dynamic", "template"]

    # ipprefix type fields
    ip6: str | None = None

    # iprange type fields
    start_ip: str | None = Field(None, alias="start-ip")
    end_ip: str | None = Field(None, alias="end-ip")

    # fqdn type fields
    fqdn: str | None = None

    # dynamic type fields
    sdn: str | None = None
    sdn_tag: str | None = Field(None, alias="sdn-tag")

    # Common optional fields
    macaddr: list[Any] = []
    country: str | None = None
    cache_ttl: int | None = Field(None, alias="cache-ttl")
    color: int | None = None
    obj_id: str | None = Field(None, alias="obj-id")
    list_: list[Any] = Field(default=[], alias="list")
    tagging: list[Any] = []
    comment: str | None = None
    template: str | None = None
    subnet_segment: list[Any] = Field(default=[], alias="subnet-segment")
    host_type: str | None = Field(None, alias="host-type")
    tenant: str | None = None
    epg_name: str | None = Field(None, alias="epg-name")
    fabric_object: str | None = Field(None, alias="fabric-object")


class NwObjAddrGrp(BaseModel):
    """firewall/addrgrp - IPv4 address groups"""

    name: str
    q_origin_key: str
    uuid: str
    type: str | None = None
    category: str | None = None
    member: list[NameRef] = []
    comment: str | None = None
    exclude: str | None = None
    exclude_member: list[NameRef] = Field(default=[], alias="exclude-member")
    color: int | None = None
    tagging: list[Any] = []
    allow_routing: str | None = Field(None, alias="allow-routing")
    fabric_object: str | None = Field(None, alias="fabric-object")


class NwObjAddrGrp6(BaseModel):
    """firewall/addrgrp6 - IPv6 address groups"""

    name: str
    q_origin_key: str
    uuid: str
    color: int | None = None
    comment: str | None = None
    member: list[NameRef] = []
    tagging: list[Any] = []
    fabric_object: str | None = Field(None, alias="fabric-object")


class NwObjIpPool(BaseModel):
    """firewall/ippool - IP pool objects for NAT"""

    name: str
    q_origin_key: str
    type: str  # Seen: "overload", likely also: "one-to-one", "fixed-port-range", "port-block-allocation"
    startip: str
    endip: str
    startport: int | None = None
    endport: int | None = None
    source_startip: str | None = Field(None, alias="source-startip")
    source_endip: str | None = Field(None, alias="source-endip")
    block_size: int | None = Field(None, alias="block-size")
    port_per_user: int | None = Field(None, alias="port-per-user")
    num_blocks_per_user: int | None = Field(None, alias="num-blocks-per-user")
    pba_timeout: int | None = Field(None, alias="pba-timeout")
    permit_any_host: str | None = Field(None, alias="permit-any-host")
    arp_reply: str | None = Field(None, alias="arp-reply")
    arp_intf: str | None = Field(None, alias="arp-intf")
    associated_interface: str | None = Field(None, alias="associated-interface")
    comments: str | None = None
    nat64: str | None = None
    add_nat64_route: str | None = Field(None, alias="add-nat64-route")


class NwObjVip(BaseModel):  # TODO: correct? no example data available
    """firewall/vip - Virtual IP objects for destination NAT"""

    name: str
    q_origin_key: str
    uuid: str | None = None
    type: str | None = None
    extip: str | None = None  # External IP
    extintf: str | None = None  # External interface
    mappedip: list[NameRef] | str | None = None  # Mapped internal IP(s)
    comment: str | None = None
    color: int | None = None
    extport: str | None = None
    mappedport: str | None = None
    protocol: str | None = None
    portforward: str | None = None


class NwObjInternetService(BaseModel):
    """firewall/internet-service - Internet service objects"""

    id_: int = Field(alias="id")
    q_origin_key: int
    name: str
    icon_id: int = Field(alias="icon-id")
    direction: str
    database: str
    ip_range_number: int = Field(alias="ip-range-number")
    extra_ip_range_number: int = Field(alias="extra-ip-range-number")
    ip_number: int = Field(alias="ip-number")
    ip6_range_number: int = Field(alias="ip6-range-number")
    extra_ip6_range_number: int = Field(alias="extra-ip6-range-number")
    singularity: int
    obsolete: int


class NwObjInternetServiceGroup(BaseModel):  # TODO: correct? no example data available
    """firewall/internet-service-group - Internet service groups"""

    name: str
    q_origin_key: str
    comment: str | None = None
    member: list[NameRef] = []


class ZoneObject(BaseModel):
    """zone object model"""

    zone_name: str


# ============================================================================
# Service Object Models
# ============================================================================


class AppCategory(BaseModel):
    """Application category reference"""

    id_: int = Field(alias="id")


class SvcObjApplicationList(BaseModel):
    """application/list - Application filter lists"""

    name: str
    q_origin_key: str
    comment: str | None = None
    replacemsg_group: str | None = Field(None, alias="replacemsg-group")
    extended_log: str | None = Field(None, alias="extended-log")
    other_application_action: str | None = Field(None, alias="other-application-action")
    app_replacemsg: str | None = Field(None, alias="app-replacemsg")
    other_application_log: str | None = Field(None, alias="other-application-log")
    enforce_default_app_port: str | None = Field(None, alias="enforce-default-app-port")
    force_inclusion_ssl_di_sigs: str | None = Field(None, alias="force-inclusion-ssl-di-sigs")
    unknown_application_action: str | None = Field(None, alias="unknown-application-action")
    unknown_application_log: str | None = Field(None, alias="unknown-application-log")
    default_network_services: list[Any] = Field(default=[], alias="default-network-services")
    control_default_network_services: str | None = Field(None, alias="control-default-network-services")
    options: str
    entries: list[dict[str, Any]] = []
    # entries structure is complex with app-category, application, etc.
    # keeping as generic dict since it's not directly used in normalization


class SvcObjApplicationGroup(BaseModel):  # TODO: correct? no example data available
    """application/group - Application groups"""

    name: str
    q_origin_key: str
    comment: str | None = None
    type: str | None = None
    category: list[AppCategory] = []
    application: list[NameRef] = []


class SvcObjCustom(BaseModel):
    """firewall.service/custom - Custom service objects"""

    name: str
    q_origin_key: str
    proxy: str | None = None
    category: str | None = None
    protocol: Literal["ALL", "TCP/UDP/SCTP", "ICMP", "ICMP6", "IP"] | None = None
    helper: str | None = None
    iprange: str | None = None
    fqdn: str | None = None

    # TCP/UDP/SCTP port ranges (can have spaces like "80 8080" or single like "443")
    tcp_portrange: str | None = Field(None, alias="tcp-portrange")
    udp_portrange: str | None = Field(None, alias="udp-portrange")
    sctp_portrange: str | None = Field(None, alias="sctp-portrange")

    # IP protocol number
    protocol_number: int | None = Field(None, alias="protocol-number")

    # Timers
    tcp_halfclose_timer: int | None = Field(None, alias="tcp-halfclose-timer")
    tcp_halfopen_timer: int | None = Field(None, alias="tcp-halfopen-timer")
    tcp_timewait_timer: int | None = Field(None, alias="tcp-timewait-timer")
    tcp_rst_timer: int | None = Field(None, alias="tcp-rst-timer")
    udp_idle_timer: int | None = Field(None, alias="udp-idle-timer")
    session_ttl: str | None = Field(None, alias="session-ttl")

    # Common fields
    check_reset_range: str | None = Field(None, alias="check-reset-range")
    comment: str | None = None
    color: int | None = None
    visibility: str | None = None
    app_service_type: str | None = Field(None, alias="app-service-type")
    app_category: list[Any] = Field(default=[], alias="app-category")
    application: list[Any] = []
    fabric_object: str | None = Field(None, alias="fabric-object")


class SvcObjGroup(BaseModel):
    """firewall.service/group - Service groups"""

    name: str
    q_origin_key: str
    proxy: str | None = None
    member: list[NameRef] = []
    comment: str | None = None
    color: int | None = None
    fabric_object: str | None = Field(None, alias="fabric-object")


# ============================================================================
# User Object Models
# ============================================================================


class UserObjLocal(BaseModel):
    """user/local - Local user objects"""

    name: str
    q_origin_key: str
    id_: int = Field(alias="id")
    status: str
    type: str  # Seen: "password", likely also: "radius", "ldap", "tacacs+"
    passwd: str | None = None
    ldap_server: str | None = Field(None, alias="ldap-server")
    radius_server: str | None = Field(None, alias="radius-server")
    tacacs_server: str | None = Field(None, alias="tacacs+-server")
    two_factor: str | None = Field(None, alias="two-factor")
    two_factor_authentication: str | None = Field(None, alias="two-factor-authentication")
    two_factor_notification: str | None = Field(None, alias="two-factor-notification")
    fortitoken: str | None = None
    email_to: str | None = Field(None, alias="email-to")
    sms_server: str | None = Field(None, alias="sms-server")
    sms_custom_server: str | None = Field(None, alias="sms-custom-server")
    sms_phone: str | None = Field(None, alias="sms-phone")
    passwd_policy: str | None = Field(None, alias="passwd-policy")
    passwd_time: str | None = Field(None, alias="passwd-time")
    authtimeout: int | None = None
    workstation: str | None = None
    auth_concurrent_override: str | None = Field(None, alias="auth-concurrent-override")
    auth_concurrent_value: int | None = Field(None, alias="auth-concurrent-value")
    ppk_secret: str | None = Field(None, alias="ppk-secret")
    ppk_identity: str | None = Field(None, alias="ppk-identity")
    username_sensitivity: str | None = Field(None, alias="username-sensitivity")


class UserObjGroup(BaseModel):
    """user/group - User groups"""

    name: str
    q_origin_key: str
    id_: int = Field(alias="id")
    group_type: str = Field(alias="group-type")
    authtimeout: int | None = None
    auth_concurrent_override: str | None = Field(None, alias="auth-concurrent-override")
    auth_concurrent_value: int | None = Field(None, alias="auth-concurrent-value")
    http_digest_realm: str | None = Field(None, alias="http-digest-realm")
    sso_attribute_value: str | None = Field(None, alias="sso-attribute-value")
    member: list[NameRef] = []
    match: list[Any] = []

    # Guest user specific fields
    user_id: str | None = Field(None, alias="user-id")
    password: str | None = None
    user_name: str | None = Field(None, alias="user-name")
    sponsor: str | None = None
    company: str | None = None
    email: str | None = None
    mobile_phone: str | None = Field(None, alias="mobile-phone")
    sms_server: str | None = Field(None, alias="sms-server")
    sms_custom_server: str | None = Field(None, alias="sms-custom-server")
    expire_type: str | None = Field(None, alias="expire-type")
    expire: int | None = None
    max_accounts: int | None = Field(None, alias="max-accounts")
    multiple_guest_add: str | None = Field(None, alias="multiple-guest-add")
    guest: list[Any] = []


# ============================================================================
# Rule Models
# ============================================================================


class Rule(BaseModel):
    """firewall/policy - Firewall access rules"""

    policyid: int
    q_origin_key: int
    status: Literal["enable", "disable"]
    name: str | None = None
    uuid: str
    uuid_idx: int | None = Field(None, alias="uuid-idx")

    # Interfaces
    srcintf: list[NameRef] = []
    dstintf: list[NameRef] = []

    # Action
    action: str  # Seen: "accept", "deny" (code checks for 'deny'), possibly also: "ipsec"

    # NAT
    nat: str | None = None
    nat64: str | None = None
    nat46: str | None = None

    # Addresses
    srcaddr: list[NameRef] = []
    dstaddr: list[NameRef] = []
    srcaddr6: list[NameRef] = []
    dstaddr6: list[NameRef] = []

    # Internet services
    internet_service: str | None = Field(None, alias="internet-service")
    internet_service_name: list[NameRef] = Field(default=[], alias="internet-service-name")
    internet_service_group: list[NameRef] = Field(default=[], alias="internet-service-group")
    internet_service_custom: list[NameRef] = Field(default=[], alias="internet-service-custom")
    internet_service_custom_group: list[NameRef] = Field(default=[], alias="internet-service-custom-group")
    internet_service_src: str | None = Field(None, alias="internet-service-src")
    internet_service_src_name: list[NameRef] = Field(default=[], alias="internet-service-src-name")
    internet_service_src_group: list[NameRef] = Field(default=[], alias="internet-service-src-group")
    internet_service_src_custom: list[NameRef] = Field(default=[], alias="internet-service-src-custom")
    internet_service_src_custom_group: list[NameRef] = Field(default=[], alias="internet-service-src-custom-group")

    # IPv6 Internet services
    internet_service6: str | None = Field(None, alias="internet-service6")
    internet_service6_name: list[NameRef] = Field(default=[], alias="internet-service6-name")
    internet_service6_group: list[NameRef] = Field(default=[], alias="internet-service6-group")
    internet_service6_custom: list[NameRef] = Field(default=[], alias="internet-service6-custom")
    internet_service6_custom_group: list[NameRef] = Field(default=[], alias="internet-service6-custom-group")
    internet_service6_src: str | None = Field(None, alias="internet-service6-src")
    internet_service6_src_name: list[NameRef] = Field(default=[], alias="internet-service6-src-name")
    internet_service6_src_group: list[NameRef] = Field(default=[], alias="internet-service6-src-group")
    internet_service6_src_custom: list[NameRef] = Field(default=[], alias="internet-service6-src-custom")
    internet_service6_src_custom_group: list[NameRef] = Field(default=[], alias="internet-service6-src-custom-group")

    # Network services
    network_service_dynamic: list[Any] = Field(default=[], alias="network-service-dynamic")
    network_service_src_dynamic: list[Any] = Field(default=[], alias="network-service-src-dynamic")

    # Services
    service: list[NameRef] = []

    # Schedule
    schedule: str | None = None
    schedule_timeout: str | None = Field(None, alias="schedule-timeout")

    # Policy expiry
    policy_expiry: str | None = Field(None, alias="policy-expiry")
    policy_expiry_date: str | None = Field(None, alias="policy-expiry-date")

    # Negation flags
    srcaddr_negate: str | None = Field(None, alias="srcaddr-negate")
    srcaddr6_negate: str | None = Field(None, alias="srcaddr6-negate")
    dstaddr_negate: str | None = Field(None, alias="dstaddr-negate")
    dstaddr6_negate: str | None = Field(None, alias="dstaddr6-negate")
    service_negate: str | None = Field(None, alias="service-negate")
    internet_service_negate: str | None = Field(None, alias="internet-service-negate")
    internet_service_src_negate: str | None = Field(None, alias="internet-service-src-negate")
    internet_service6_negate: str | None = Field(None, alias="internet-service6-negate")
    internet_service6_src_negate: str | None = Field(None, alias="internet-service6-src-negate")

    # Logging
    logtraffic: str | None = None  # Seen: "disable", "utm", likely also: "all"
    logtraffic_start: str | None = Field(None, alias="logtraffic-start")

    # Security profiles
    utm_status: str | None = Field(None, alias="utm-status")
    inspection_mode: str | None = Field(None, alias="inspection-mode")
    profile_type: str | None = Field(None, alias="profile-type")
    profile_group: str | None = Field(None, alias="profile-group")
    profile_protocol_options: str | None = Field(None, alias="profile-protocol-options")
    ssl_ssh_profile: str | None = Field(None, alias="ssl-ssh-profile")
    av_profile: str | None = Field(None, alias="av-profile")
    webfilter_profile: str | None = Field(None, alias="webfilter-profile")
    dnsfilter_profile: str | None = Field(None, alias="dnsfilter-profile")
    emailfilter_profile: str | None = Field(None, alias="emailfilter-profile")
    dlp_profile: str | None = Field(None, alias="dlp-profile")
    file_filter_profile: str | None = Field(None, alias="file-filter-profile")
    ips_sensor: str | None = Field(None, alias="ips-sensor")
    application_list: str | None = Field(None, alias="application-list")
    voip_profile: str | None = Field(None, alias="voip-profile")
    waf_profile: str | None = Field(None, alias="waf-profile")
    ssh_filter_profile: str | None = Field(None, alias="ssh-filter-profile")

    # NAT configuration
    ippool: str | None = None
    poolname: list[NameRef] = []
    poolname6: list[NameRef] = []
    permit_any_host: str | None = Field(None, alias="permit-any-host")
    permit_stun_host: str | None = Field(None, alias="permit-stun-host")
    fixedport: str | None = None
    natip: str | None = None

    # Direction
    inbound: str | None = None
    outbound: str | None = None
    natinbound: str | None = None
    natoutbound: str | None = None

    # VIP matching
    match_vip: str | None = Field(None, alias="match-vip")
    match_vip_only: str | None = Field(None, alias="match-vip-only")

    # Users and authentication
    groups: list[NameRef] = []
    users: list[NameRef] = []
    fsso_groups: list[NameRef] = Field(default=[], alias="fsso-groups")
    auth_path: str | None = Field(None, alias="auth-path")

    # VPN
    vpntunnel: str | None = None

    # Traffic shaping
    traffic_shaper: str | None = Field(None, alias="traffic-shaper")
    traffic_shaper_reverse: str | None = Field(None, alias="traffic-shaper-reverse")
    per_ip_shaper: str | None = Field(None, alias="per-ip-shaper")

    # QoS
    tos: str | None = None
    tos_mask: str | None = Field(None, alias="tos-mask")
    tos_negate: str | None = Field(None, alias="tos-negate")
    vlan_cos_fwd: int | None = Field(None, alias="vlan-cos-fwd")
    vlan_cos_rev: int | None = Field(None, alias="vlan-cos-rev")

    # Comments and labels
    comments: str | None = None
    label: str | None = None
    global_label: str | None = Field(None, alias="global-label")

    # ZTNA
    ztna_status: str | None = Field(None, alias="ztna-status")
    ztna_ems_tag: list[Any] = Field(default=[], alias="ztna-ems-tag")
    ztna_geo_tag: list[Any] = Field(default=[], alias="ztna-geo-tag")

    # Reputation
    reputation_minimum: int | None = Field(None, alias="reputation-minimum")
    reputation_direction: str | None = Field(None, alias="reputation-direction")
    reputation_minimum6: int | None = Field(None, alias="reputation-minimum6")
    reputation_direction6: str | None = Field(None, alias="reputation-direction6")

    # Misc
    send_deny_packet: str | None = Field(None, alias="send-deny-packet")
    firewall_session_dirty: str | None = Field(None, alias="firewall-session-dirty")
    session_ttl: str | None = Field(None, alias="session-ttl")
    anti_replay: str | None = Field(None, alias="anti-replay")
    tcp_session_without_syn: str | None = Field(None, alias="tcp-session-without-syn")
    geoip_anycast: str | None = Field(None, alias="geoip-anycast")
    geoip_match: str | None = Field(None, alias="geoip-match")
    dynamic_shaping: str | None = Field(None, alias="dynamic-shaping")
    passive_wan_health_measurement: str | None = Field(None, alias="passive-wan-health-measurement")
    auto_asic_offload: str | None = Field(None, alias="auto-asic-offload")
    np_acceleration: str | None = Field(None, alias="np-acceleration")
    rtp_nat: str | None = Field(None, alias="rtp-nat")
    rtp_addr: list[Any] = Field(default=[], alias="rtp-addr")
    wccp: str | None = None
    fec: str | None = None

    # Authentication
    ntlm: str | None = None
    ntlm_guest: str | None = Field(None, alias="ntlm-guest")
    ntlm_enabled_browsers: list[Any] = Field(default=[], alias="ntlm-enabled-browsers")
    fsso_agent_for_ntlm: str | None = Field(None, alias="fsso-agent-for-ntlm")
    disclaimer: str | None = None
    email_collect: str | None = Field(None, alias="email-collect")
    auth_cert: str | None = Field(None, alias="auth-cert")
    auth_redirect_addr: str | None = Field(None, alias="auth-redirect-addr")

    # Redirect
    redirect_url: str | None = Field(None, alias="redirect-url")
    http_policy_redirect: str | None = Field(None, alias="http-policy-redirect")
    ssh_policy_redirect: str | None = Field(None, alias="ssh-policy-redirect")
    webproxy_profile: str | None = Field(None, alias="webproxy-profile")
    webproxy_forward_server: str | None = Field(None, alias="webproxy-forward-server")

    # Diffserv
    diffserv_copy: str | None = Field(None, alias="diffserv-copy")
    diffserv_forward: str | None = Field(None, alias="diffserv-forward")
    diffserv_reverse: str | None = Field(None, alias="diffserv-reverse")
    diffservcode_forward: str | None = Field(None, alias="diffservcode-forward")
    diffservcode_rev: str | None = Field(None, alias="diffservcode-rev")

    # TCP MSS
    tcp_mss_sender: int | None = Field(None, alias="tcp-mss-sender")
    tcp_mss_receiver: int | None = Field(None, alias="tcp-mss-receiver")

    # Other
    identity_based_route: str | None = Field(None, alias="identity-based-route")
    block_notification: str | None = Field(None, alias="block-notification")
    custom_log_fields: list[Any] = Field(default=[], alias="custom-log-fields")
    replacemsg_override_group: str | None = Field(None, alias="replacemsg-override-group")
    timeout_send_rst: str | None = Field(None, alias="timeout-send-rst")
    captive_portal_exempt: str | None = Field(None, alias="captive-portal-exempt")
    decrypted_traffic_mirror: str | None = Field(None, alias="decrypted-traffic-mirror")
    dsri: str | None = None
    radius_mac_auth_bypass: str | None = Field(None, alias="radius-mac-auth-bypass")
    delay_tcp_npu_session: str | None = Field(None, alias="delay-tcp-npu-session")
    vlan_filter: str | None = Field(None, alias="vlan-filter")
    sgt_check: str | None = Field(None, alias="sgt-check")
    sgt: list[Any] = []
    src_vendor_mac: list[Any] = Field(default=[], alias="src-vendor-mac")
    sctp_filter_profile: str | None = Field(None, alias="sctp-filter-profile")
    icap_profile: str | None = Field(None, alias="icap-profile")
    cifs_profile: str | None = Field(None, alias="cifs-profile")
    videofilter_profile: str | None = Field(None, alias="videofilter-profile")

    # Internal tracking field
    _last_hit: int | None = None


# ============================================================================
# Main Configuration Model
# ============================================================================


class FortiOSConfig(BaseModel):
    """Complete FortiOS configuration structure"""

    # Network objects
    nw_obj_address: list[NwObjAddress] = Field(default=[], alias="nw_obj_firewall/address")
    nw_obj_address6: list[NwObjAddress6] = Field(default=[], alias="nw_obj_firewall/address6")
    nw_obj_addrgrp: list[NwObjAddrGrp] = Field(default=[], alias="nw_obj_firewall/addrgrp")
    nw_obj_addrgrp6: list[NwObjAddrGrp6] = Field(default=[], alias="nw_obj_firewall/addrgrp6")
    nw_obj_ippool: list[NwObjIpPool] = Field(default=[], alias="nw_obj_firewall/ippool")
    nw_obj_vip: list[NwObjVip] = Field(default=[], alias="nw_obj_firewall/vip")
    nw_obj_internet_service: list[NwObjInternetService] = Field(default=[], alias="nw_obj_firewall/internet-service")
    nw_obj_internet_service_group: list[NwObjInternetServiceGroup] = Field(
        default=[], alias="nw_obj_firewall/internet-service-group"
    )

    # Service objects
    svc_obj_application_list: list[SvcObjApplicationList] = Field(default=[], alias="svc_obj_application/list")
    svc_obj_application_group: list[SvcObjApplicationGroup] = Field(default=[], alias="svc_obj_application/group")
    svc_obj_custom: list[SvcObjCustom] = Field(default=[], alias="svc_obj_firewall.service/custom")
    svc_obj_group: list[SvcObjGroup] = Field(default=[], alias="svc_obj_firewall.service/group")

    # User objects
    user_obj_local: list[UserObjLocal] = Field(default=[], alias="user_obj_user/local")
    user_obj_group: list[UserObjGroup] = Field(default=[], alias="user_obj_user/group")

    # Zone objects
    zone_objects: list[ZoneObject] = []

    # Rules
    rules: list[Rule] = []
    rules_nat: dict[str, Any] = Field(default_factory=dict)

    # Lookup dictionaries (built during processing)
    nw_obj_lookup_dict: dict[str, str] = Field(default_factory=dict)
    svc_obj_lookup_dict: dict[str, str] = Field(default_factory=dict)

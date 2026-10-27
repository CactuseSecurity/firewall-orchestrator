# Phase 4 open-questions worksheet

## How to use this document

Write your thoughts below each **Your notes** heading. Ordinary language, examples, guesses, and partial answers are all welcome; no Check Point or FWO terminology is required.

These notes are working material. We will review them together before turning them into binding decisions or implementation requirements.

---

## Question 1 — What can a layer guard contain?

### Explain it like I am five

Think of an inline layer as a room with a guard at its door. The guard rule says:

> Traffic from these addresses to those addresses may enter this room.

To decide whether a new rule belongs in that room, FWO must understand everything written in the guard rule's source and destination lists.

Those lists could contain:

- a plain IP address, such as `10.0.0.10`;
- a network, such as `10.0.0.0/24`;
- a group containing several addresses or networks;
- `Any`, meaning every address;
- a negated entry, meaning “everything except this address or network”;
- a dynamic object whose addresses can change; or
- an IPv6 address or network.

### What we need to know

What kinds of objects does KfW actually put into the source and destination of its layer-guard rules?

Helpful things you might describe:

- Are the guards always made from normal IP addresses and networks?
- Can they contain groups? If yes, can a group contain another group?
- Can they contain `Any`?
- Can an entry be negated—for example, “everything except network X”?
- Can they contain dynamic objects whose IPs are not fixed in FWO?
- Is IPv6 used?
- Is there anything else that can appear there?

It is completely fine to answer something like:

> The guards only contain normal IPv4 objects and groups. They never contain Any, negation, dynamic objects, nested groups, or IPv6.

### Your notes

Write here:

Alright so inline layers are like the big "buildings" and sections are the "rooms" in that building. A layer guard then is the security guard at the front checking the rules ID badge and says "nuh uh you dont belong in the XYZ building ur badge is for building ABC". Our problem now is that we need to know what the guards are checking for... why? 

Anyway I do know that this customer doesnt use IPv6 but we generally do need to support it in FWO... so its tricky. Ill definetely have to ask this but still do me the favor to explain why we need to know.

---

## Question 2 — Can a rule belong directly in the main layer?

### Explain it like I am five

Imagine the Check Point policy looks like this:

```text
Main layer
├── Guard rule → Inline layer A
├── Guard rule → Inline layer B
└── Other ordinary rules
```

Inline layers have guard rules. FWO can inspect those guards and decide whether all source and destination addresses belong in one of the inline layers.

The main layer itself does not have a guard in front of it. Therefore, the same test cannot naturally select the main layer.

### What we need to know

Are rules handled by this KfW algorithm always placed inside one of the guarded inline layers, or can they sometimes be placed directly in the main layer?

If they can be placed directly in the main layer, how do we recognize that case?

Possible answers might look like:

> These rules always belong in an inline layer. If exactly one guard does not match, positioning fails.

or:

> A rule goes into the main layer when no inline-layer guard matches.

or:

> A separate property in the ticket tells us to use the main layer.

The second and third examples introduce additional rules that must be explicitly confirmed. FWO must not invent a main-layer fallback.

### Your notes

Write here:

Im taking the decision here: No main layer needs to be supported for THIS algorithm but in general the option should be possible. From the KFW files they state that there always has to be one of those inline layers + sections no default. It seems like this main layer would be the default and as such does not need to be considered as they clearly state no defauit.

---

## Question 3 — How should a group that does not exist yet be understood?

### Explain it like I am five

Imagine one ticket says:

```text
1. Create Server-A with address 10.0.0.10
2. Create Server-B with address 10.0.0.11
3. Create group WEB-SERVERS containing Server-A and Server-B
4. Create a firewall rule using WEB-SERVERS
```

When FWO calculates the position for step 4, `WEB-SERVERS` may not exist in the real Flow catalog or on the firewall yet. However, the ticket already describes what the group is supposed to contain.

The likely model is to calculate with the **final expected state**: what the objects and groups will look like after all relevant changes in this ticket have been applied.

### What we need to know

Should positioning use the final expected object and group state produced by the ticket?

Helpful things you might describe:

- For a newly created group, are its intended members fully listed in the group task?
- For a modified group, should FWO start with its existing members and apply the additions and removals from the ticket?
- If the ticket changes a group that already exists in the Flow catalog, should the ticket's intended final version win?
- Can a group contain another group?
- Can multiple tasks in the same ticket modify the same group?
- How does a rule refer to a new group before it has a permanent ID: by name, a temporary ID, or something else?
- If FWO cannot determine one unambiguous final membership, should positioning fail?

A possible overall answer might be:

> Use the final expected state after applying all object and group tasks in the ticket. The ticket version overrides the older catalog version. If the final state cannot be determined unambiguously, fail positioning.

### Your notes

Write here:

Again... why do we need to know?
I imagine we need it because we need the IP addresses to resolve the zones which should work fine. But why do we need to reference said group other than whatever is in the ticket?
Generally how it should work if this is only about the IPs
-> Get the groups and fully resolve them, yes groups can contain other groups. Once fully resolved check if all their IPs lie in a single zone for sources and a single zone for destinations if yes continue if no abort.

---

## Review outcome

Leave this section empty for now. We will fill it together after reviewing your notes.

### Confirmed decisions

- The KfW algorithm targets guarded inline layers only. The main layer is neither a target nor a fallback; zero or multiple matching inline-layer guards fail.
- Referenced groups are recursively expanded to leaf IP ranges, including nested groups. Missing members, ambiguity, and cycles fail.
- When the current ticket creates or changes a group, its final intended state overrides the older Flow-catalog version.
- Ticket creators are responsible for defining object and group changes before rules that consume them. Positioning does not guess around a ticket that violates this protocol.
- KfW uses IPv4 for this algorithm. Shared FWO IP normalization and containment must still remain IPv4/IPv6 aware.
- KfW layer guards use normal source/destination fields. `Any` is common and must match every requested range on that side.
- KfW guarantees that every new rule for this algorithm is completely contained by exactly one layer guard. This must not be assumed for general Check Point inline rules.
- Positioning trusts the normalized fixed-import database representation for every object. Importer mistakes remain importer defects; positioning does not maintain a second customer allow-list.
- IP-less imported guard objects use three-state containment. Unique selection requires one definite matching layer and definite non-matches for every other candidate.


### Questions that still need an expert

- None for the settled KfW Check Point calculation behavior. Broader work-package scope and technical contracts are tracked in the active planning documents.


### Technical follow-up

- Normalize every supported guard object kind into comparable, address-family-aware IP ranges.
- Construct the ticket's final effective group state from current members plus additions and removals, recursively resolving nested groups with ticket state taking precedence over catalog state.
- Supply the complete relevant ticket-task projection to positioning; the current callback provides only the access task or merged access bundle.
- Apply normalized imported semantics as-is. Do not repair lossy imports in positioning; route those defects back to the importer.
- For an IP-less positive object, allow known positive ranges or `Any` to prove containment; otherwise return indeterminate. Any indeterminate competing guard prevents unique selection.


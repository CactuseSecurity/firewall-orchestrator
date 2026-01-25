# Rulebase Links

## Concept

- each firewall device defines one directed tree for its rulebases
- rulebases are the tree verticies and rulebase links are tree edges
- rulebases contain a (possibly empty) list of rules and a uid/id
- in context of database we use ids, in importer context uids, here we default to ids
- each rulebase tree is rooted, this is defined with the initial rulebase link
- rulebase links have many fields, each is explained in this document

## Rulebase Link Fields

### From and To Rulebase Id

- **from_rulebase_id** and **to_rulebase_id** constitute the directed tree edges
- both fields are not NULL, with one exception, from_rulebase_id is empty for the initial rulebase link
- sometimes rulebases need to be linked from a specific rule (e.g. inline-layer, domain)
- this is done with **from_rule_id**
- from _rule_id is a rule id that is in the from_rulebase_id

### Boolean Flags

- **is_initial** is true, if to_rulebase_id is the first rulebase for the device
- **is_global** is true, if to_rulebase_id belongs to the super manager
- **is_section** is true, if to_rulebase_id gets a section header in reporting
- sections get a section header in reporting and are collapsible

### Types

Each link has a **type** that explains
- how the rulebase tree is traversed by incoming traffic requests
- how the rules report should look like
- how rule numbers are created

#### Ordered

- high level rulebase concept
- ordered rulebases are a chain
- each ordered rulebase needs one accept rule to allow traffic request
- first ordered rulebase gets number 1 and so on
- rules are numbered in dot notation, 2.13 is the 13th rule in the 2nd ordered rulebase
- if only one (initial) ordered rulebase exists the number 1. is ommited for all rules
- each ordered rulebase gets a header that is collapsible in reporting

#### Domain

- from_rulebase_id is a global rulebase, to_rulebase_id is a local rulebase
- from_rule_id is defined and named placeholder rule
- traffic request traverses from_rulebase_id up to and including placeholder rule
- then domain rules are traversed and after that the remaining global rules
- if traffic is droped in domain rules, it is droped overall
- numbering is in dot notation, each domain rule gets the number of placeholder rule as prefix
- 2.3.38 is the 38th rule in the local rulebase, placeholder rule is the 3rd global rule in the 2nd global ordered rulebase
- in reporting to_rulebase_id is collapsible with a the placeholder rule as banner  

#### Concatenated

- from_rulebase_id and to_rulebase_id are the same rulebase in context of a traffic request
- first from_rulebase_id is traversed for an accept rule and if none is found, then to_rulebase_id
- rule numbers are continued, if rulebase 1 ends with rule 16, then rulebase 2 starts with rule 17
- concatenated rulebases are not collapsible in reporting by default, but could be if is_section is true

#### Inline

- from_rulebase_id is interrupted by to_rulebase_id
- from_rule_id is defined and named layer guard
- if layer guard is reached by traffic traversal and allows traffic, then all to_rulebase_id are traversed
- if to_rulebase_id allows traffic it is allowed overall, else from_rulebase_id keeps getting traversed after layer guard
- inline rulebases may be nested
- numbering is in dot notation, each inline rule gets the number of from_rule_id as prefix
- 2.3.18 is the 18th rule in the nested inline rulebase, its layerguard is the 3rd rule in the inline rulebase with layer guard 2 in the ordered layer
- in reporting to_rulebase_id is collapsible with a the layer guard rule as banner 

#### NAT

- each ordered from_rulebase_id may have a nat to_rulebase_id
- nat rulebases do not contain access rules but nat rules
- nat rules are not important for numbering or traffic traversal
- nat rules get their own report

# Jsontron

[![Version](https://img.shields.io/nuget/vpre/Jsontron.svg?color=royalblue)](https://www.nuget.org/packages/Jsontron)
[![Downloads](https://img.shields.io/nuget/dt/Jsontron.svg?color=darkmagenta)](https://www.nuget.org/packages/Jsontron)
[![EULA](https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&color=C9FF30)](https://github.com/devlooped/oss/blob/main/osmfeula.txt)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/devlooped/oss/blob/main/license.txt)

<!-- include https://github.com/devlooped/.github/raw/main/osmf.md -->
## Open Source Maintenance Fee

To ensure the long-term sustainability of this project, users of this package who generate 
revenue must pay an [Open Source Maintenance Fee](https://opensourcemaintenancefee.org). 
While the source code is freely available under the terms of the [License](license.txt), 
this package and other aspects of the project require [adherence to the Maintenance Fee](osmfeula.txt).

To pay the Maintenance Fee, [become a Sponsor](https://github.com/sponsors/devlooped) at the proper 
OSMF tier. A single fee covers all of [Devlooped packages](https://www.nuget.org/profiles/Devlooped).

<!-- https://github.com/devlooped/.github/raw/main/osmf.md -->
<!-- #content -->
## Overview

JSON Schema is excellent at *shape*: types, required fields, formats, enums.
It is awkward at *business rules* that cross fields, compare against policy at
the document root, or select a subset of nodes and assert something about each.

**Jsontron** adds that missing layer on top of
[JsonSchema.Net](https://www.nuget.org/packages/JsonSchema.Net): Schematron-style
`rules` / `assert` keywords whose `context` and `test` expressions are
[jq](https://jqlang.org), evaluated by
[Devlooped.JQSharp](https://www.nuget.org/packages/Devlooped.JQSharp).

- Structural validation stays with JSON Schema.
- Cross-cutting rules stay in the schema, next to the data they describe.
- jq expressions are parsed once when the schema is built, then reused.

## Quick start

```csharp
using System.Text.Json;
using Json.Schema;
using Jsontron;

// Once per process — extends Dialect.Default with rules/assert
MetaSchemas.Register();

var schema = JsonSchema.FromText(
    """
    {
      "type": "object",
      "required": [ "orders", "policy", "approvedCustomers" ],
      "rules": [
        {
          "context": ".orders[] | select(.total > 1000)",
          "asserts": [
            {
              "test": ".discount >= ($root.policy.minHighValueDiscount // 0.1)",
              "message": "High-value order \\(.id // \"?\") discount \\(.discount) is below policy"
            },
            {
              "test": ".customer.id | IN($root.approvedCustomers[])",
              "message": "Customer \\(.customer.id) is not on the approved list"
            }
          ]
        }
      ]
    }
    """);

using var doc = JsonDocument.Parse(
    """
    {
      "policy": { "minHighValueDiscount": 0.15 },
      "approvedCustomers": [ "acme", "globex" ],
      "orders": [
        {
          "id": "O-1001",
          "total": 2500,
          "discount": 0.05,
          "customer": { "id": "initech" }
        },
        {
          "id": "O-1002",
          "total": 80,
          "discount": 0,
          "customer": { "id": "unknown" }
        }
      ]
    }
    """);

var result = schema.Evaluate(
    doc.RootElement,
    new EvaluationOptions { OutputFormat = OutputFormat.List });

// O-1001 fails both asserts (discount + customer).
// O-1002 is ignored by the rule (total ≤ 1000).
Console.WriteLine(result.IsValid); // False
```

## Why jq?

jq already knows how to walk JSON: filter arrays, default missing fields, compare
values, and build messages with string interpolation. Jsontron reuses that instead
of inventing another expression language.

| Idea | In Jsontron |
|------|-------------|
| Select nodes to check | `context` — jq filter over the **current instance** |
| Predicate that must hold | `asserts[].test` — jq filter with `.` = each context node |
| Human-readable failure | `asserts[].message` — jq expression → string |
| Document root | `$root` (always bound for context, test, and message) |

An assert **fails** when the test is not jq-truthy (`false`, `null`, or an empty
stream) — the same idea as Schematron `assert`.

## Keywords

### `rules`

Full form: an array of rules. Each rule picks context nodes, then runs one or more asserts.

```json
{
  "rules": [
    {
      "context": ".lineItems[] | select(.sku | startswith(\"PROMO-\"))",
      "asserts": [
        {
          "test": ".qty <= ($root.promotions[.sku].maxQty // 1)",
          "message": "Promo \\(.sku) allows at most \\($root.promotions[.sku].maxQty // 1), got \\(.qty)"
        }
      ]
    }
  ]
}
```

### `assert` (sugar for `context: "."`)

When the rule is “about this node,” skip the full `rules` array:

```json
{
  "type": "object",
  "properties": {
    "email": { "type": "string", "format": "email" },
    "age": { "type": "integer", "minimum": 0 }
  },
  "assert": ".age >= 18 or (.guardianEmail | type) == \"string\""
}
```

Forms accepted:

| JSON | Meaning |
|------|---------|
| `"assert": "<jq>"` | One assert; test and message source are that expression |
| `"assert": { "test", "message" }` | Explicit message (still a jq string expression) |
| `"assert": [ ... ]` | Several asserts, all on the same `context: "."` rule |

```json
{
  "assert": [
    ".status | IN([\"draft\", \"submitted\", \"approved\"])",
    {
      "test": ".status != \"approved\" or .approver != null",
      "message": "Approved documents require an approver"
    }
  ]
}
```

If both `assert` and `rules` appear on the same schema object, sugar asserts are
**merged** into the rule with `context: "."` (created if needed) and evaluated once.

### Nested schema locations

`assert` / `rules` apply to the instance at that schema location. Under
`properties` / `items`, `.` is the nested value; `$root` remains the full document.

```json
{
  "type": "object",
  "properties": {
    "shipments": {
      "type": "array",
      "items": {
        "type": "object",
        "required": [ "weightKg", "method" ],
        "assert": {
          "test": ".method != \"air\" or .weightKg <= $root.limits.maxAirKg",
          "message": "Air shipment \\(.id // \"?\") exceeds max air weight"
        }
      }
    }
  }
}
```

## Messages

`message` is a **jq expression** that should produce a string. If it does not
start with `"`, Jsontron wraps it as a jq string for you so plain text just works:

```json
"message": "Discount too low"
```

Use jq interpolation when you want values in the text (escape backslashes in JSON):

```json
"message": "Line \\(.sku): qty \\(.qty) exceeds cap"
```

Or start with `"` yourself for a full jq string expression.

## Registration

```csharp
MetaSchemas.Register();
```

That:

1. Extends `Dialect.Default` (and `BuildOptions.Default.Dialect`) with `rules` / `assert`
2. Registers the vocabulary and meta-schema  
   (`https://www.schemastore.org/jsontron-0.1.json`)

After `Register()`, ordinary `JsonSchema.FromText` / `Build` calls pick up the keywords
with no extra `BuildOptions` plumbing.

Keyword syntax is also published as [`schemas/jsontron-0.1.json`](schemas/jsontron-0.1.json).

## Design notes

- **Build-time compile**: invalid keyword shape or invalid jq fails when the schema is built, not on the first instance.
- **No rule shadowing** (v1): every rule runs (closer to Schematron 2025 `group` than classic pattern shadowing).
- **Empty context**: if `context` matches nothing, asserts do not run — vacuously valid.
- **Stack**: JsonSchema.Net 9.x + Devlooped.JQSharp 1.0.2+ (evaluation-time variables for `$root`).

<!-- #content -->
---
<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://avatars.githubusercontent.com/u/71888636?v=4&s=39 "Clarius Org")](https://github.com/clarius)
[![MFB Technologies, Inc.](https://avatars.githubusercontent.com/u/87181630?v=4&s=39 "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://avatars.githubusercontent.com/u/321868?u=99e50a714276c43ae820632f1da88cb71632ec97&v=4&s=39 "SandRock")](https://github.com/sandrock)
[![DRIVE.NET, Inc.](https://avatars.githubusercontent.com/u/15047123?v=4&s=39 "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://avatars.githubusercontent.com/u/16598898?u=64416b80caf7092a885f60bb31612270bffc9598&v=4&s=39 "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://avatars.githubusercontent.com/u/127185?u=7f50babfc888675e37feb80851a4e9708f573386&v=4&s=39 "Thomas Bolon")](https://github.com/tbolon)
[![Kori Francis](https://avatars.githubusercontent.com/u/67574?u=3991fb983e1c399edf39aebc00a9f9cd425703bd&v=4&s=39 "Kori Francis")](https://github.com/kfrancis)
[![Reuben Swartz](https://avatars.githubusercontent.com/u/724704?u=2076fe336f9f6ad678009f1595cbea434b0c5a41&v=4&s=39 "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://avatars.githubusercontent.com/u/480334?v=4&s=39 "Jacob Foshee")](https://github.com/jfoshee)
[![](https://avatars.githubusercontent.com/u/33566379?u=bf62e2b46435a267fa246a64537870fd2449410f&v=4&s=39 "")](https://github.com/Mrxx99)
[![Eric Johnson](https://avatars.githubusercontent.com/u/26369281?u=41b560c2bc493149b32d384b960e0948c78767ab&v=4&s=39 "Eric Johnson")](https://github.com/eajhnsn1)
[![Jonathan ](https://avatars.githubusercontent.com/u/5510103?u=98dcfbef3f32de629d30f1f418a095bf09e14891&v=4&s=39 "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Ken Bonny](https://avatars.githubusercontent.com/u/6417376?u=569af445b6f387917029ffb5129e9cf9f6f68421&v=4&s=39 "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://avatars.githubusercontent.com/u/122666?v=4&s=39 "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://avatars.githubusercontent.com/u/5989304?v=4&s=39 "agileworks-eu")](https://github.com/agileworks-eu)
[![Zheyu Shen](https://avatars.githubusercontent.com/u/4067473?v=4&s=39 "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://avatars.githubusercontent.com/u/87844133?v=4&s=39 "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://avatars.githubusercontent.com/u/16239022?v=4&s=39 "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://avatars.githubusercontent.com/u/68428092?v=4&s=39 "4OTC")](https://github.com/4OTC)
[![domischell](https://avatars.githubusercontent.com/u/66068846?u=0a5c5e2e7d90f15ea657bc660f175605935c5bea&v=4&s=39 "domischell")](https://github.com/DominicSchell)
[![Adrian Alonso](https://avatars.githubusercontent.com/u/2027083?u=129cf516d99f5cb2fd0f4a0787a069f3446b7522&v=4&s=39 "Adrian Alonso")](https://github.com/adalon)
[![torutek](https://avatars.githubusercontent.com/u/33917059?v=4&s=39 "torutek")](https://github.com/torutek)
[![Ryan McCaffery](https://avatars.githubusercontent.com/u/16667079?u=c0daa64bb5c1b572130e05ae2b6f609ecc912d4d&v=4&s=39 "Ryan McCaffery")](https://github.com/mccaffers)
[![Seika Logiciel](https://avatars.githubusercontent.com/u/2564602?v=4&s=39 "Seika Logiciel")](https://github.com/SeikaLogiciel)
[![Andrew Grant](https://avatars.githubusercontent.com/devlooped-user?s=39 "Andrew Grant")](https://github.com/wizardness)
[![eska-gmbh](https://avatars.githubusercontent.com/devlooped-team?s=39 "eska-gmbh")](https://github.com/eska-gmbh)
[![Geodata AS](https://avatars.githubusercontent.com/u/5946299?v=4&s=39 "Geodata AS")](https://github.com/geodata-no)


<!-- sponsors.md -->
[![Sponsor this project](https://avatars.githubusercontent.com/devlooped-sponsor?s=118 "Sponsor this project")](https://github.com/sponsors/devlooped)

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->

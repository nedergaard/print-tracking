# 3D Printing Knowledge System

## Status of this document

This is the design of the Obsidian-based knowledge system. It describes the **intended**
model for the print-tracking feature.

Two things follow from that, and both matter when reading:

- Where this document and the ADRs once disagreed, the ADRs win and this
  document has been updated to match. `adr/0001` through `0007` are the record of why.
- The vault does not fully match this document yet. Roughly 680 legacy notes predate the
  split described below and are converted by a one-shot migration tool. *Divergences From
  The Vault As It Stands*, at the end, lists what is known to differ.

## Purpose

This Obsidian-based system tracks:

- Print history
- Filament inventory and consumption
- Master-spool inventory
- Nozzle wear
- Model usage
- Cost and production metrics

The system is designed as a **fact ledger**. It stores facts and avoids storing derived or
estimated values where possible.

Derived values (remaining filament, nozzle wear, model print time, costs, etc.) should be
calculated from recorded facts.

Print history is no longer entered by hand. The print-tracking feature polls the printer and writes the notes for each finished job. Curated content (Model, Filament, Nozzle) and
the human judgements the printer cannot know (why a print failed, which nozzle is fitted
where) remain hand-authored.

---

# Core Design Principles

## Store Facts, Derive Estimates

Examples:

### Stored

- Print duration
- Which spool was consumed
- Nozzle used
- Which nozzle occupied which slot, and when
- Models printed
- Number of units printed
- Spool weight measurements
- Which master spool is mounted on which spool

### Derived

- Remaining filament
- Cost per model
- Nozzle wear
- Total print time per model
- Estimated print time allocation between models
- Estimated filament remaining
- Stock on hand
- Free master spools

Note that "nozzle used" is stored and "nozzle wear" is derived. The stored fact has to
come from somewhere: see *Nozzle Installation*.

Some stored facts now live in Spoolman rather than the vault. See *Division Of
Responsibility With Spoolman*.

---

## One Shape For Every Print

Every print produces the same three notes:

- One PrintJob note: the run
- One Output note per model printed: what came off the plate
- One Usage note per filament slot consumed: what went in

This includes the trivial single-model, single-filament case, which is more than 99% of
jobs. Uniformity is worth more than note economy: there is exactly one place to look for
any given fact, and no rule that only applies to complex jobs.

This replaces the earlier design, in which a single note carried both `3dprint/printjob`
and `3dprint/usage` tags for simple jobs and was split only when necessary. That form
worked until a job printed two models from one filament, at which point the grams
attributable to each model do not exist in any data the printer or slicer emits and had
to be invented.

---

## Model Reality, Not Perfect Accounting

A print job records what actually happened.

If multiple models are printed together, exact time attribution per model is usually
unavailable and should not be fabricated. The same is true of material: grams are
recorded per filament slot, never per model.

Queries may estimate attribution later.

---

## One Concept Per Field

Fields should represent a single concept.

For example:

- `model` contains model links
- `units` contains quantity
- `nozzle` contains a nozzle identity, never a diameter
- `spool` contains a physical spool, never a filament type
- `status-print` contains an outcome, never a cause

Avoid encoding multiple concepts into a single field.

---

## Facts, Not Fuzzy Matches

Where the system links one note to another, the match is exact or it does not happen.

A plausible but wrong link is unrecoverable noise in a vault of this size, whereas a
missing link is visible work. When the service cannot establish a link it leaves the field
blank and records a *review item* on the note, rather than guessing.

Reporting happens in the note, not only in a log. The vault is where the correction gets
made, so that is where the outstanding work has to be visible. See *Review Items*.

---

## Tags Represent Note Roles

Tags identify entity types.

Examples:

```yaml
tags:
  - 3dprint/model
```

```yaml
tags:
  - 3dprint/nozzle-install
```

Tags are always a YAML list, one entity type per note. Structured attributes belong in
frontmatter fields rather than tags.

---

# Entity Types

## Model

Represents a printable object or part.

Generally:

- One printable model file = one Model note

Models may optionally represent parts belonging to larger assemblies.

Example:

```yaml
tags:
  - 3dprint/model
```

Key fields:

| Field | Meaning |
|---------|---------|
| category | Classification(s) |
| url | Source URL |
| model-folder | Storage location of source files |
| model-files | The model's source filenames, used to match slicer object labels |

`model-files` is the join key between a Model note and the objects a slicer reports.
Without it a model cannot be matched automatically, and the service creates a Model Stub
instead.

The key is named for *model files*, not for STLs: a source file may equally be a `.step`,
`.stp`, `.3mf` or `.obj`, and the slicer reports whichever one was loaded. A label with no
model-file extension is not a filename at all, and therefore not a model.

## Model Stub

A Model note created by the service because a slicer object label named a model file it
could not match to an existing Model.

Tagged `3dprint/model-stub` rather than `3dprint/model`, so that machine-made placeholders
never masquerade as curated content and can be listed for merging by hand.

Merging is by hand, and the accepted path is reactive: rather than authoring `model-files`
across the corpus up front, a stub is merged when it appears. A merge is not finished until
the label's filename is added to the curated note's `model-files` -- without that, the same
stub reappears on the next print of the model.

## Filament

Represents a *type* of filament: a product, not a physical thing.

A filament is never consumed. It is the answer to "what is this made of", and many Spools
may be of one filament.

Example:

```yaml
tags:
  - 3dprint/filament
```

Key fields:

| Field | Meaning |
|---------|---------|
| material | PLA, PETG, TPU, etc. |
| color | Filament color |
| brand | Manufacturer |
| product-line | Product family |
| diameter-mm | Filament diameter |
| density | g/cm³, for length/weight conversion |
| temp-range-°C | Recommended temperatures |
| nominal-weight-grams | Net weight of a full spool of this product |

This word previously meant a physical spool in this vault, which is what made `filament`
on a Usage note ambiguous: the key named a product but pointed at an instance. The term
now matches Spoolman's, where Filament is likewise the product. See ADR 0005.

## Spool

Represents one physical spool of one Filament.

Each spool has its own note, identified by a `spool-id` such as `1a` which is printed on a
physical label stuck to the spool.

A Spool note holds **identity and provenance**. It does not hold live state: how much
remains, where it currently is, and whether it is archived are Spoolman's to know. See
*Division Of Responsibility With Spoolman*.

Example:

```yaml
tags:
  - 3dprint/spool
```

Key fields:

| Field | Meaning |
|---------|---------|
| filament | The product this is a spool of |
| spool-id | Physical identifier, e.g. `1a` |
| purchase-order-code | The ordinal, per filament, of purchases of that filament -- the numeric part of `spool-id`, not an order id; the purchase itself is a Purchase Order note (ADR 0008) |
| spool-sequence | Sequence within that purchase of that filament, the letter part of `spool-id` |
| status | Lifecycle: `unopened`, `active`, `spent`, `off-spool` |
| card-uid | The NFC tag assigned to this spool |
| master-spool | The master spool mounted on this spool; the key is written bare on every spool note, blank when it is on its own hardware |
| spool-type | The form this spool was bought in, present on every spool note: `refill`, a bare core with no hardware of its own, or `spool`, its own hardware |
| gross-weight | A measured total spool weight |
| nominal-weight-grams | Override, when this spool differs from its type's default |
| purchase-date | When it was bought |
| purchase-line | The invoice line this spool was bought on; prices live there, never on the spool |
| retailer | Where it was bought |
| lot-code | Manufacturer lot |

`status` looks like live state but is not. "This spool is spent and went in the bin" is a
physical judgement a human makes; "412 g remain" is a running figure. Spoolman can only
express the second, and its `archived` flag is a boolean, so the four-state lifecycle
has no equivalent there and is not a duplicate of it.

`off-spool` names the loose remainder: filament kept without any spool hardware. Which
spool it came from is still known, so identity and provenance survive the state change.
It is kept because the remainder may be fused onto another spool, and it is the right
material for a very small model. The spool's NFC tag is stored with the loose filament
rather than discarded, so the chip follows the material out of the hardware, and the
`master-spool` mount is cleared with the same edit.

Every Spool note is created at purchase, with its `spool-id` complete, so that the vault
reflects inventory and filenames are stable from creation. The physical spool carries no
id label until first use: the label is printed and stuck when the refill is started, and
a plain refill's id is matched to a physical spool only at that moment -- identical
refills are interchangeable, so whichever refill is started takes a not-yet-started
spool's id, and a mixed order is told apart by its filament. A refill bought bundled
with a master spool is identified from day one, because the master spool's id written on
the shrink-wrap names it, so its `master-spool` field records the factory mount from
purchase. A pack that includes loose master spools names no refill from day one; its
refills are matched to ids at first use like plain refills.

**An NFC tag is never moved to another spool.** The tag is retired with the spool it was
assigned to. A tag may be adhered to a master spool's disk, but a master spool is a
mounting surface and never carries an identity across refills: the tag is discarded when
the refill it was mounted with is spent, and stored with the loose filament when the
contents go off-spool. That is what makes `card-uid` a permanent
identity rather than one more interval to track; tags cost pennies, an interval model
costs a design. The service refuses to resolve a uid claimed by two Spool notes.

## Spool Stub

A Spool note created by the service because a print used a spool the vault has no note
for. Tagged `3dprint/spool-stub`.

Unlike a Nozzle Installation, a stub here is legitimate: Spoolman answers with the spool's
material, colour, brand and diameter, so the note asserts facts rather than existing to
silence a warning.

A stub never points at a stub. If the product matches no Filament note exactly, `filament`
is left bare and a review item raised. The product facts on the stub are what a human needs
in order to decide which Filament it belongs to, and a placeholder pointing at a
placeholder is two guesses deep.

## Master Spool

A reusable pair of plastic disks mounted on a refill spool's cardboard core, providing the
wheels that let it stand and rotate on rollers. Tagged `3dprint/master-spool`, one note per
physical pair, identified by a short label such as `ms1` printed on a sticker -- the
master-spool counterpart of `spool-id`.

A master spool is an owned asset in the Spool's sense, not a Spool: it holds no filament,
is never consumed, and outlives many refills. Its note holds identity and provenance --
brand, the disk pair's weight, and the invoice line it arrived on. Its purchase facts
live on that line, which is why the note carries no `acquired-with`: naming the purchase
is the line link's job, and naming the refill a bundled master spool arrived mounted on
is the spool note's own `master-spool` field, set from creation.

Attachment is not recorded here. It lives on the Spool note, as `master-spool`: cleared
when the refill is spent or its contents go off-spool, and set either at start -- when a refill is mounted onto an
owned master spool, alongside `status: active` -- or at the spool note's creation, for a
refill that arrived bundled with its master spool already mounted while `unopened`. Every Spool note is created at purchase, `spool-id` complete; only the physical label
waits until first use. A bundled refill's physical spool is identified from day one,
because the master spool's id, written by hand on the shrink-wrap, names it, and so the
mount is recorded from day one and the free count is never wrong during the unopened
window. The master spool's own label is printed only at unpacking: a sticker cannot go
through shrink-wrap.

Availability is derived and never stored: a master spool is free when no spool links it,
or only a `spent` or `off-spool` one does. Readiness depends on hardware, and is derived too: a refill
(`spool-type: refill`) is ready to use as-is when its spool is `active` with
`master-spool` set; a spool with its own hardware (`spool-type: spool`) is ready with
the field blank. A
`master-spool` link only ever appears on a refill -- a spool with its own hardware keeps
the field blank for its whole life.

Only current attachment is tracked. There is no mounting history and no reuse count;
nothing yet needs one, and interval records are the Nozzle Installation pattern, a larger
commitment than the questions being asked. See ADR 0007.

Prices live on invoice lines, never on spool or master spool notes (ADR 0008). A line
that brought master spools carries `refill-reference-price-dkk` -- absent when it brought
none, bare when the market price was never observed, filled with the fact -- and the
premium, the line's price minus its refills' reference price, is each master spool's
estimated marginal value in that purchase, computed at query time and never stored. The
value queries clamp at zero: a bulk discount is not a payment to take the master spools,
so a negative premium's surplus goes back to the refills. Every master spool note body
carries an inline query that computes its value from its line; see Purchase Line for the
allocation.

Example:

```yaml
tags:
  - 3dprint/master-spool
```

Key fields:

| Field | Meaning |
|---------|---------|
| brand | Manufacturer of the disk pair |
| weight-grams | Weight of the disk pair, for gross-weight tare |
| purchase-line | The invoice line it arrived on |

## Purchase Order

One real-world order of filament or master spools. Tagged `3dprint/purchase-order`, one
note per order, named `purchase-order_<date>_<slug>` -- identity is the note name, and
no numeric order id exists anywhere (a purchase never had one before; see ADR 0008).

The order holds what an invoice holds at the top. Its lines link to it, and its total is
the sum of its lines' prices plus shipping -- derived, never stored.

Key fields:

| Field | Meaning |
|---------|---------|
| purchase-date | When it was bought |
| retailer | Where it was bought |
| shipping-dkk | Shipping paid, 0 when free |

## Purchase Line

One line of an order's invoice -- the price-bearing unit. What the seller priced is the
line, so prices live on lines and nowhere else. Tagged `3dprint/purchase-line`, named
`purchase-line_<date>_<order-slug>_<line-number>`, the line number taken from the
invoice and sequenced within the order when the invoice does not number its lines.

A line's composition is the backlinks -- the Spool and Master Spool notes whose
`purchase-line` points here -- cross-checked against the line's counts rather than
duplicated by them.

Field presence on line notes is looser than the spool-note doctrine, deliberately: an
absent count means zero, and `refill-reference-price-dkk` is three-valued. That is safe
because line queries are presence filters -- a missing field matching nothing is the
correct result -- whereas the spool-note doctrine guards against negation, which fails
silently. See ADR 0008.

Key fields:

| Field | Meaning |
|---------|---------|
| name | What the line names, e.g. the pack or product |
| purchase-order | The order this line belongs to |
| purchase-date | When it was bought |
| retailer | Where it was bought |
| price-dkk | The line's price, as actually paid |
| filament | The Filament this line bought; absent when the line is not a filament product |
| refill-reference-price-dkk | Plain-refill market price at purchase time; absent when the line brought no master spools, bare when it brought them but the price was never observed, filled with the fact |
| refill-count | Refills on this line; absent when zero |
| spool-count | Spools with their own hardware on this line; absent when zero |
| master-spool-count | Master spools on this line; absent when zero |

Costs are estimates and live at query time, computed from the line by the inline queries
in the spool and master spool note bodies. Let *items* be `refill-count + spool-count`,
*masters* be `master-spool-count`, *ref* be `refill-reference-price-dkk`:

- premium = `price-dkk - items x ref` when ref is present, 0 otherwise
- a master spool's value = `max(0, premium) / masters`, 0 when masters is zero
- a spool item's cost = `(price-dkk - masters x value) / items`

Every branch -- positive premium, premium clamped at zero, reference price unknown with
masters valued at zero -- exhausts the line price: spool costs and master spool values
sum to what was actually paid. The clamp is a choice, not an error: a negative premium
is mathematically consistent, but nobody was paid to take the master spools, so a bulk
discount's surplus is returned to the refills.

## Nozzle

Represents a physical nozzle.

Nozzles may be installed and removed multiple times during their lifetime, and may move
between slots.

Wear is derived by summing grams over the Usage notes that link the nozzle. It is never
stored on the Nozzle note, and installation periods are not used to compute it.

Example:

```yaml
tags:
  - 3dprint/nozzle
```

Key fields:

| Field | Meaning |
|---------|---------|
| diameter-mm | Nozzle diameter, numeric |
| material | Brass, steel, etc. |
| brand | Manufacturer |
| purchase-date | Purchase date |
| retailer | Where it was bought |

## Nozzle Installation

Records that one physical nozzle occupied one slot of one printer over one interval.

This is the only source of nozzle identity. The print data names a filament slot and a
nozzle diameter, never a nozzle, so without an installation record a Usage note cannot say
which nozzle wore.

It is written by hand, and it must exist before the print it applies to. The service reads
it and never writes one: unlike a Model Stub, there is no fact available from which to
derive one.

Example:

```yaml
tags:
  - 3dprint/nozzle-install
nozzle: "[[nozzle_0.4_steel_snapmaker-stock_2]]"
printer: "printer-snapmaker-u1"
slot: 2
installed: 2026-06-22
removed:
```

Key fields:

| Field | Meaning |
|---------|---------|
| nozzle | The physical nozzle |
| printer | Which printer's slot this is |
| slot | Slot number |
| installed | When it went in |
| removed | When it came out; blank means still installed |

Rules:

- `installed` and `removed` are a bare date, or a date with a time of day
  (`2026-06-22 14:30`). There is no UTC offset; values are local.
- The end of an installation is recorded explicitly. It is deliberately **not** implied by
  the start of the next installation in the same slot, because an implied end makes a
  slot's last known nozzle cover all future time, and an unrecorded swap would then be
  attributed silently to the wrong nozzle.
- Being uninstalled is the absence of a record, not a record. A spare nozzle has no
  installation note, and nothing ever states that a slot is empty.
- Contradictory records, such as two installations covering one slot at one moment, are
  refused rather than resolved.

The printer's own interface calls a slot a *tool*, and older nozzle notes say
`Installed in tool 2` in prose. It is the same numbered position. `slot` is the canonical
term.

## Printer

Represents a physical printer.

The `printer` field on other notes is a bare string, not a link, so a Printer note is
reference material rather than a join target.

The print-tracking feature supports the Snapmaker U1 only, so the value the service writes
is fixed -- `printer-snapmaker-u1` -- rather than read from the job. Legacy notes carry
`ender-3-max` for the Ender; those values are correct and are never rewritten, the service
being a create-only writer that never revisits an existing note.

Example:

```yaml
tags:
  - 3dprint/printer
```

## PrintJob

Represents a printing event: the run, and nothing else.

Stores:

- When a print occurred
- Which printer performed it
- Duration
- The printer-reported outcome
- Which printer job it came from

It carries no model, units, filament, or grams. Those belong to Output and Usage.

Example:

```yaml
tags:
  - 3dprint/printjob
```

Key fields:

| Field | Meaning |
|---------|---------|
| printer | Printer used |
| date | Print date |
| print-duration-secs | Time spent printing |
| total-duration-secs | Wall-clock time including heat-up |
| status-print | Outcome: `success`, `cancelled` or `failed` |
| printer-job-id | The printer's own job identifier |
| gcode-file | The gcode filename, verbatim |

`printer-job-id` is what lets the service tell an already-recorded run from a new one, and
what lets a human delete a note to have it rebuilt.

## Output

Represents what a print run produced: one model, and how many of it.

One Output note per distinct (PrintJob, Model) pair.

An Output is an event record, not an inventory item. It states what a past run produced
and is never decremented when a part is used, gifted or discarded. Stock on hand is
deliberately not an Output.

Outputs are written for every terminal outcome, not only successes.

Example:

```yaml
tags:
  - 3dprint/output
```

Key fields:

| Field | Meaning |
|---------|---------|
| print-job | Associated PrintJob |
| model | Printed model |
| units | Quantity produced |
| status-print | Outcome of the run that produced it |

## Usage

Represents material consumption: one filament slot, and what went through it.

One Usage note per distinct (PrintJob, slot) pair.

Usage deliberately does **not** reference a Model. When a print job produces two models
from one filament, the grams attributable to each are not knowable, and the system records
only facts.

Example:

```yaml
tags:
  - 3dprint/usage
```

Key fields:

| Field | Meaning |
|---------|---------|
| print-job | Associated PrintJob |
| slot | Filament slot consumed |
| extruded-grams | Material consumed |
| spool | Physical spool consumed; blank when unresolved |
| nozzle | Nozzle that wore; blank when unresolved |

`spool` is resolved through Spoolman, not from the print data. The printer's per-slot
`CARD_UID` is never persisted anywhere, so an after-the-fact poll cannot see it; what *is*
durable is the list of Spoolman spool ids the job used, carried in the job history. See
*Spool Resolution* and ADR 0006.

`nozzle` is resolved from Nozzle Installation records against the job's start time. Three
outcomes:

| Result | When | Recorded as |
|---|---|---|
| Resolved | Exactly one installation covers the run | the link |
| Assumed | The installation begins on the run's date with no time of day recorded | the link, plus `has-assumptions` |
| Refused | No installation covers it, or more than one does | blank, plus a review item naming the conflicting records |

Whole-job durations stay on the PrintJob note and are never copied onto Usage. A duration
on a Usage note would be wrong the moment anyone summed it.

---

# Division Of Responsibility With Spoolman

Spoolman is introduced alongside this service and holds some of the same data. The line
between them is drawn by the *nature of the fact*, not by convenience:

| | Owner |
|---|---|
| What a spool is, when it was bought, from whom, at what price, its lot | **Vault** |
| Which NFC tag is assigned to it | **Vault** |
| Lifecycle judgement: unopened / active / spent | **Vault** |
| Which master spool is mounted on a spool | **Vault** |
| How much remains, where it is, whether it is archived | **Spoolman** |
| Per-job consumption history | **Vault** |

That last row is not a mistake. Spoolman stores a single running `used_weight` per spool
and no history at all -- its documentation delegates history to Prometheus. The vault's
Usage notes are therefore the only durable per-job consumption record in the system.

Identification runs one way only. `spool-id` stays the single identifier; Spoolman carries
it in a custom field so the vault can be looked up from a Spoolman spool. The vault stores
no Spoolman id, because that integer is Spoolman's internal key and would rot silently if
an entry were ever deleted and recreated. Keeping the uids in step between the two systems
is a separate utility's job, not this service's.

Two practical notes on Spoolman custom fields: keys must match `^[a-z0-9_]+$`, so the key
is `spool_id` and not `spool-id`; and every extra-field value is JSON-encoded on the wire
regardless of its declared type, so a label code of `1a` reads back as `"\"1a\""`.

---

# Spool Resolution

The printer cannot tell you which spool was used, even though it reads the tag. Its
`CARD_UID` exists only in a live status object -- never written to disk, never logged,
never recorded in the job history or database -- so a service that polls after a print has
finished will never see it. ADR 0006 records the alternatives and why they were rejected.

What is durable is this chain: the Extended Firmware reads the tag, resolves the uid
against Spoolman, and sets the active spool through Moonraker, which records it as an
auxiliary field on the job. So the job history carries, per print, the deduplicated list
of Spoolman spool ids that print used. Resolution is then:

1. Read the spool id list from the job's auxiliary data.
2. Fetch each spool from Spoolman.
3. Read the vault's own `spool-id` back out of the Spoolman custom field.
4. Link that Spool note.

**The list carries no slot mapping.** It is a set in resolution order, not an array
indexed by slot. For a single-filament job -- more than 99% of prints -- there is one spool
and one slot, and the attribution is unambiguous. For a multi-filament job the pairing is
unknowable, and matching by position would be a guess presented as a fact. Those jobs get
a bare `spool` key and a review item listing the candidates.

A cross-check comes free: the job history already carries `filament_type`, `filament_name`
and `filament_colour` per slot, taken from the gcode profile. If Spoolman names a material
the profile contradicts, the link is still written and a review item raised -- see
*Review Items*.

Preconditions, all four required: Extended Firmware installed, `[spoolman]` configured in
Moonraker, NFC tags on the spools, and the vault's label code present in a Spoolman custom
field. With any of them missing, `spool` is simply left bare and **no** review item is
raised, because that state is "not attempted" rather than "could not be worked out".

The service only ever reads from Spoolman. Moonraker's own `spoolman` component already
reports consumption during prints; if this service also reported usage, every gram would
be counted twice.

---

# Quantities And Multiple Models

A print run that produces several models produces several Output notes, one per model,
each with its own `units`.

```yaml
tags:
  - 3dprint/output
print-job: "[[printjob_2026-07-07_opengrid-8x8-base_000042]]"
model: "[[model_part-a]]"
units: 10
```

Quantity belongs to the Output record, because a count of copies is a fact about what was
produced. It is a counted fact rather than an estimate, taken from the slicer's per-copy
instance definitions, which requires object-exclusion output to be enabled in every print
profile. That setting is a standing requirement of the workflow.

A Usage note never carries `units` or `model`.

---

# Naming Conventions

## Print Jobs

```text
printjob_<date>_<model-stem>_<printer-job-id>
```

Examples:

```text
printjob_2026-09-05_gridfinity-ext-cup-1x1_000050
printjob_2026-09-05_gridfinity-ext-cup-1x1_00004F
```

The printer's job identifier is always present, never appended only on collision. Two runs
of the same model on the same day are ordinary, so a name built from model and date is not
unique. `<model-stem>` is derived from the gcode filename by stripping the slicer's
profile, material, duration and copy-count tokens.

The same stem is shared by all three notes a run produces, so they sort together.

## Usage

```text
usage_<date>_<model-stem>_<printer-job-id>_slot<N>
```

Examples:

```text
usage_2026-09-12_wedge-4x10x40_000051_slot2
```

## Outputs

```text
output_<date>_<model-stem>_<printer-job-id>_<model-slug>
```

## Models

```text
model_<name>
```

## Filaments

```text
filament_<color>_<material>_<brand>_<product-line?>
```

Examples:

```text
filament_white_pla_snapmaker_snapspeed
filament_black_petg_azurefilm
```

No `spool-id`: a Filament is a product, and the id belongs to an instance of it.

## Spools

```text
spool_<status?><spool-id>_<color>_<material>_<brand>
```

Examples:

```text
spool_1a_black_petg_azurefilm
spool_(spent)_1a_green_pla_smartfil
```

The `spool-id` comes first because it is the only part printed on the physical label, so
typing `spool_1a` in the quick switcher finds the spool in your hand, and spools sort by
purchase order.

Status prefixes intentionally reduce accidental selection of inactive spools.

## Master Spools

```text
master-spool_<label>
```

Examples:

```text
master-spool_ms1
master-spool_ms7
```

The label is printed on a sticker on the disk pair and is the master-spool counterpart of
`spool-id`.

## Purchase Orders

```text
purchase-order_<date>_<slug>
```

Examples:

```text
purchase-order_2026-10-05_sunlu-haul
purchase-order_2026-10-12_3djake-restock
```

The slug is a human-chosen descriptor of the purchase, present in every name -- never
appended only on collision, for the printjob reason: a name must not depend on what was
named before it. The note name is the order's identity; there is no numeric order id
anywhere in the vault.

## Purchase Lines

```text
purchase-line_<date>_<order-slug>_<line-number>
```

Examples:

```text
purchase-line_2026-10-05_sunlu-haul_1
purchase-line_2026-10-05_sunlu-haul_2
```

The line number is the invoice's own, the way the printjob slug carries the printer job
id; when the invoice does not number its lines, the sequence within the order as entered
is used. The date and order slug tie each line to its Purchase Order note at a glance.

## Nozzles

```text
nozzle_<diameter>_<material>_<brand>_<instance>
```

Examples:

```text
nozzle_0.4_brass_evatmaster_2
```

## Nozzle Installations

```text
nozzle-install_<installed-date>_slot<N>_<nozzle-stem>
```

Examples:

```text
nozzle-install_2026-06-22_slot2_0.4-steel-snapmaker-stock-2
```

The nozzle stem is included because a slot can be changed twice in one day once a time of
day is recorded, and because it makes the note self-describing in the quick switcher.

---

# Formatting Conventions

The service writes frontmatter with a deliberate serialiser rather than a general YAML
library, so that key order is fixed and diffs stay readable.

- `tags` is a YAML list.
- `date` is unquoted.
- String values are quoted.
- Wikilinks are quoted, with double quotes.
- A known-but-empty value is a bare key with nothing after the colon.
- Dates derive from the printer's reported start time, in `Europe/Copenhagen`.
- Note bodies are never touched. Hand-written prose below the frontmatter survives
  migration and is never overwritten.

The service creates notes and never modifies one that already exists. A note's existence is
the record that its source event was processed. Regenerating a note, after a parser fix or
a late nozzle record, is done by deleting it and letting the service write it again.

---

# Spool Identification System

Each spool receives a physical identifier combining a per-filament purchase ordinal and a
sequence within that purchase:

```text
1a
1b
2a
2b
...
```

Where:

- Number = the ordinal, per filament, of purchases of that filament -- not an order id.
  One physical order of two filaments produces two numbers, and the purchase itself is
  identified by its Purchase Order note (ADR 0008)
- Letter = sequence within that purchase of that filament

Stored as:

```yaml
spool-id: 1a
purchase-order-code: 1
spool-sequence: a
```

These identifiers:

- Appear in filenames
- Appear on physical labels
- Are carried in a Spoolman custom field, so a Spoolman spool resolves to a vault note
- Minimize selection mistakes during logging

A spool also carries an NFC tag, whose uid is recorded as `card-uid`. The tag is never
moved to another spool, so the uid is a permanent second identifier rather than an
interval. When the spool sits on a master spool the tag is adhered to the master spool's
disk, but it is assigned to the refill and discarded with it; the master spool is a
mounting surface, never an identity. The `spool-id` remains the primary key: it is
human-readable, it survives a Spoolman database loss, and it is what is physically
written on the label.

---

# Data Quality And Assumptions

Migration-era and future estimated values may be tracked using:

```yaml
has-assumptions: true

assumptions:
  - field: extruded-grams
    method: estimated_from_duration
    confidence: low
```

```yaml
has-assumptions: true

assumptions:
  - field: nozzle
    method: same_day_nozzle_change
    confidence: low
```

This allows:

- Auditing estimates
- Replacing estimates later
- Querying notes that contain inferred data

An `assumptions` entry means something was inferred. It is not used to record that
something is *missing*: an unresolved field is simply blank, so that a query for notes
containing inferred data does not return notes containing absent data. For that, see
*Review Items*.

---

# Review Items

When the service cannot work something out, or works it out and has reason to doubt it, it
says so **in the note**:

```yaml
spool:
needs-review: true
review:
  - field: spool
    reason: multi_spool_job_no_slot_mapping
    candidates:
      - "[[spool_1a_black_petg_azurefilm]]"
      - "[[spool_2c_grey_pla_snapmaker]]"
```

`reason` comes from a controlled vocabulary, the same discipline `assumptions` applies to
`method`, so the values stay queryable. `candidates` is present only when there are any.

A review item is **not** an assumption. An `assumptions` entry says *we inferred this and
it may be wrong*. A review item says *we know we do not know* -- or *we do know and we
doubt it*. The second case is why a review item may sit on a note whose field is
**populated**: a cross-check that disagrees, such as Spoolman naming one material where
the gcode profile names another, is the only signal that an otherwise plausible link is
silently wrong. That is the failure this system most wants to catch, so the link stays
written and the doubt is recorded beside it.

Review items live in the note, not only in a log, because the vault is where the
correction gets made. A single query over `needs-review` is the entire backlog. A warning
in a log nobody reads is not a work item at all.

Clearing one is a human act: fill in the field, delete the block. The service never
revisits a note it has written, so it never clears its own review items.

---

# Migration Provenance

Notes that a migration pass has touched carry an append-only `migrated` list, one entry
per pass:

```yaml
migrated:
  - by: M3-v1
    on: 2026-04-04
  - by: print-watcher-migration
    on: 2026-10-14
```

`by` names the migration, `on` the date that pass ran. The first entry is the
Notion-to-vault migration (M3-v1), converted from the legacy flat `migrated_on` /
`migrated_by` pair it wrote on 796 notes -- a record kept deliberately, because it is
the only marker of which content predates the vault.

The print-watcher migration appends its entry to every note it writes or rewrites,
generated notes included: a rewritten printjob, an extracted output, a generated
filament product note all carry the same stamp. Entries are never rewritten or removed;
a partial re-run months later leaves visible strata of which notes were converted when.

The service itself writes no `migrated` entry: a service-written note's provenance is
its `printer-job-id`, and the service is the single writer of the shape it produces.

---

# Divergences From The Vault As It Stands

The counts below come from an automated survey of the vault on 2026-09-14, and have not
been re-verified since. Treat them as indicative. Each item is a place where the vault
contradicts this document, or where this document is silent about something in wide use.

## Expected, resolved by migration

These are all consequences of the three-note split, and the migration tool converts them.

| Divergence | Scale |
|---|---|
| Notes carrying both `3dprint/printjob` and `3dprint/usage` | ~609 printjob notes; `3dprint/usage` appears on ~672 notes total |
| `model` and `units` on Usage rather than Output | `model` on ~655 notes, `units` on ~656 |
| `duration-hours` instead of `print-duration-secs` / `total-duration-secs` | ~584 notes |
| Legacy `usage_<date>_<model>_<filament>` names | 97 standalone Usage notes |
| No `printer-job-id` or `gcode-file` anywhere | all pre-service notes |
| `3dprint/filament` tagging physical spools rather than types | ~97 notes become `3dprint/spool`; Filament notes are generated by grouping them |
| `filament:` on Usage pointing at a spool | ~669 notes; the key becomes `spool:` |
| Product attributes duplicated across every spool of one product | e.g. `temp-range-°C` on ~42 notes, deduplicated onto Filament |
| No `card-uid` anywhere | no tags applied yet; left blank |
| No `master-spool`, `spool-type` fields or `3dprint/master-spool` notes | new entity (ADR 0007); the migration writes `master-spool` bare on every spool note and fills `spool-type` where the purchase form is known, and master-spool notes are created by hand as disk pairs are labelled |
| No `3dprint/purchase-order` or `3dprint/purchase-line` notes, and `purchase-price-dkk` on spool notes | new entities (ADR 0008); backfill groups existing notes into orders by purchase date plus retailer as a proposal for human confirmation, moves prices onto line notes, and writes bare `refill-reference-price-dkk` where the plain-refill market price was never observed |
| Untagged Model notes | 363 `model_*` files, only 95 tagged -- 268 have no `tags` key at all, so queries over the tag silently miss most of the corpus. The migration tags every `model_*` note `3dprint/model`, name being the signal per the vault's own naming convention. The `models_no-time` view in `bases/nozzles.base` empties when this lands. |
| Legacy capitalised keys | `Date bought` (~34), `Material` / `Diameter` (~33), `Distributor` (~30): duplicates of documented lower-case keys, every one of them with its lower-case counterpart already present on the same note (verified 2026-10-09; paired values agree, `Material` and `Distributor` differing only in letter case). The migration drops the capitalised duplicate, renaming only where no counterpart exists -- one note, `printer_snapmaker-u1.md`'s `Date bought` to `purchase-date`. `Last used` / `Hours used` / `Filament extruded` / `3D Prints` (~12 each) are Notion-computed historical records (see the derived-values row), kept deliberately rather than cleaned up. |

## Unresolved: this document was wrong

| Divergence | Detail |
|---|---|
| Spool identifier format | This document previously described identifiers as `A1, B2` with letter-as-order. The vault has always used `1a`, number-as-order. This document has been corrected to match the vault. |
| `retailer` undocumented | In use on ~131 notes across Filament and Nozzle. Now documented. |
| `lot-code` undocumented | ~59 Filament notes. Now documented. |
| Model `name` field | This document listed a `name` key for Model notes. No Model note appears to use one; the name lives in the filename. Removed. |
| Printer as an entity type | Two `printer_*` notes exist and 609 notes reference `printer`, but this document listed no Printer entity. Now documented, minimally. |
| `printer` value has two forms | This document said one of the two forms was wrong. Neither is: legacy notes carry `ender-3-max` for the Ender, and the service writes `printer-snapmaker-u1`. The feature supports the Snapmaker U1 only, so the value the service writes is fixed rather than read from the job, and legacy values are correct and are never rewritten. |

## Unresolved: needs a decision or a clean-up

| Divergence | Detail |
|---|---|
| **The model join key does not exist** | Verified 2026-10-09: none of the 363 Model notes carries `model-files` or the older `stl-files`, so every Output generates a Model Stub until a merge adds the filename to a curated Model note. The accepted path is reactive: stubs are the work queue and merges are by hand, each merge adding the label's filename to the curated note's `model-files` so the stub cannot reappear. No migration rename step is needed (issue 02 of the print-watcher service). |
| `status-print` is not a controlled set | Present on 210 of ~609 printjob notes: 11 distinct values (verified 2026-10-09) that mix outcome and cause, including both `bed-adhesion` and `failed_bed-adhesion`. This document now specifies `success`, `cancelled`, `failed`; a cause belongs in a human-written `status-note`. The migration tool requires an operator-authored map from old values to new; a complete draft from the census sits in the print-watcher repo's migration feature directory. |
| Derived values stored on Nozzle notes | ~12 Nozzle notes carry `Hours used`, `Filament extruded`, `Last used` and `3D Prints`. They were computed by Notion, where these notes were hosted before the vault, and are kept deliberately rather than retired: they are the only record of the pre-service era, and `Filament extruded` is already in grams, the unit ADR 0003 made canonical, so it carries the wear metric's history across the format change. Comparing `Hours used` against the base's duration-derived figure is the detector for missing `duration-hours` (25 printjobs lack it). `3D Prints`, a Notion relation list, is redundant with Obsidian backlinks. |
| Migrated `extruded-grams` contain export errors | At least one printjob (`printjob_2021-12-25_First print`) carries `107` where the print was 10.7 g -- a decimal lost in the Notion-to-vault migration. The values are corrected at source against a fresh Notion export (issue 02 of the print-watcher-migration feature), because the migration tool copies `extruded-grams` into the extracted Usage notes unchanged and nozzle wear is the sum of grams over Usage notes. |
| `nozzles.base` computes wear in hours | Its `hours_used` formula sums `duration-hours` over backlinked printjobs. A job spreads across up to four nozzles and the printer reports one duration, so this over-counts once per slot. Wear is grams. The formula is the mechanism this document's Nozzle section replaced and should be retired. |
| `printer_ender-3-max.md` is empty | Zero bytes, while ~553 printjobs reference that printer. |
| `date` is sometimes a string | Newer notes quote it (`date: "2026-08-29"`), legacy notes do not. This document says unquoted. |
| Two wikilink quoting styles | Legacy notes use single quotes, newer ones double. This document says double. |
| `model` is sometimes a list, sometimes a scalar | Legacy notes use a YAML list even for one model. Output notes take a single link. |
| `status` on fewer than half of spools | 53 of 99 spool notes carry no `status` key and 2 more carry it bare; the values in use also included a fourth state, `off-spool`, now part of the controlled set (`unopened` / `active` / `spent` / `off-spool`), owned by the vault. The filename prefix is kept deliberately as a selection guard rather than treated as duplication. The migration converts legacy `open` to `active` and passes `unopened`, `spent` and `off-spool` through; filling the blanks is a human task, listed by the `Spools-status-empty` view in `bases/nozzles.base` (tracked as issue 01 of the print-watcher-migration feature). Three notes are marked `off-spool` today and the true count is significantly higher; the physical review joins the same issue. |
| Time-varying facts recorded as body prose | `[[2026-06-22]] Installed in tool 2` on Nozzle notes, `[[2025-12-13]] Dried 5 hours at 65°C` on Filament notes. This is the vault's de-facto mechanism for anything that changes over time and this document has never described it. Nozzle Installation formalises one case of it; drying and maintenance remain prose. |
| Dataview queries embedded in note bodies | Several spool notes carry aggregation queries inline. A useful convention, undocumented, and duplicated by hand per note. **These query `#3dprint/usage ... WHERE filament = this.file.link` and will silently return nothing once the key becomes `spool`** -- auditing them is a named migration step, per ADR 0005. |
| Undocumented entity types | `3dprint/build` and `3dprint/assembly`, one note each (`build_2026-07-18_soap-dispenser`, `assembly_soap-dispenser`), both with inline rather than list-form tags, the assembly note carrying a nested `bom:` list. Decided 2026-10-09: these are experiments, out of scope for this project. They stay as they are, undocumented on purpose and untouched by the migration, and are unrelated to the glossary's *Assembly*, which names a slicer-side object grouping. |
| Notes with no frontmatter at all | `alumina_log.md` is plain text with dated rows and no tag, so it is invisible to every query. Decided 2026-10-09: accepted as a plain log -- it stays as it is, out of scope, and no frontmatter or tag is added. |
| `temp-range-°C` | ~42 notes carry a key with a non-ASCII character. Now documented on Filament, where the split also reduces it from ~42 copies to one per product, but the non-ASCII key name remains a hazard for any tooling that assumes ASCII. |
| `migrated_on` / `migrated_by` | 796 notes, the most widely used keys in the vault, and undocumented. Now documented: the pair is the Notion-to-vault migration's record, and the print-watcher migration converts it into the first entry of the append-only `migrated` list (*Migration Provenance*), appending its own entry to every note it touches, generated notes included. |
| One malformed Nozzle note | `nozzle_0.4_brass_evatmaster 1.md` uses a space where its four siblings use an underscore, and carries `diameter-mm: 0.1` despite being named `0.4`. |

---

# Future Integration Ideas

## NFC-Tagged Spools

No longer speculative -- this is the mechanism *Spool Resolution* depends on. The
workflow:

- NFC tags, one per spool, never re-used; adhered to the master spool's disk when the
  spool sits on one, but assigned to the spool -- stored with the loose filament when the
  contents go off-spool, discarded when the spool is spent
- Snapmaker U1 Extended Firmware reads the tag and resolves it against Spoolman
- Moonraker records the resolved spool on the job
- The service reads it back from the job history

The spool identifier (`spool-id`) remains the primary key linking the physical spool, its
NFC tag, its Spoolman entry and its vault note.

Remaining genuinely future work:

- **Slot-accurate spool attribution.** The only route is a Moonraker component that
  registers a per-slot history field fed from `filament_detect`. That is the sanctioned
  mechanism and the gap the Extended Firmware leaves open, but it means deploying Python
  into the printer's firmware. Until then, multi-filament jobs raise a review item.
- **Writing richer tag payloads.** An OpenSpool-format NDEF payload would also give the
  printer correct temperatures for third-party filament. Worth doing for the printer's
  sake; the note design deliberately does not depend on it, because payload formats will
  churn and the uid will not.
- **A vault/Spoolman sync utility**, keeping `card-uid` and `spool-id` in step in both
  directions. Out of this service's scope: it only ever reads.
- OctoPrint, automatic inventory updates.

---

# Philosophy

This system favors:

- Simplicity
- Accuracy
- Low-friction data entry
- Future-proof aggregation

The system records facts as they become known and postpones estimation and analysis until
query time.

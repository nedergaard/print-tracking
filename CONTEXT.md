# Context

Glossary for the print-tracking feature and the 3D-printing notes it writes into the Obsidian vault (`nedergaard/leandervault`, notes under `para.resources/3d/printer/`).

## Printjob

A single print run on the printer. One printjob per plate started. Identified in the vault as `printjob_<date>_<slug>` and tagged `3dprint/printjob`.

A printjob is an event: it happened at a time, on a printer, for a duration. It is not a part and not a material record.

## Output

A record that a printjob produced copies of one [[Model]]. One output per distinct (printjob x model) pair, carrying a unit count.

An output is an **event record**, not an inventory item. It states what a past print run produced; it is never decremented when a part is used, gifted, or discarded. It is immutable and fully regenerable from the printjob plus its gcode, which is what makes the service idempotent and a mis-parse fixable by deleting and re-running.

Outputs answer: "how many of model X have I printed, and when?"

Outputs are written for every terminal printjob state, not only successes; the state is recorded on the output.

Stock on hand ("what is in the drawer right now") is deliberately *not* an output. If tracked later it is a separate, aggregating note type.

## Usage

A record of material consumed by a printjob: one usage note per (printjob x [[Slot]]), carrying grams extruded, the [[Spool]] it came off and the nozzle it passed through.

Usage is material accounting. It is *not* a record of what was produced -- that is an [[Output]]. Usage deliberately does **not** reference a Model: when a printjob prints two models from one filament, the grams attributable to each model are not knowable, and the vault's rule is to record only facts, leaving estimation to query time.

## Model

A curated note about a printable design: name, category, source URL, local folder. Tagged `3dprint/model`.

A model is curated content, authored by a human. It is not derived from a filename.

## Skinny printjob

After the split a Printjob note records only the run: printer, date, duration, and the printer-reported terminal state. It carries no model, units, filament, or grams -- those belong to [[Output]] and [[Usage]]. Every print gets the same three-note shape, including the trivial single-model single-filament case, so that there is exactly one place to look for any given fact.

## Model stub

A Model note created by the service because gcode named a model file it could not match to an existing Model. A model file is any of the extensions the slicer may load -- `.stl`, `.step`, `.stp`, `.3mf`, `.obj` -- not an STL specifically; see [[Object label]].

Tagged distinctly from a curated Model so that machine-made placeholders never masquerade as curated content and can be listed for merging.

Matching is exact, never fuzzy: a wrong model link is unrecoverable noise in a 366-model vault, whereas an unmatched stub is visible work.

## Create-only writer

The service creates notes and never modifies one that already exists. A note's existence is the record that its source event was processed. Regenerating a note -- after a parser fix, say -- is done by deleting it and letting the service write it again.

This is what keeps [[Output]] an immutable event record while still leaving room for hand-added keys: the service simply never comes back.

## Assembly

A slicer-side grouping of several objects. When objects are grouped, the gcode reports the group's name in place of the source filenames, so model identity is lost.

An assembly is therefore *not* a [[Model]] and never becomes a [[Model stub]]. The service refuses to derive a model from a name that is not a filename, and surfaces the printjob for manual handling instead. Not grouping objects in the slicer is a standing workflow rule.

## Unattributed material

The print data attributes material to a [[Slot]] -- index, colour, type, grams -- and to nothing more physical than that. It gives a nozzle diameter but no nozzle identity, and a slot index but no spool identity.

Both gaps are now closed from outside the print data rather than by guessing at it. A [[Nozzle installation]] supplies the nozzle; Spoolman, reached through the printer's job history, supplies the [[Spool]]. Neither fact is ever inferred from the print data, which is why a [[Usage]] note leaves the key bare when no record answers for it.

What remains genuinely unattributable is the *pairing* on a multi-slot job: the history names the spools a run used without saying which slot held which. That is recorded as a [[Review item]], never resolved by position.

## Filament

A *type* of filament: a product, identified by brand, material, colour and product line, with the properties that follow from being that product -- diameter, density, temperatures. Tagged `3dprint/filament`.

A filament is not a physical thing and is never consumed. It is the answer to "what is this made of", and many [[Spool]]s may be of one filament.

The word previously meant a physical spool in this vault, which is what made `filament` on a [[Usage]] note ambiguous: the key named a product but pointed at an instance. The term now matches Spoolman's, where Filament is likewise the product and Spool the instance, so that one vocabulary spans both systems.

## Spool

One physical spool of one [[Filament]]. Tagged `3dprint/spool`, and identified by a `spool-id` such as `1a` -- a purchase order and a sequence within it -- which is printed on a physical label stuck to the spool.

A spool note holds identity and provenance: what it is, when it was bought, from whom, at what price, its manufacturer lot, and the NFC tag stuck to it. It does **not** hold live state. How much remains, where it currently is, and whether it is archived are Spoolman's to know, because Spoolman is the thing being told about every gram as it is extruded.

The one apparent exception is lifecycle -- `unopened`, `active`, `spent` -- which stays in the vault because it is a physical judgement a human makes, not a running figure. "This spool is spent and went in the bin" and "412 g remain" are two different facts, and Spoolman can only express the second.

A spool's NFC tag is never moved to another spool. The tag is retired with the spool it was stuck to, which is what makes its uid a permanent identity rather than one more interval to track. Tags cost pennies; an interval model costs a design.

## Spool stub

A [[Spool]] note created by the service because a print used a spool the vault has no note for. Tagged distinctly, like a [[Model stub]], so machine-made placeholders never masquerade as curated content.

Unlike a [[Nozzle installation]], a stub here is legitimate: Spoolman answers with the spool's material, colour, brand and diameter, so the note asserts facts rather than existing to silence a warning.

A stub never points at a stub. If the spool's product matches no [[Filament]] note exactly, `filament` is left bare and a [[Review item]] raised -- the product facts sitting on the stub are what a human needs to decide which filament it belongs to, and a placeholder pointing at a placeholder is two guesses deep.

## Print outcome

The outcome of a printjob, drawn from a controlled set: success, cancelled, failed.

Outcome is distinct from *cause*. A cause of failure ("bed adhesion", "layer shift") is recorded separately and is only ever written by a human; the service knows outcomes, not reasons. Historically the two were conflated into a single free-text field, which is why the vault holds both `bed-adhesion` and `failed_bed-adhesion` as if they were outcomes.

## Printer job

The printer's own record of a print run, with its own sequential identifier. A [[Printjob]] note names the printer job it was derived from, which is what lets the service tell an already-recorded run from a new one -- and lets a human delete a note to have it rebuilt.

## Printjob slug

The name shared by the three notes a print run produces, of the form `<date>_<model-stem>_<job-id>` behind a note-type prefix: `printjob_2026-09-05_gridfinity-ext-cup-1x1_000050`, with `usage_...` and `output_...` built on the same stem so that a run's notes sort together.

A slug is a pure function of the [[Printer job]] alone, and the printer job's identifier is always part of it -- never appended only on collision. Two runs of the same model on the same day are ordinary, and the vault already holds pairs that differ by nothing but the slicer's duration suffix, so a name built from model and date is not unique. Appending the id unconditionally means no collision-handling path that fires only on rare inputs, and no run whose name depends on what was printed before it.

A slug is not the gcode filename. The filename is recorded verbatim on the [[Printjob]]; the slug is derived from it by stripping the slicer's `<profile>_<material>_<duration>` suffix and a trailing copy count. Those tokens are dropped because the facts they carry are recorded as keys elsewhere -- duration on the [[Printjob]], copies as units on the [[Output]] (see [[Instance]]) -- and a filename is the wrong place to keep a fact twice. A gcode name that is not slicer-shaped is kept whole rather than guessed at; the appended id keeps it unique regardless, which is what makes that fallback safe.

## Slot

A filament position on the printer, numbered. The printer has four, each with its own nozzle, so a slot identifies both the material path and the nozzle that wore.

Print data attributes material to slots, never to spools or nozzles. A slot is therefore the most precise thing the printer itself reports, and both [[Spool]] and nozzle identity have to be resolved against records kept outside the print data. See [[Unattributed material]].

The printer's own vocabulary, and the vault's older nozzle prose (`Installed in tool 2`), call this a *tool*. It is the same numbered position; `slot` is the canonical term.

## Instance

One physical copy of an object on the plate. The slicer emits one instance definition per copy, which is what makes the `units` on an [[Output]] a counted fact rather than an estimate -- but only when object-exclusion output is enabled in the slicer profile. That setting is a standing requirement of the workflow, not an optional nicety.

## Object label

The name the slicer gives an object on the plate. It is the only link back to a [[Model]], and it is not a clean filename:

- The slicer appends a numeric disambiguator *after* the extension when two objects share a name, so two labels may denote one model. A trailing number is stripped only when what remains still ends in a model-file extension, so that a model genuinely named with a trailing number survives intact.
- A label with no model-file extension is not a filename and therefore not a model -- a CAD primitive or an [[Assembly]]. Having an extension is the signal the service keys on; no output is written for a label that lacks one.

## Nozzle installation

A human-written record that one physical nozzle occupied one [[Slot]] of one printer over one interval. One note per installation, tagged `3dprint/nozzle-install` and named `nozzle-install_<installed-date>_slot<N>_<nozzle-stem>`.

An installation is an interval, not an event: it carries an `installed` and a `removed` bound, and a blank `removed` means the nozzle is still in. Time of day is optional and local; a bare date means midnight. The end bound is recorded explicitly rather than implied by the next installation in the same slot, because an implied end makes a slot's last known nozzle cover all future time -- an unrecorded swap would then be attributed silently to the wrong nozzle, whereas an explicit end leaves a gap that can be seen and reported.

An installation is the only source of nozzle identity: the print data names a slot and a diameter, never a nozzle. It is never written by the service -- unlike a [[Model stub]], there is no fact available from which to derive one, so a missing installation is reported rather than papered over.

An installation is not a wear record. Wear remains a sum of grams over the [[Usage]] notes that link a nozzle; see [[Nozzle wear]]. Nothing stores a wear total, and an installation's interval is never used to compute one.

Contradictory records -- two installations covering one slot at one moment, or one nozzle in two slots at once -- are refused rather than resolved, on the same grounds as exact [[Model]] matching: a plausible wrong link is worse than a visible hole. The refusal is recorded on the [[Usage]] note as a [[Review item]] naming the records that conflict, so that the hole is visible where the work happens.

## Nozzle wear

Wear is measured in grams extruded through a nozzle, not hours run.

Per-nozzle *time* is not available: the printer reports one duration for the whole job, and a job spreads across up to four nozzles, so attributing that duration to a nozzle would over-count it once per slot. Grams per slot is a recorded fact and excludes travel, heating and idle time, during which no material passes through the nozzle at all.

Whole-job durations stay on the [[Printjob]] and are never copied onto [[Usage]].

A [[Nozzle installation]] is not part of this measurement. It attributes grams to a nozzle; it never measures them.

## Review item

A machine-written statement that a human needs to look at a note: `needs-review: true` alongside a `review` list of `{field, reason, candidates}`, where `reason` comes from a controlled vocabulary and `candidates` names the possibilities when there are any.

A review item is not an assumption. An `assumptions` entry says *we inferred this and it may be wrong*; a review item says *we know we do not know*, or *we do know and we doubt it*. The second case is why a review item may sit on a note whose field is populated: a cross-check that disagrees -- Spoolman naming one material where the gcode profile names another -- is the only signal that an otherwise plausible link is silently wrong, which is the failure this vault most wants to catch.

Review items live in the note rather than in a log because the vault is where the work gets done. A single query over `needs-review` is the whole backlog; a warning in a log nobody reads is not a work item at all.

Clearing one is a human act: fill the field in, delete the block. The service never revisits a note, so it never clears its own review items -- see [[Create-only writer]].

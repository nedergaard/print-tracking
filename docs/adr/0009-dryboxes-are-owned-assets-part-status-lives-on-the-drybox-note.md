# Dryboxes are owned assets; upgrade-part status lives on the drybox note

A drybox is a dry-storage container for filament spools. It is an owned asset in the
master-spool sense (ADR 0007): it holds no filament, is never consumed, and outlives the
spools stored in it. It gets one note per physical box, tagged `3dprint/drybox`, named
`drybox_<label>`, identified by a short label (`db1`) printed on a sticker -- the drybox
counterpart of a spool's `spool-id`. The note holds `label`, `location` (where the box
itself sits), `capacity-spools`, and the two parts lists below. It deliberately carries no
`purchase-line` and no lifecycle status.

A drybox upgrade needs parts, and the status of each part per box is a fact the owner
wants exact, not derived. Parts are recorded as entries in two lists on the drybox note:
`printed-parts`, entries of `{model, status}` linking the Model to print, and
`purchased-parts`, entries of `{name, status}`. One entry per physical unit -- four
rollers are four entries. Both keys are present on every drybox note, empty when there are
none, because an absent key silently matches nothing in a query. `status` comes from a
controlled set: `needed`, `ordered`, `printed`, `bought`, `mounted`, `not-needed`.
`mounted` is terminal for the box; `not-needed` covers the either/or case (rollers
printable or buyable: fulfill one, mark the other `not-needed`).

## Considered Options

Deriving part status from Output notes was the first alternative, and it was rejected
because it cannot work: an Output is an immutable event, never decremented, and which
printed part is allocated to which box is a human judgement. "A base was printed" and
"db1's base is done" are two different facts, and only the second answers the question
being asked. The per-box status is therefore stored, hand-maintained like a spool's
`status`, with no cross-check against Output and no review item -- the service never
writes drybox notes.

One `parts` list with a `kind: printed|bought` field per entry was considered against two
lists. Two lists won: each list's entry shape stays homogeneous (a `model` link only makes
sense on the printed side), and no query has to discriminate on `kind`. The umbrella term
is *drybox part*; the lists are `printed-parts` and `purchased-parts`.

One note per (drybox, part) pair was rejected: a drybox part has no label and no
provenance of its own -- it is a requirement line, not an owned asset, so the per-unit
pattern does not apply. ADR 0007's own precedent is that the pairing fact lives on the
consuming note.

Recording spool occupancy in the vault -- a `drybox` field on spool notes -- was rejected.
A spool's location is live state, and the division of responsibility with Spoolman
assigns "where it currently is" to Spoolman by the nature of the fact, not by convenience.
Spoolman's spool `location` therefore names the drybox label (`db1`), and the two systems
join on that label. The box's own placement, by contrast, lives in the vault as `location`
on the drybox note: it is a stable human-maintained judgement, and Spoolman has no
container entity that could hold it.

A one-way Spoolman-to-vault sync mapping spools to dryboxes was considered and rejected.
It would copy a live fact into a second store -- the two would drift, and the vault's
only writer is create-only, so a sync would be a modifying writer, the one shape the
system refuses. The only sanctioned sync is for identity fields (`spool-id`, `card-uid`),
never for state. If answering "where exactly is spool X" (not just which drybox) becomes
cumbersome, the answer is hierarchical location strings in Spoolman (`shelf-2/db1`), so
one filter gives the full path, or joining Spoolman's spool-to-box answer with the vault
drybox note's `location` at query time -- not syncing state into the vault.

`purchase-line` on the box note and on purchased-parts entries was rejected by the owner:
provenance for dryboxes and their purchased parts is deliberately not tracked. Prices
live on invoice lines for the things that do track them (ADR 0008), and the link is
additive later if a query ever wants it.

`capacity` was renamed `capacity-spools`: the unit belongs in the key name, per the
`diameter-mm` / `weight-grams` convention, and a bare `capacity` silently answers nothing
when queried.

A lifecycle `status` on the drybox note (`active` / `retired`) was rejected: no query
asks for it yet, and a field exists only when a query needs it.

Which filaments want a drybox was placed on the Filament note as `storage: drybox` rather
than as a filament list on the drybox: the requirement is a property of the product (PETG
needs dry storage regardless of which box), so it belongs to the product, and the drybox
note stays about the box.

## Consequences

- Completeness is derived at query time, never stored: "box complete" = every entry in
  both lists has status `mounted` or `not-needed`; per-box "what's missing" = anything
  else; "do I need to print more parts for dryboxes" = `printed-parts` entries with
  `status: needed`, grouped by `model` across all dryboxes.
- The service is untouched: it never reads or writes drybox notes, and the create-only
  writer is unaffected because the service simply never comes back to a note.
- Occupancy is split by the nature of the fact: Spoolman's spool `location` names the
  drybox label (live state), the vault's drybox `location` says where the box sits
  (stable judgement). The join key is the label.
- No humidity or desiccant telemetry is recorded in the vault; drying remains dated prose
  on Filament notes, the vault's existing convention for time-varying facts.
- The NFC tag reader placed next to a drybox keeps its single job of feeding tag reads to
  the printer/Spoolman; it is not an occupancy sensor.

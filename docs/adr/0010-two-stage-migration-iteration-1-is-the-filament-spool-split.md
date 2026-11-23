# Two-stage migration: iteration 1 is the Filament/Spool split, later iterations skip what it already migrated

The migration tool (`PrintWatcher.Cli`, executable `pw`) is built and run in iterations.
Iteration 1 runs only the Filament/Spool split -- IMPLEMENTATION-PLAN section 11 steps
7, 8, 9 and 13, the spec's story-16 lifecycle map, and step 12 on the notes it touches.
The remaining steps (dual-tag split, output/usage extraction, duration conversion,
status map, model tagging, capitalised-key cleanup, and step 12 on untouched notes) run
later, as sibling `migrate` commands in the same CLI.

## Decision

- Every note iteration 1 writes or rewrites gets a `migrated` list entry
  `{by: pw migrate split-filament, on: <run date>, version: <assembly version + git sha>}`,
  appended append-only. A legacy flat `migrated_on`/`migrated_by` pair on a touched note
  becomes the first entry with its values copied verbatim (`{by: <migrated_by>,
  on: <migrated_on>}`; known `by` values are `M3-v1` for the machine pass and
  `manual-notion-backfill` for notes backfilled by hand from the 2026-11-23 Notion
  export). Recording the version is what
  lets several tool iterations leave distinguishable strata on notes touched more than
  once.
- Later iterations detect what iteration 1 already did: a note tagged `3dprint/spool`
  is skipped by the split, a note already carrying a `spool:` key is skipped by the
  rename, an existing `migrated` list is appended to rather than converted, and the
  `#3dprint/filament` reference audit is idempotent and re-runnable.
- Naming: spool renames preserve the legacy stem verbatim (`filament_<stem>` ->
  `spool_<stem>`); generated Filament notes are
  `filament_<colour>_<material>_<brand>[_<product-line>]`, slugified from the canonical
  tuple. Note-name parts are joined with `_`; `-` appears only inside stems derived from
  filenames.

## Considered Options

A single big-bang run of all steps was the original shape of the migration spec; running
iteration 1 first was chosen because the `#3dprint/filament` tag meaning change is the
one part of the migration that fails silently (ADR 0005), and landing the split first
limits that blast radius while the rest of the tool is still being built. Stamping all
~796 notes with the `migrated` list in iteration 1 -- instead of only the notes the split
touches -- was rejected to keep the iteration-1 diff reviewable; the deferred run converts
the rest.

## Consequences

Until the deferred run, the vault holds two `migrated` shapes side by side (flat pair on
untouched notes, list on touched ones); any query over `migrated` must handle both until
then. The tag `3dprint/spool` and the `spool:` key are the durable markers of "iteration
1 done", so nothing but the migration tool may write them.

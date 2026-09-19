# Split the usage note into output and usage

The vault recorded what a print produced and what it consumed in one `usage` note, and
for the common single-model single-filament case collapsed that into the `printjob` note
as well, tagging it both `3dprint/printjob` and `3dprint/usage`. That works until a job
prints two models from one filament, at which point the grams attributable to each model
do not exist in any data the printer or slicer emits and have to be invented. We have
split the responsibilities: an `output` note records one (printjob x model) pair with a
unit count, a `usage` note records one (printjob x slot) pair with grams extruded and
references no model, and a `printjob` note records only the run. Every print now produces
the same three-note shape, including the trivial case.

## Considered Options

Keeping `model` on `usage` -- making it (printjob x model x filament) -- was the
alternative, and it is exact whenever the attribution happens to be knowable. It was
rejected because it forces a fabricated split in every multi-model job, and because it
leaves `output` with nothing to contribute but a unit count. The vault's own standing
rule, written down long before this service existed, is to record only known facts and
leave estimation to query time.

## Consequences

Per-model filament consumption is no longer a stored value anywhere. It becomes a
query-time estimate, and an inherently rough one.

Roughly 680 existing notes are in the old shape: ~583 printjobs carrying `model` and
`units`, and 72 usage notes carrying both responsibilities. A one-off migration rewrites
them, splits the dual tags, and preserves the 185 printjob notes that have body content.
The migration shares its frontmatter writer with the service so the two cannot drift.

Anything querying the old shape breaks. Most visibly, nozzle usage was computed by
summing `duration-hours` over a nozzle's `3dprint/printjob` backlinks; after the split the
nozzle link lives on `usage`, which carries no duration. See
[0003-nozzle-wear-measured-in-grams.md](0003-nozzle-wear-measured-in-grams.md).

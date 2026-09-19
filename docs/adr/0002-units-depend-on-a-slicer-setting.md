# Unit counts depend on the slicer's object-exclusion setting

The count of copies a print produced is not reliably present in the gcode by default.
Snapmaker Orca's object labelling emits `; printing object <name> id:<n> copy <n>` lines,
but with default settings the `copy` index is always 0 and the `id` is uninitialised
memory -- observed values include `0x0101010101010101`, heap pointers, and a single id
shared by three different objects -- so neither field can be counted. Enabling
`exclude_object` in the print profile makes the slicer emit one
`EXCLUDE_OBJECT_DEFINE NAME=<label>_id_<i>_copy_<c>` line per physical instance, and as a
side effect repairs the `id` field in the labelling comments too. We therefore derive
`units` by counting instance definitions, and require `exclude_object` to be enabled in
every print profile used.

## Consequences

A field in the notes vault depends on a checkbox in an unrelated desktop application. If
the setting is off for a profile, or a new profile is created without it, affected jobs
lose their unit counts silently -- the gcode is still valid and still parses, it simply
contains no instance definitions. The service treats a job with object labels but no
instance definitions as a parse failure and retains the gcode for inspection rather than
guessing a count.

Filename-encoded counts are *not* used as a fallback, despite the habit of naming files
like `4xholder.stl` and `wedge-4x10x40_x4`. The same heuristic reads `wedge-4x10x40` as
four copies of something.

The setting only affects prints sliced after it is enabled. Since no history is
backfilled, this is not a concern in practice.

# Nozzle wear is measured in grams extruded, not hours run

Nozzle wear was tracked by summing print duration across the jobs that used a nozzle.
The printer has four nozzles, one per filament slot, and reports a single duration for
the whole job; a four-filament job would therefore contribute its full duration to each
of four nozzles. There is no per-nozzle time anywhere in the print data. We measure wear
as grams extruded per slot instead -- a value the printer already reports per slot, which
is recorded on each `usage` note, and which by construction attributes only to the nozzle
that passed the material.

## Consequences

Whole-job durations stay on the `printjob` note as `print-duration-secs` and
`total-duration-secs` and are never copied onto `usage`. A duration on a `usage` note
would be wrong the moment anyone summed it.

Grams is arguably the better proxy regardless: job duration includes travel, heating and
idle time, during which no material passes through the nozzle at all.

Per-nozzle extrusion time could be computed by integrating move distance over feedrate
within each tool section of the gcode. It was rejected as a meaningful amount of parser
work whose output is still an estimate, since it cannot account for acceleration.

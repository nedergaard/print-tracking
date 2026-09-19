# Nozzle identity comes from human-written installation records

The print data names a filament slot and a nozzle diameter, never a nozzle, so generated
`usage` notes left `nozzle` blank and a human filled it in afterwards -- 669 notes so far.
The vault's design document says of nozzles that "wear is tracked through linked Usage
records rather than installation periods", which is a statement about how wear is
computed rather than a prohibition: the same document lists "nozzle used" among stored
facts and "nozzle wear" among derived ones, and the stored fact had nowhere to come from.
We have added a hand-maintained `3dprint/nozzle-install` note -- one physical nozzle, one
printer, one slot, an `installed` and a `removed` bound -- and the service resolves
(printer, slot, job start) against it as it writes each `usage` note. Wear itself is
unchanged: still a sum of grams over linked `usage` notes, per ADR 0003.

## Considered Options

Resolving at query time -- leaving `nozzle` blank permanently and joining slot against
installation intervals in Dataview -- was the alternative, and it is immune to a record
written late, since a query always sees the current state of both sides. It was rejected
because every consumer would reimplement a date-range join, and because a permanently
blank `nozzle` leaves `usage` notes that are not self-contained material records, which
is the reason that note type exists at all (ADR 0001). Resolving at write time means a
`usage` note states what was true when it was written, and a record written late is
repaired the way every other mis-parse is: delete the note and let the service rebuild
it.

Implying the end of an installation from the start of the next one in the same slot was
also considered, and would have made these notes immutable, which suits the vault's
event-ledger style. It was rejected because it admits no gaps: a slot's last known
nozzle covers all future time, so forgetting to record a swap attributes grams silently
to the wrong nozzle. An explicit `removed` turns that forgetting into a visible hole.

Deriving historical installations from the 669 existing hand-written `nozzle` links was
rejected too. Those links are already correct, so nothing needs the derived records, and
the `installed` and `removed` bounds would be manufactured from first and last use --
dates that were never observed -- then stored as facts.

## Consequences

The correctness of `usage` now depends on a hand-maintained record existing before the
next print. That is a standing workflow rule of the same kind as enabling
`exclude_object` (ADR 0002) and not grouping objects in the slicer. Three outcomes are
therefore rendered distinctly: a resolved nozzle is a link; a job on a change-over day
where no time of day was recorded is attributed to the incoming nozzle and flagged with
`has-assumptions`; a contradictory record or a genuine gap leaves `nozzle` blank with no
assumption entry -- because nothing was assumed -- and is counted as a parse failure.

Bootstrapping is four notes written by hand for the slots in use. The existing
`Installed in tool N` prose on the nozzle notes stays where it is: it is the provenance
of those dates.

This is the vault's first entity type keyed on an interval rather than a date, and the
first new one since `output`. Filament is unaffected and remains unattributed -- no data
anywhere links a slot to a physical spool.

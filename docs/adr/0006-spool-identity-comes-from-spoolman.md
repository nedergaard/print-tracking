# Spool identity comes from Spoolman, not from the printer

A `usage` note should name the physical Spool its grams came off. The obvious source
is the printer: the Snapmaker U1 reads an RFID or NFC tag per filament slot, and its
Klipper `filament_detect` object exposes a `CARD_UID` per channel. We do not use it.
**`CARD_UID` is never persisted anywhere.** It exists only in the live status object -- it
is not written to disk, not recorded in `klippy.log`, not copied into Moonraker's job
history, and not stored in Moonraker's key/value database. A service that polls
`/server/history/list` after a print finishes will never see it.

There is exactly one durable per-job record of a physical spool in any of these
codebases, and it runs through Spoolman. The Extended Firmware's `spoollink` component
edge-detects a card event, reads the uid, resolves it against Spoolman's `card_uids`
custom field, and sets the active spool via Moonraker's built-in `spoolman` component --
which registers a history auxiliary field. So `GET /server/history/list` returns, per job,
`auxiliary_data[]` with `provider: "spoolman"` and `name: "spool_ids"`: the deduplicated
list of Spoolman spool ids used during that print. The service reads that list, fetches
each spool, and reads back the vault's own `spool-id` from a Spoolman custom field.

## Considered Options

Resolving a uid against a `card-uid` key on the vault's own Spool notes was the intended
design, and it is the better architecture: no runtime dependency on a third system, one
lookup in an index already held in memory. It is simply not implementable, because the
service never receives a uid.

Capturing the uid live -- subscribing to Moonraker's websocket and snapshotting
`filament_detect` at the transition into `printing` -- would work, and is what the
community filament UI does. It was rejected because the service is deliberately an
after-the-fact poller: watching prints as they happen is a different architecture and is
explicitly out of scope.

Writing a Moonraker component that registers a per-slot history field fed from
`filament_detect` is the sanctioned way to get uids into `auxiliary_data`, and is the gap
`spoollink` leaves open. It is recorded as future work. It means deploying Python into the
printer's firmware, which is a much larger commitment than reading an existing field.

## Consequences

**`spool_ids` carries no slot mapping.** It is a deduplicated set in resolution order, not
a slot-indexed array. For a single-filament job -- more than 99% of prints -- there is one
spool and one slot and the attribution is unambiguous. For a multi-filament job the
pairing is unknowable, and matching by position would be a guess presented as a fact. Such
jobs get a bare `spool` key and a review item listing the candidate spools, so the pairing
is corrected by hand in the note.

The service acquires a runtime dependency on Spoolman, which has no authentication and its
own uptime. An unreachable Spoolman fails the cycle and writes nothing, the same response
the plan gives a failed gcode download -- because create-only means a note written during a
two-minute restart would be permanently degraded, and a transient outage must never
produce durable manual work.

**The service never writes to Spoolman.** Moonraker's own `spoolman` component is already
reporting consumption with `PUT /spool/{id}/use` during prints. If print-watcher also
reported usage, every gram would be counted twice.

Spoolman stores only a running `used_weight` and no usage history at all -- history is
delegated to Prometheus. The vault's `usage` notes are therefore the only durable per-job
consumption record that exists, which makes the vault more authoritative here, not less.

Four preconditions gate all of this: Extended Firmware installed, `[spoolman]` configured
in Moonraker, NFC tags on the spools, and the vault's label code present in a Spoolman
custom field. Spoolman custom-field keys must match `^[a-z0-9_]+$`, so the key is
`spool_id` rather than `spool-id`, and every extra-field value is JSON-encoded on the
wire: a label code of `1a` reads back as `"\"1a\""`.

# Master-spools are owned assets; attachment lives on the spool note

Filament is now bought mostly as refills -- filament wound on a bare cardboard core -- and
a refill is made usable by mounting it on a master-spool: a reusable pair of plastic disks
that provides the wheels, so the spool can stand and rotate on rollers. A master-spool is
neither filament nor a spool in this system's sense. It is never consumed, it outlives
many refills, and its availability gates a purchasing decision: a plain refill can only be
bought when a master-spool is free, otherwise the small premium for a refill that ships
with one is mandatory. We track it as a first-class entity. One note per physical pair,
tagged `3dprint/master-spool`, identified by a short label (`ms1`) printed on a sticker
the way a spool's `spool-id` is. The note holds identity and provenance only: brand, the
disk pair's weight, purchase facts, `purchase-order-code` for the purchase order it
arrived in, and `acquired-with` when it arrived mounted with a single refill.
Attachment is recorded on the Spool note, as `master-spool`, and only the current
attachment is recorded anywhere.

## Considered Options

Recording master-spools only from the spool side -- a boolean on each spool, or a count
kept on one inventory note -- was the alternative. It was rejected because a free
master-spool is attached to nothing: a count cannot come from spool notes, and a
hand-kept inventory count loses all provenance and drifts silently. Per-unit notes follow
the vault's existing shape for owned assets: nozzles and spools each get one, and a
master-spool is one more of those.

Recording attachment on the master-spool note, as `mounted-on`, was also considered. The
pairing is one fact, and it changes at or before the moments the spool note is being
edited: a clear accompanies `status: spent`, and a mount accompanies `status: active` when
a refill is mounted onto an owned master spool at start. A refill bought bundled with a
master spool arrives mounted while `unopened`. Every spool's note is created at purchase,
`spool-id` complete -- only the physical label waits until first use -- and a bundled
refill's physical spool is identified from day one: the master spool's id, written by hand
on the shrink-wrap, names it, which a plain refill's indistinguishable siblings cannot do
for each other. That note is created with the field already set, so the mount costs no additional
edit and the free-master-spool count is never wrong during the unopened window. Putting
the field on the spool note also makes "which filaments are ready to use as-is" a flat
query rather than a backlink join.

An interval record per mounting -- the Nozzle Installation pattern -- would additionally
give a reuse count and durability history. It was deferred rather than rejected: no
standing question needs it, and current attachment answers all of them. Adding intervals
later is additive; carrying them from the start is a commitment nothing has asked for.

Linking a master-spool to its purchase was reopened by the pack case: refills bought as a
pack that includes master-spools. The one-to-one bundle hides the linkage problem, because
its master-spool arrives mounted on its refill: the refill's shrink-wrap names the pair,
`acquired-with` points at that spool note, and the purchase is reachable through it. A
pack ships its master-spools loose: no refill's shrink-wrap names anything, so
`acquired-with` has nothing to point at, and purchase date plus retailer is not a key --
two orders placed the same day would conflate silently. The purchase order is the
grouping the price arithmetic already operates over, and the order's refill notes carry
it by construction (`purchase-order-code`), so the master-spool note carries the same key
and the count of master-spools an order brought becomes a flat query rather than a
recollection. Pointing `acquired-with` at an arbitrary refill from the pack was rejected
on the same grounds as fuzzy model matching: a plausible wrong link is worse than a
visible hole.

Spoolman was never a candidate: it cannot model a master-spool at all, and the questions
("how many free", "which filaments ready") are vault queries.

## Consequences

Availability is derived, never stored: a master-spool is free when no spool links it, or
only a `spent` one does. Readiness is derived from hardware: a refill (`spool-type:
refill` on the spool) is ready to use as-is when its spool is `active` with `master-spool`
set; a spool with its own hardware (`spool-type: spool`) is ready with the field blank.
The distinction is recorded rather than left to memory, because a blank `master-spool`
cannot on its own tell a ready spool from an unusable bare refill. It is a field value
rather than a boolean or an extra tag: a value leaves the vocabulary room to grow, and
structured attributes belong in fields rather than tags. The field is present on every
spool note rather than absent on classic ones: in Dataview a comparison against a
missing field silently matches nothing, so a query like `spool-type != "refill"` would
return no classic spools at all while looking like a valid answer -- and absence is not
self-documenting.

NFC tag assignment is unaffected in substance but amended in wording. The tag is adhered
to the master-spool's disk yet assigned to the refill it is mounted with, and is discarded
when that refill is spent; a master-spool is a mounting surface and never carries an
identity across refills. The previous rule said the tag is retired with the spool it was
*stuck to*; it now says the spool it was *assigned to*. Mounting a new refill always means
a fresh tag and a fresh Spoolman `card_uids` entry, so no re-mapping ever happens, and the
no-interval property that rule protects (see ADR 0006) is preserved.

A purchase that brought master-spools records, on each refill spool note it contains, the
price actually paid and `refill-reference-price-dkk`: the observed market price of the
same product as a plain refill at purchase time -- a fact available on the day and
forgotten soon after, which is why it is worth recording. A refill bought one-to-one with
a master spool records the bundle's whole price. A pack bought at one undivided price
splits it equally across its refills' notes -- the one allocation the rule makes,
because a pack price is a fact about the order that no single refill note can carry whole
without breaking the sum, and an equal split is the only division with no basis to
dispute. The premium is not netted out of refill prices, because that allocation would be
a stored estimate; it stays distributed inside them. It remains computable at query time,
now as an order-level quantity: the sum of the order's refills' purchase prices minus the
sum of their reference prices, divided by the number of master-spools the order brought
-- a count the master-spool notes' `purchase-order-code` supplies. The one-to-one bundle
is the degenerate case, read off a single note. The premium is each master-spool's
estimated *marginal* value in that purchase, not its market value; it can be negative
when a bulk discount subsidizes the spools, which is a correct answer rather than a bug.
It is never stored. A master-spool note's `purchase-price-dkk` stays blank in such a
purchase and is filled only when the invoice prices the spool as its own line item -- a
fact of the day rather than an allocation. `purchase-order-code` is present on every
master-spool note rather than absent on standalone ones, for the same Dataview reason as
`master-spool` on spool notes.

The gross weight of a mounted spool includes the master-spool's disks, so the master-spool
note's `weight-grams` completes the tare chain for remaining-filament estimates.

The service is untouched. It resolves spools through Spoolman and never reads or writes
`master-spool`; the field is hand-maintained like `status`, and the create-only writer is
unaffected because the service simply never comes back to a note.

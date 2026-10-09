# Purchases are first-class; prices live on invoice lines

A spool note has always recorded provenance "at what price", but the price actually paid
is a fact about a purchase, not about a spool. Refill buying has moved to packs and
bundles whose price covers several items, and orders typically mix filaments. The only
identity a purchase leaves in the vault is the numeric part of a `spool-id`, and that is
not an order id: it is the ordinal, per filament, of purchases of that filament. A mixed
order therefore leaves several codes and no fact tying them to one purchase, and a
purchase that brought only master-spools leaves none at all -- ADR 0007's pack case
exposed the hole, because a loose master-spool had no refill for `acquired-with` to name
and no order to belong to. A purchase is modeled at two levels, both as notes: one
Purchase Order note per real-world order, holding what an invoice holds at the top
(`purchase-date`, `retailer`, `shipping-dkk`), and one Purchase Line note per invoice
line. What the seller priced is the line, so prices live on lines and nowhere else; the
order's total is the sum of its lines plus shipping, a derived figure, and is never
stored. Spool and master-spool notes link the line they arrived on (`purchase-line`), and
a line's composition is the backlinks, cross-checked against the line's counts.

## Considered Options

Recording the order on the master-spool note, as `purchase-order-code`, was the first
attempt. It failed on the code's real meaning: the number counts purchases *of one
filament*, so on a filamentless note it is ambiguous without the filament, and a mixed
order produces several codes with nothing tying them together. The code keeps its meaning
-- the ordinal is an interesting fact -- but it stops being asked to identify anything.

Grouping by purchase date plus retailer was rejected: it is not a key. Two orders placed
the same day from one retailer would conflate silently, which is the same failure shape
as a fuzzy match -- a plausible wrong link rather than a visible hole.

An items list on the Purchase Order note, one entry per line, was the second attempt. It
gave the pack price a home, but a line is a query target -- the premium, and with it every
cost question, is line-level -- and a list entry cannot be linked to, only re-derived by
matching. Making each line a note turns the join into the vault's native shape: a
wiki-link, with composition as backlinks and counts as a cross-check.

Renumbering `spool-id` to a global order count was never a candidate: it would destroy the
per-filament ordinal and invalidate printed physical labels.

## Consequences

Field presence on line notes is looser than the spool-note doctrine, deliberately. The
doctrine's failure mode is negation: a query like `spool-type != "refill"` against a
missing field silently matches nothing while looking valid. Line queries are presence
filters -- "lines that brought master-spools" is a query for the notes where the field
exists -- and there, a missing field matching nothing is the correct result, not a bug.
So an absent count means zero, and `refill-reference-price-dkk` is three-valued: absent
when the line brought no master-spools, bare when it brought them but the plain-refill
market price was never observed -- a visible hole rather than a silent one, which is how
purchases that predate the observation are backfilled -- and filled with the fact. The
tool refuses to write a line that brings master-spools without an explicit choice between
the two, so the unrecorded hole cannot enter the vault going forward.

Master-spool notes slim to identity: `brand`, `weight-grams`, and the line link. Their
purchase facts live on the line, and `acquired-with` is gone -- naming the purchase is the
line link's job, and naming the refill a bundled master-spool arrived mounted on is the
spool note's own `master-spool` field, set from creation. Spool notes keep
`purchase-date` and `retailer` -- a fact from the same invoice carried for flat queries
-- but lose `purchase-price-dkk` and `refill-reference-price-dkk`; the line is the price's
home, one rule with no quantity-one exception to blur which note owns it.

Costs stay estimates and live at query time, computed from the line by inline queries
written into the spool and master-spool note bodies. The premium of a line is its price
minus its refills' reference price; a master-spool's value is the premium divided by the
master-spools the line brought; a spool item's cost is what remains, divided by the
spool items. The master-spool's value is clamped at zero: a negative premium is
mathematically consistent but nobody was paid to take the spools, so a bulk discount's
surplus goes back to the refills instead. Every branch -- positive premium, clamped
negative, unknown reference price with masters valued at zero -- exhausts the line price,
which is the invariant the allocation exists to keep: spool costs and master-spool values
sum to what was actually paid.

Naming follows the vault's existing shapes. Orders are `purchase-order_<date>_<slug>`
with a human-chosen descriptor for the slug, never one appended only on collision -- the
printjob lesson: a name must not depend on what was named before. Lines are
`purchase-line_<date>_<order-slug>_<line-number>`, numbered from the invoice and
sequenced within the order when the invoice does not number them. Identity is the note
name in both cases; no counter is maintained anywhere.

A tool, `tools/purchase-notes.cs`, a single-file C# program run with `dotnet run`, does
the ceremony: it prompts for the order and its lines, refuses invalid combinations, scans
the vault for each filament's next ordinal and the next master-spool label, and writes
the order, line, spool, and master-spool notes with all links and the inline queries.
It uses a deliberate serialiser rather than a YAML library, as the service does, and the
service itself is untouched: it never reads or writes purchase notes, which are created
at purchase time by hand and tool the way spool notes are.

Backfill of historical purchases is deferred, and will group existing notes by purchase
date plus retailer and propose orders for confirmation -- a grouping that is safe only as
a proposal, for the same reason it was rejected as a key.

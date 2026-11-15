# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-08-07

Additive, backward-compatible hardening. No change to the matching semantics or output of any existing reconciliation: the same input and rules produce the same result as 0.1.0.

### Added

- Opt-in unkeyed-pass size guard: `ReconciliationOptions.MaxUnkeyedPassProduct`, applied via `Reconciliation.Between(...).WithOptions(...)`. An unkeyed pass (a `ThenMatchOn` with only an amount or date criterion) is O(n*m); when this ceiling is set, `Run` throws `InvalidOperationException` before a pass runs whose remaining left-by-right product exceeds it. Off by default (`null`), so existing behavior is unchanged.
- In-repo conservation and determinism property tests: hundreds of deterministic-seeded randomized datasets (negatives and refunds, zeros, kobo precision, shared group keys, orphans, near-matches, null keys) each assert that every input record appears exactly once across the matched, grouped, and unmatched views on both sides, that per-side amount sums are conserved, that the summary counts are consistent, and that reruns and shuffled input remain deterministic and conserving.
- Edge-case unit tests named by the audit: negative and refund matching, `AmountPercent` against a negative right amount, greedy first-claim when several right records share one group key, and at-most-once for a record eligible for both a pair pass and a later group pass.

### Changed

- Documented the `DateTimeKind`-ignored caveat on the aging APIs (`AgeUnmatchedLeft` / `AgeUnmatchedRight` XML docs and the README aging section), matching the note already stated for date match criteria: aging compares calendar dates only, so normalize both sides to one timezone.
- README: documented the unkeyed-pass size guard under Performance, and added a prose cross-link noting that the sibling Mt940.Net can parse SWIFT MT940 statements into records that feed reconciliation (no dependency between the packages).

### Deferred

- The value-type composite-key fast path (boxing-free typed keys) is deferred to 0.2. Guaranteeing byte-identical output for all key types while removing boxing would replace `object.Equals` with `EqualityComparer<T>.Default` and rework the two hottest matching paths, which is more surface than a patch on a zero-defect engine should carry. It remains on the roadmap.

## [0.1.0] - 2026-08-03

### Added

- Fluent, generic reconciliation pipeline: `Reconciliation.Between(left, right).MatchOn(...).ThenMatchOn(...).ThenMatchGroups(...).Run()` over any caller-defined record types.
- Exact key matching with composite keys (repeat `MatchOn` or `Key`); keyed passes are dictionary-based, O(n + m) per pass, with a timed 100k x 100k linearity test in the suite.
- Amount criteria: absolute tolerance and percentage-of-right tolerance, inclusive boundaries, zero (exact) default.
- Date window criteria with direction control: `Either`, `LeftMayBeLater`, `RightMayBeLater`; inclusive boundary.
- One-to-many group passes for gross-vs-net settlement: one right record against the sum of remaining left records sharing an explicit group key, with amount tolerance and fee variance reporting.
- Immutable results: matched pairs (pass number, pass description, signed amount and date variances), matched groups (left total, right amount, variance), unmatched lists in original stream order, and a summary of counts plus amount-bearing totals (`AmountBearingMatchedLeftTotal`, `AmountBearingMatchedRightTotal`, `AmountBearingVarianceTotal`).
- `ReconciliationResult.SumAmounts(leftAmount, rightAmount)` returning `AmountTotals`: true totals over every match made by every pass plus everything unmatched, with matched plus unmatched always equal to the input totals on each side.
- Aging of unmatched items on either side into 0-7, 8-30, 31-90, and 91+ day buckets, with custom bucket bounds.
- Documented determinism guarantees: passes run in declaration order over remaining records only, every record matches at most once, ties resolve first-by-original-order, identical input yields identical output, and null never equals null (a record whose key selector returns null never key-matches; it surfaces in the unmatched lists).
- Fail-loud validation at build time: null selectors throw `ArgumentNullException`, negative tolerances and windows throw `ArgumentOutOfRangeException`, passes without criteria throw `InvalidOperationException`.
- `decimal` in every money path; zero runtime dependencies; SourceLink (GitHub), deterministic CI builds, and `.snupkg` symbol packages.

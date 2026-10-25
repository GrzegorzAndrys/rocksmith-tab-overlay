# Vendored RockSniffer

`RockSniffer/` is a source snapshot, not a Git submodule, so a clean clone can
build the complete overlay without fetching nested repositories.

- Upstream: https://github.com/kokolihapihvi/RockSniffer
- Base commit: `6111f62` (`Bump PsarcLib, fix #61`)
- RockSnifferLib: `745a741` (`v0.3.4-20-g745a741`)
- Rocksmith2014PsarcLib: `c226fca` (`heads/main`)

Local changes:

- Added a lightweight `GET /tab` endpoint that omits album art from the
  high-frequency response and includes monotonic memory-sample timestamps.
- Added `ArrangementData.TabEvents` and per-phrase maximum-difficulty note
  merging, including structured bends, hammer-ons, pull-offs, tremolo picking,
  and pitched/unpitched slide destinations.
- Versioned parsed arrangement caches so notation additions trigger a one-time
  safe rebuild instead of leaving stale chart data in place.
- Added arrangement-ID cache lookup for the short window where Rocksmith
  reports a new song before the song ID catches up.
- Added `runtimeconfig.template.json` with `rollForward: LatestMajor` so the
  net8.0 build can run on newer installed .NET runtimes.
- Removed the nested `.git` metadata and upstream CI files.

Upstream and local changes remain MIT-licensed; see `RockSniffer/LICENSE`,
`RockSniffer/RockSnifferLib/LICENSE`, and
`RockSniffer/Rocksmith2014PsarcLib/LICENSE`.

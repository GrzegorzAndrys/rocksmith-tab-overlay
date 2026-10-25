# Third-party notices

Rocksmith TAB Overlay is MIT-licensed. It includes or links against the
following third-party software.

## Vendored source

The `vendor/RockSniffer` tree is a modified snapshot of
[RockSniffer](https://github.com/kokolihapihvi/RockSniffer), commit
`6111f62`, including its `RockSnifferLib` and `Rocksmith2014PsarcLib`
submodules. All three are MIT-licensed:

- RockSniffer, Copyright (c) 2017 kokolihapihvi
- RockSnifferLib, Copyright (c) 2017 kokolihapihvi
- Rocksmith2014PsarcLib, Copyright (c) 2020 kokolihapihvi

The original MIT license texts are kept in `vendor/RockSniffer/LICENSE`,
`vendor/RockSniffer/RockSnifferLib/LICENSE`, and
`vendor/RockSniffer/Rocksmith2014PsarcLib/LICENSE`. The local changes are
described in `vendor/README.md`.

The optional RockSniffer addon web UI in `vendor/RockSniffer/addons` is
included from the same upstream project. Its JavaScript dependencies and
assets retain their respective upstream terms.

## NuGet dependencies

The published application also redistributes the following packages:

| Package | License |
| --- | --- |
| DiscordRichPresence | MIT |
| Newtonsoft.Json | MIT |
| Pfim | MIT |
| Portable.BouncyCastle | Bouncy Castle License |
| System.Data.SQLite.Core | SQLite public domain |
| System.Drawing.Common | MIT |
| Microsoft.Win32.SystemEvents | MIT |
| System.Threading.Tasks.Dataflow | MIT |

License texts and package metadata are available from the linked NuGet
packages and their upstream projects. RockSniffer's own dependency list is in
`vendor/RockSniffer/RockSniffer/RockSniffer.csproj`.

Rocksmith 2014 and its content are property of their respective owners. This
project does not include game assets; it reads charts from the user's
installed game files.

# macOS Publishing

The macOS project creates a self-contained, architecture-specific `Files.app` after a Release publish.

## Automated signed release

After storing notarization credentials in Keychain with the `Files-MacOS-notary` profile, run:

```shell
./build_release.sh
```

The script automatically cleans and publishes the ARM64 Release build, signs and verifies `Files.app`, notarizes and staples the app, creates and signs the DMG, notarizes and staples the DMG, runs Gatekeeper verification and writes a SHA-256 checksum beside the artifact.

It automatically selects the first valid `Developer ID Application` identity. Override release settings when required:

```shell
MACOS_SIGN_IDENTITY="Developer ID Application: Example (TEAMID)" \
NOTARY_KEYCHAIN_PROFILE="Files-MacOS-notary" \
RUNTIME_IDENTIFIER="osx-arm64" \
./build_release.sh
```

Credentials and app-specific passwords are read from Keychain and are never stored in the script.

## Local ad-hoc packages

```shell
dotnet publish src/Files.App.MacOS/Files.App.MacOS.csproj \
  -f net10.0-desktop \
  -p:Configuration=Release \
  -p:RuntimeIdentifier=osx-arm64

dotnet publish src/Files.App.MacOS/Files.App.MacOS.csproj \
  -f net10.0-desktop \
  -p:Configuration=Release \
  -p:RuntimeIdentifier=osx-x64
```

The bundles are written beside their publish directories:

- `src/Files.App.MacOS/bin/Release/net10.0-desktop/osx-arm64/Files.app`
- `src/Files.App.MacOS/bin/Release/net10.0-desktop/osx-x64/Files.app`

Patch releases use a three-part semantic version such as `0.1.1`, `0.1.2` or `0.1.10`. Update `Version` and `ApplicationDisplayVersion` together, and increase the numeric `ApplicationVersion` for every published build. A release can also override them without editing the project:

```shell
dotnet publish src/Files.App.MacOS/Files.App.MacOS.csproj \
  -f net10.0-desktop \
  -c Release \
  -r osx-arm64 \
  -p:Version=0.1.2 \
  -p:ApplicationDisplayVersion=0.1.2 \
  -p:ApplicationVersion=10102
```

Release bundles omit managed symbols and .NET diagnostic payloads. Keep symbols as separate CI artifacts when crash symbolication is required; do not copy them back into the distributed app.

Local builds use an ad-hoc signature and `Files.AdHoc.entitlements`. The local entitlement disables library validation because separately ad-hoc-signed .NET runtime libraries do not share a Team ID. It must not be used for Developer ID distribution.

Full Disk Access is tied to the app's code identity. Replacing an ad-hoc-signed bundle can therefore require removing the old Files entry from System Settings and adding the updated app again. For repeated local releases, pass the same valid Apple Development identity on every publish so the designated requirement remains stable; public distribution should use the Developer ID flow below.

## Developer ID packages

CI or a release operator supplies a valid Developer ID Application identity and the strict entitlement file:

```shell
dotnet publish src/Files.App.MacOS/Files.App.MacOS.csproj \
  -f net10.0-desktop \
  -p:Configuration=Release \
  -p:RuntimeIdentifier=osx-arm64 \
  -p:MacOSCodeSignIdentity="Developer ID Application: Example (TEAMID)" \
  -p:MacOSCodeSignTimestampArgument=--timestamp \
  -p:MacOSCodeSignEntitlements="$PWD/src/Files.App.MacOS/Packaging/Files.entitlements"
```

The same command is used with `osx-x64`. Prefer `build_release.sh` for public artifacts so the complete signing, notarization and verification sequence is not skipped.

## Verification

```shell
plutil -lint Files.app/Contents/Info.plist
codesign --verify --deep --strict --verbose=4 Files.app
spctl --assess --type execute --verbose=4 Files.app
```

The bundle keeps native code as real files in `Contents/MacOS`. Managed assemblies and other runtime resources live in `Contents/Resources/Runtime`, with relative links that preserve .NET probing behavior without violating macOS nested-code layout rules.

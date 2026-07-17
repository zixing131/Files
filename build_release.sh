#!/bin/zsh

set -euo pipefail

readonly ROOT_DIR="${0:A:h}"
readonly PROJECT="$ROOT_DIR/src/Files.App.MacOS/Files.App.MacOS.csproj"
readonly TARGET_FRAMEWORK="net10.0-desktop"
readonly ENTITLEMENTS="$ROOT_DIR/src/Files.App.MacOS/Packaging/Files.entitlements"
readonly RUNTIME_IDENTIFIER="${RUNTIME_IDENTIFIER:-osx-arm64}"
readonly NOTARY_PROFILE="${NOTARY_KEYCHAIN_PROFILE:-Files-MacOS-notary}"

case "$RUNTIME_IDENTIFIER" in
	osx-arm64) readonly PACKAGE_ARCH="arm64" ;;
	osx-x64) readonly PACKAGE_ARCH="x64" ;;
	*) print -u2 "Unsupported runtime identifier: $RUNTIME_IDENTIFIER"; exit 1 ;;
esac

function require_command()
{
	if ! command -v "$1" >/dev/null 2>&1; then
		print -u2 "Required command was not found: $1"
		exit 1
	fi
}

function log_step()
{
	print "\n==> $1"
}

function notarize_file()
{
	local input_path="$1"
	local label="$2"
	local result_file="$WORK_DIR/notary-$label.json"

	log_step "Notarizing $label"
	if ! xcrun notarytool submit "$input_path" \
		--keychain-profile "$NOTARY_PROFILE" \
		--wait \
		--output-format json > "$result_file"; then
		cat "$result_file" >&2
		return 1
	fi

	cat "$result_file"
	local notary_status
	notary_status="$(plutil -extract status raw -o - "$result_file")"
	if [[ "$notary_status" != "Accepted" ]]; then
		print -u2 "Apple notarization did not accept $label (status: $notary_status)."
		return 1
	fi
}

for command_name in dotnet xcrun codesign hdiutil ditto plutil security shasum; do
	require_command "$command_name"
done

if [[ "$(uname -s)" != "Darwin" ]]; then
	print -u2 "This release script must run on macOS."
	exit 1
fi

if [[ ! -f "$PROJECT" || ! -f "$ENTITLEMENTS" ]]; then
	print -u2 "Run this script from a complete Files_MacOS source checkout."
	exit 1
fi

VERSION="$(sed -n 's/.*<ApplicationDisplayVersion>\([^<]*\)<\/ApplicationDisplayVersion>.*/\1/p' "$PROJECT" | sed -n '1p')"
if [[ -z "$VERSION" ]]; then
	print -u2 "Unable to read ApplicationDisplayVersion from $PROJECT."
	exit 1
fi

SIGN_IDENTITY="${MACOS_SIGN_IDENTITY:-}"
if [[ -z "$SIGN_IDENTITY" ]]; then
	IDENTITIES="$(security find-identity -v -p codesigning)"
	SIGN_IDENTITY="$(print -r -- "$IDENTITIES" | sed -n 's/.*"\(Developer ID Application:[^"]*\)".*/\1/p' | sed -n '1p')"
fi
if [[ -z "$SIGN_IDENTITY" ]]; then
	print -u2 "No valid Developer ID Application certificate was found."
	print -u2 "Set MACOS_SIGN_IDENTITY to the certificate name after installing it in Keychain Access."
	exit 1
fi

readonly VERSION
readonly SIGN_IDENTITY
readonly APP_PATH="$ROOT_DIR/src/Files.App.MacOS/bin/Release/$TARGET_FRAMEWORK/$RUNTIME_IDENTIFIER/Files.app"
readonly ARTIFACT_DIR="${ARTIFACT_DIR:-$ROOT_DIR/artifacts}"
readonly DMG_PATH="$ARTIFACT_DIR/Files_MacOS-$VERSION-macos-$PACKAGE_ARCH.dmg"
readonly CHECKSUM_PATH="$DMG_PATH.sha256"
readonly WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/files-macos-release.XXXXXX")"
trap 'rm -rf "$WORK_DIR"' EXIT INT TERM

print "Files_MacOS release build"
print "  Version:          $VERSION"
print "  Runtime:          $RUNTIME_IDENTIFIER"
print "  Signing identity: $SIGN_IDENTITY"
print "  Notary profile:   $NOTARY_PROFILE"
print "  Output:           $DMG_PATH"

mkdir -p "$ARTIFACT_DIR"

log_step "Cleaning Release output"
dotnet clean "$PROJECT" \
	-f "$TARGET_FRAMEWORK" \
	-c Release \
	-r "$RUNTIME_IDENTIFIER" \
	-v:quiet \
	-clp:ErrorsOnly

log_step "Publishing and signing Files.app"
dotnet publish "$PROJECT" \
	-f "$TARGET_FRAMEWORK" \
	-c Release \
	-r "$RUNTIME_IDENTIFIER" \
	--self-contained true \
	-v:quiet \
	-clp:ErrorsOnly \
	"-p:MacOSCodeSignIdentity=$SIGN_IDENTITY" \
	-p:MacOSCodeSignTimestampArgument=--timestamp \
	"-p:MacOSCodeSignEntitlements=$ENTITLEMENTS"

if [[ ! -d "$APP_PATH" ]]; then
	print -u2 "Release app bundle was not created: $APP_PATH"
	exit 1
fi

plutil -lint "$APP_PATH/Contents/Info.plist"
codesign --verify --deep --strict --verbose=2 "$APP_PATH"

log_step "Preparing Files.app for notarization"
readonly APP_ZIP="$WORK_DIR/Files.app.zip"
ditto -c -k --keepParent "$APP_PATH" "$APP_ZIP"
notarize_file "$APP_ZIP" "app"
xcrun stapler staple "$APP_PATH"
xcrun stapler validate "$APP_PATH"

log_step "Creating signed DMG"
readonly DMG_STAGE="$WORK_DIR/dmg"
mkdir -p "$DMG_STAGE"
ditto "$APP_PATH" "$DMG_STAGE/Files.app"
ln -s /Applications "$DMG_STAGE/Applications"
hdiutil create \
	-volname "Files_MacOS $VERSION" \
	-srcfolder "$DMG_STAGE" \
	-ov \
	-format UDZO \
	"$DMG_PATH"
codesign --force \
	--sign "$SIGN_IDENTITY" \
	--options runtime \
	--timestamp \
	"$DMG_PATH"
codesign --verify --verbose=2 "$DMG_PATH"

notarize_file "$DMG_PATH" "dmg"
xcrun stapler staple "$DMG_PATH"
xcrun stapler validate "$DMG_PATH"

log_step "Performing final verification"
codesign --verify --deep --strict --verbose=2 "$APP_PATH"
spctl --assess --verbose=2 --type open --context context:primary-signature "$DMG_PATH"
shasum -a 256 "$DMG_PATH" | tee "$CHECKSUM_PATH"

print "\nRelease completed successfully:"
print "  $DMG_PATH"
print "  $CHECKSUM_PATH"

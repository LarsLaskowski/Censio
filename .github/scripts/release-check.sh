#!/usr/bin/env bash
# The checks of the release workflow (feature 5, record 0012): the workflow and the test script
# release-check-test.sh both run them, so a refusal is tested before a release needs it.
# Usage: release-check.sh <command> <argument>...   (run from the repository root)
#   tag <tag>                                        prints the version: the tag without its "v"
#   form version|digest <name> <value>               checks a value passed from an earlier job
#   on-main <tag> <main-ref>                         the tagged commit must be reachable from <main-ref>
#   prerelease <tag> <true|false>                    warns when the release was published as a full release
#   dockerhub-secrets <user-set> <token-set>         both Docker Hub secrets must be set (true)
#   companion <version> <out-dir>                    publishes, checks and zips the Companion
#   check-companion <publish-dir> <dll> <version>    checks a Companion publish output
#   image <version> <image>                          builds and checks the server image, prints its ID
#   check-image <image> <version>                    starts the image and checks its health and version
#   push <archive> <image-id> <repo> <version>       loads the checked image, pushes <repo>:<version>, prints its digest
#   promote-image <repo> <digest>                    sets <repo>:latest to <digest> in the registry
# Needs bash, git, dotnet, docker with buildx, curl, python3, zip and unzip, depending on the command.
# Every error is the annotation "::error::<text>" on stderr and exit code 1; the warning of "prerelease" is
# "::warning::<text>" with exit code 0. Values and tool output that are not the result go to stderr, so a result on
# stdout can be captured with $(...). Nothing here reads a secret: the caller logs in to Docker Hub.
set -euo pipefail
export LC_ALL=C

version_pattern='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
digest_pattern='^sha256:[0-9a-f]{64}$'
companion_project="src/Censio.Companion/Censio.Companion.csproj"
companion_dll="src/Censio.Companion/obj/Release/net10.0-windows/win-x64/Censio.Companion.dll"
dockerfile="src/Censio.Server/Dockerfile"
exe_name="Censio.Companion.exe"
container=""

cleanup()
{
    if [[ -n "$container" ]]; then
        docker rm -f "$container" > /dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

# Sets "encoded" to the value as an annotation message: %, CR and LF are escaped.
encode()
{
    local value="$1"

    value="${value//'%'/%25}"
    value="${value//$'\r'/%0D}"
    value="${value//$'\n'/%0A}"
    encoded="$value"
}

error()
{
    local message="$1"
    local encoded

    encode "$message"
    printf '::error::%s\n' "$encoded" >&2
    exit 1
}

usage()
{
    echo "release-check: $1" >&2
    exit 2
}

# Prints the version of a tag; the tag is matched whole by a fixed pattern.
check_tag()
{
    local tag="${1-}"

    if [[ "$tag" != v* ]] || [[ ! "${tag#v}" =~ $version_pattern ]]; then
        error "Release tag '${tag}' is not of the form vMAJOR.MINOR.PATCH."
    fi
    printf '%s\n' "${tag#v}"
}

check_form()
{
    local kind="${1-}"
    local name="${2-}"
    local value="${3-}"
    local pattern

    case "$kind" in
        version) pattern="$version_pattern" ;;
        digest) pattern="$digest_pattern" ;;
        *) usage "form: unknown kind '${kind}', expected version or digest" ;;
    esac
    if [[ ! "$value" =~ $pattern ]]; then
        error "The value ${name} passed from an earlier job is not of the expected form."
    fi
}

check_on_main()
{
    local tag="${1-}"
    local main_ref="${2-}"
    local sha
    local status=0

    sha="$(git rev-parse "refs/tags/${tag}^{commit}")"
    git merge-base --is-ancestor "$sha" "$main_ref" || status=$?
    if ((status == 1)); then
        error "Release tag '${tag}' points to commit ${sha}, which is not on main."
    elif ((status != 0)); then
        exit "$status"
    fi
}

check_prerelease()
{
    local tag="${1-}"
    local prerelease="${2-}"
    local encoded

    case "$prerelease" in
        true) ;;
        false)
            encode "Release ${tag} was published as a full release; publish releases as pre-releases so they are not shown as the latest release before their artifacts exist."
            printf '::warning::%s\n' "$encoded" >&2
            ;;
        *) usage "prerelease: expected true or false, got '${prerelease}'" ;;
    esac
}

check_dockerhub_secrets()
{
    local user_set="${1-}"
    local token_set="${2-}"

    if [[ "$user_set" != "true" || "$token_set" != "true" ]]; then
        error "Docker Hub credentials are missing: set DOCKERHUB_USERNAME and DOCKERHUB_TOKEN as secrets of the environment release."
    fi
}

# Fails when the files of a directory are not exactly the Companion exe.
check_file_list()
{
    local directory="${1-}"
    local files

    files="$(find "$directory" -mindepth 1 -type f -printf '%P\n' | sort)"
    if [[ "$files" != "$exe_name" ]]; then
        files="${files//$'\n'/, }"
        error "The Companion publish output holds ${files}, expected ${exe_name} only."
    fi
}

check_companion()
{
    local directory="${1-}"
    local dll="${2-}"
    local version="${3-}"
    local report
    local bundled
    local actual

    check_file_list "$directory"
    # The dll must be inside the single-file bundle byte for byte; its ProductVersion is the value the tray reads:
    # the compiler writes the informational version into the dll's own Win32 VERSIONINFO.
    report="$(python3 -I - "$dll" "${directory}/${exe_name}" << 'PY'
import mmap
import sys

dll_path, exe_path = sys.argv[1], sys.argv[2]
with open(dll_path, "rb") as handle:
    dll = handle.read()

with open(exe_path, "rb") as handle:
    with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as exe:
        print("bundled=" + ("yes" if exe.find(dll) >= 0 else "no"))

key = "ProductVersion".encode("utf-16-le") + b"\x00\x00"
version = "unknown"
position = dll.find(key)
while position >= 0:
    # VERSIONINFO String: wLength, wValueLength, wType (1 = text), szKey, padding, Value.
    if position >= 2 and dll[position - 2:position] == b"\x01\x00":
        start = position + len(key)
        while dll[start:start + 1] == b"\x00":
            start += 1
        end = start
        while end + 1 < len(dll) and dll[end:end + 2] != b"\x00\x00":
            end += 2
        version = dll[start:end].decode("utf-16-le", errors="replace")
        break
    position = dll.find(key, position + 1)
print("version=" + version)
PY
)"
    bundled="$(sed -n 's/^bundled=//p' <<< "$report")"
    actual="$(sed -n 's/^version=//p' <<< "$report")"
    if [[ "$bundled" != "yes" ]]; then
        error "${exe_name} does not bundle the checked Censio.Companion.dll."
    fi
    if [[ "$actual" != "$version" ]]; then
        error "${exe_name} shows version '${actual}', expected '${version}'."
    fi
}

companion()
{
    local version="${1-}"
    local out="${2-}"
    local archive
    local listed

    check_form version version "$version"
    archive="${out}/Censio-${version}-win-x64.zip"
    rm -rf "${out}/publish"
    rm -f "$archive"
    mkdir -p "$out"
    dotnet publish "$companion_project" -c Release -r win-x64 --self-contained true \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded \
        -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false \
        "-p:Version=${version}" -o "${out}/publish" >&2
    check_companion "${out}/publish" "$companion_dll" "$version"
    zip -j -X -q "$archive" "${out}/publish/${exe_name}" >&2
    listed="$(unzip -Z1 "$archive")"
    if [[ "$listed" != "$exe_name" ]]; then
        listed="${listed//$'\n'/, }"
        error "The Companion publish output holds ${listed}, expected ${exe_name} only."
    fi
    echo "${archive}"
}

check_image()
{
    local image="${1-}"
    local version="${2-}"
    local port
    local deadline=$((SECONDS + 60))
    local healthy=false
    local running
    local page
    local actual

    container="censio-release-check-$$-${RANDOM}"
    docker run -d --name "$container" -p 127.0.0.1::8080 "$image" > /dev/null
    # An image that exits at once publishes no port; the wait loop below then ends and reports E6.
    port="$(docker port "$container" 8080/tcp 2> /dev/null | sed -n '1p' || true)"
    port="${port##*:}"
    while ((SECONDS < deadline)); do
        running="$(docker inspect --format '{{.State.Running}}' "$container")"
        if [[ "$running" != "true" ]]; then
            break
        fi
        if [[ "$(curl -sS --max-time 3 "http://127.0.0.1:${port}/health" 2> /dev/null || true)" == "Healthy" ]]; then
            healthy=true
            break
        fi
        sleep 1
    done
    if [[ "$healthy" != "true" ]]; then
        docker logs "$container" >&2 || true
        error "The server image did not answer Healthy on /health within 60 s."
    fi
    actual="no answer"
    if page="$(curl -fsS --max-time 5 "http://127.0.0.1:${port}/")"; then
        actual="$(sed -n 's|.*<p>Version \([^<]*\)</p>.*|\1|p' <<< "$page")"
        actual="${actual%%$'\n'*}"
    fi
    docker rm -f "$container" > /dev/null 2>&1 || true
    container=""
    if [[ "$actual" != "$version" ]]; then
        error "The server image shows version '${actual}', expected '${version}'."
    fi
}

image()
{
    local version="${1-}"
    local image_name="${2-}"

    check_form version version "$version"
    docker build --provenance=false -f "$dockerfile" --build-arg "VERSION=${version}" -t "$image_name" . >&2
    check_image "$image_name" "$version"
    docker image inspect --format '{{.Id}}' "$image_name"
}

push()
{
    local archive="${1-}"
    local image_id="${2-}"
    local repo="${3-}"
    local version="${4-}"
    local loaded
    local reference
    local loaded_id
    local kind

    check_form digest image-id "$image_id"
    check_form version version "$version"
    loaded="$(docker load -i "$archive" | tail -n 1)"
    echo "$loaded" >&2
    reference="${loaded#Loaded image: }"
    reference="${reference#Loaded image ID: }"
    loaded_id="$(docker image inspect --format '{{.Id}}' "$reference")"
    if [[ "$loaded_id" != "$image_id" ]]; then
        error "The loaded image is ${loaded_id}, expected the checked image ${image_id}."
    fi
    docker tag "$reference" "${repo}:${version}"
    docker push "${repo}:${version}" >&2
    kind="$(docker buildx imagetools inspect "${repo}:${version}" --format '{{.Manifest.MediaType}} {{.Image.OS}}/{{.Image.Architecture}}')"
    case "$kind" in
        "application/vnd.oci.image.manifest.v1+json linux/amd64" | "application/vnd.docker.distribution.manifest.v2+json linux/amd64") ;;
        *) error "${repo}:${version} is not a single linux/amd64 image." ;;
    esac
    docker buildx imagetools inspect "${repo}:${version}" --format '{{.Manifest.Digest}}'
}

promote_image()
{
    local repo="${1-}"
    local digest="${2-}"
    local actual

    check_form digest digest "$digest"
    docker buildx imagetools create --prefer-index=false -t "${repo}:latest" "${repo}@${digest}" >&2
    actual="$(docker buildx imagetools inspect "${repo}:latest" --format '{{.Manifest.Digest}}')"
    if [[ "$actual" != "$digest" ]]; then
        error "${repo}:latest has digest ${actual}, expected ${digest}."
    fi
    echo "$actual"
}

command_name="${1-}"
if (($# > 0)); then
    shift
fi
case "$command_name" in
    tag) check_tag "$@" ;;
    form) check_form "$@" ;;
    on-main) check_on_main "$@" ;;
    prerelease) check_prerelease "$@" ;;
    dockerhub-secrets) check_dockerhub_secrets "$@" ;;
    companion) companion "$@" ;;
    check-companion) check_companion "$@" ;;
    image) image "$@" ;;
    check-image) check_image "$@" ;;
    push) push "$@" ;;
    promote-image) promote_image "$@" ;;
    *) usage "unknown command '${command_name}'" ;;
esac

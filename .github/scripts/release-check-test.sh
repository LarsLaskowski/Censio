#!/usr/bin/env bash
# Tests release-check.sh the way the acceptance criteria AC2 to AC8 of feature 5 and AC1 to AC4 of issue #78 describe: every case asserts the
# exit code and the exact annotation line (the last line of the output). Run from the repository root; needs docker
# with buildx and a running daemon, curl, git, dotnet, python3, zip and unzip. It builds the Companion and the
# server image once with version 1.2.3, starts a local registry (registry:2 pinned by digest) on 127.0.0.1 and
# pushes only there. Pushes nothing to Docker Hub and logs in nowhere.
# The ZIP built with version 1.2.3 is copied to ${RUNNER_TEMP:-${TMPDIR:-/tmp}}/companion-dry-run for the CI artifact.
# Every failure prints "release-check-test: <case>: <expected> / <actual>".
set -euo pipefail

script="$(pwd)/.github/scripts/release-check.sh"
registry_image="registry@sha256:a3d8aaa63ed8681a604f1dea0aa03f100d5895b6a58ace528858a7b332415373"
version="1.2.3"
other_version="1.2.4"
zero_digest="sha256:0000000000000000000000000000000000000000000000000000000000000000"
work="$(mktemp -d)"
dry_run_dir="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/companion-dry-run"
registry_name="release-check-test-registry-$$"
images=()
skipped=()
out=""
rc=0

cleanup()
{
    local name

    docker rm -f "$registry_name" > /dev/null 2>&1 || true
    for name in "${images[@]}"; do
        docker image rm -f "$name" > /dev/null 2>&1 || true
    done
    rm -rf "$work"
}
trap cleanup EXIT

step()
{
    echo "== $1"
}

fail()
{
    local case_name="$1"
    local expected="$2"
    local actual="$3"

    echo "release-check-test: ${case_name}: ${expected} / ${actual}" >&2
    exit 1
}

# Runs a command, keeps its combined output in "out" and its exit code in "rc".
run()
{
    rc=0
    out="$("$@" 2>&1)" || rc=$?
}

last_line()
{
    printf '%s' "${out##*$'\n'}"
}

expect_equal()
{
    local case_name="$1"
    local expected="$2"
    local actual="$3"

    if [[ "$expected" != "$actual" ]]; then
        fail "$case_name" "$expected" "$actual"
    fi
}

# Runs the script and expects exit code 1 with the given text as the annotation on the last line.
expect_error()
{
    local case_name="$1"
    local text="$2"
    shift 2

    run bash "$script" "$@"
    if ((rc != 0)) && ((rc != 1)); then
        echo "$out" >&2
    fi
    expect_equal "${case_name} exit code" "1" "$rc"
    expect_equal "${case_name} annotation" "::error::${text}" "$(last_line)"
}

# Runs the script and expects exit code 0 and exactly the given output.
expect_ok()
{
    local case_name="$1"
    local expected="$2"
    shift 2

    run bash "$script" "$@"
    if ((rc != 0)); then
        echo "$out" >&2
    fi
    expect_equal "${case_name} exit code" "0" "$rc"
    expect_equal "${case_name} output" "$expected" "$out"
}

tag_error()
{
    local tag="$1"

    printf "Release tag '%s' is not of the form vMAJOR.MINOR.PATCH." "$tag"
}

form_error()
{
    local value="$1"

    printf 'The value %s passed from an earlier job is not of the expected form.' "$value"
}

image_id_of()
{
    local image="$1"

    docker image inspect --format '{{.Id}}' "$image"
}

registry_digest()
{
    local reference="$1"

    docker buildx imagetools inspect "$reference" --format '{{.Manifest.Digest}}'
}

# Builds an image of one file without network access; further arguments are extra build options.
build_tiny()
{
    local name="$1"
    local directory="${work}/tiny-${RANDOM}"

    mkdir -p "$directory"
    printf 'tiny' > "${directory}/f"
    printf 'FROM scratch\nCOPY f /f\n' > "${directory}/Dockerfile"
    images+=("$name")
    shift
    docker build -q "$@" -t "$name" "$directory" > /dev/null
}

step "AC4: tag"
expect_ok "AC4 v0.1.0" "0.1.0" tag v0.1.0
expect_ok "AC4 v10.20.30" "10.20.30" tag v10.20.30
for bad in v1.2 v1.2.3-rc.1 1.2.3 v01.2.3 V1.2.3 v1.2.3.4 "" 'v1.2.3;id' 'v1.2.3"'; do
    expect_error "AC4 '${bad}'" "$(tag_error "$bad")" tag "$bad"
done
mkdir "${work}/inject"
(
    cd "${work}/inject"
    injected=$'v1.2.3$(touch pwned)'
    run bash "$script" tag "$injected"
    expect_equal "AC4 injection exit code" "1" "$rc"
    expect_equal "AC4 injection annotation" "::error::$(tag_error "$injected")" "$(last_line)"
    if [[ -e pwned ]]; then
        fail "AC4 injection" "no file pwned" "pwned exists"
    fi
)
expect_error "AC4 percent" "$(tag_error 'v1.2.3%250A')" tag 'v1.2.3%0A'
expect_error "AC4 line break" "Release tag 'v1.2.3%0Ax' is not of the form vMAJOR.MINOR.PATCH." tag $'v1.2.3\nx'
expect_error "AC4 trailing line break" "Release tag 'v1.2.3%0A' is not of the form vMAJOR.MINOR.PATCH." tag $'v1.2.3\n'
expect_error "AC4 carriage return" "Release tag 'v1.2.3%0D' is not of the form vMAJOR.MINOR.PATCH." tag $'v1.2.3\r'

step "AC4: form"
digest="sha256:$(printf 'a%.0s' {1..64})"
expect_ok "AC4 form version" "" form version version 1.2.3
expect_ok "AC4 form digest" "" form digest digest "$digest"
for bad in v1.2.3 '1.2.3;id' 1.2 "" $'1.2.3\n'; do
    expect_error "AC4 form version '${bad}'" "$(form_error version)" form version version "$bad"
done
for bad in "sha256:$(printf 'A%.0s' {1..64})" "sha256:$(printf 'a%.0s' {1..63})" "${digest}"$'\n' "${digest};id" "" "$(printf 'a%.0s' {1..64})"; do
    expect_error "AC4 form digest '${bad}'" "$(form_error image-id)" form digest image-id "$bad"
done

step "AC5: on-main"
repo="${work}/repo"
git init -q -b main "$repo"
git -C "$repo" config user.name test
git -C "$repo" config user.email test@example.com
git -C "$repo" config commit.gpgsign false
git -C "$repo" config tag.gpgsign false
git -C "$repo" commit -q --allow-empty -m first
git -C "$repo" tag v1.0.0
git -C "$repo" commit -q --allow-empty -m second
git -C "$repo" tag v1.0.1
git -C "$repo" switch -q -c side
git -C "$repo" commit -q --allow-empty -m side
git -C "$repo" tag v1.0.2
side_sha="$(git -C "$repo" rev-parse HEAD)"
git -C "$repo" switch -q main
(
    cd "$repo"
    expect_ok "AC5 older commit" "" on-main v1.0.0 main
    expect_ok "AC5 head of main" "" on-main v1.0.1 main
    expect_error "AC5 side branch" "Release tag 'v1.0.2' points to commit ${side_sha}, which is not on main." on-main v1.0.2 main
)

step "AC6: prerelease"
expect_ok "AC6 pre-release" "" prerelease v1.2.3 true
run bash "$script" prerelease v1.2.3 false
expect_equal "AC6 full release exit code" "0" "$rc"
expect_equal "AC6 full release warning" "::warning::Release v1.2.3 was published as a full release; publish releases as pre-releases so they are not shown as the latest release before their artifacts exist." "$out"

step "AC7: dockerhub-secrets"
expect_ok "AC7 both set" "" dockerhub-secrets true true
for pair in "false true" "true false" "false false"; do
    # shellcheck disable=SC2086 # the two words are the two arguments
    expect_error "AC7 ${pair}" "Docker Hub credentials are missing: set DOCKERHUB_USERNAME and DOCKERHUB_TOKEN as secrets of the environment release." dockerhub-secrets $pair
done

step "Issue 78: newest"
newest_repo="${work}/newest"
git init -q -b main "$newest_repo"
git -C "$newest_repo" config user.name test
git -C "$newest_repo" config user.email test@example.com
git -C "$newest_repo" config commit.gpgsign false
git -C "$newest_repo" config tag.gpgsign false
git -C "$newest_repo" commit -q --allow-empty -m first
git -C "$newest_repo" tag v1.0.0
(
    cd "$newest_repo"
    expect_ok "I78-AC1 only tag" "" newest v1.0.0

    for name in v0.9.0 v0.10.0 v1.0.9 v1.0.10; do
        git tag "$name"
    done
    expect_ok "I78-AC1 numeric" "" newest v1.0.10

    for name in v2.0.0-rc.1 v3.0 V9.0.0 v09.0.0 x9.0.0 v1.0.10.1; do
        git tag "$name"
    done
    git branch v99.0.0
    expect_ok "I78-AC1 ignored" "" newest v1.0.10

    expect_error "I78-AC4 absent" "Release tag 'v1.0.11' is not among the fetched tags." newest v1.0.11

    expect_error "I78-AC2 lower patch" "Release tag 'v1.0.9' is older than tag 'v1.0.10'; only the highest version tag can be released." newest v1.0.9
    expect_error "I78-AC2 names the highest" "Release tag 'v0.9.0' is older than tag 'v1.0.10'; only the highest version tag can be released." newest v0.9.0

    git tag v9.9.9
    git switch -q -c side
    git commit -q --allow-empty -m side
    git tag -a v10.0.0 -m "annotated tag on a side branch"
    git switch -q main
    expect_error "I78-AC2 not on main, annotated v1.0.10" "Release tag 'v1.0.10' is older than tag 'v10.0.0'; only the highest version tag can be released." newest v1.0.10
    expect_error "I78-AC2 not on main, annotated v9.9.9" "Release tag 'v9.9.9' is older than tag 'v10.0.0'; only the highest version tag can be released." newest v9.9.9
    expect_ok "I78-AC1 annotated highest" "" newest v10.0.0

    git tag v99999999999999999999.0.0
    expect_error "I78-AC2 long field" "Release tag 'v1.0.0' is older than tag 'v99999999999999999999.0.0'; only the highest version tag can be released." newest v1.0.0
    expect_error "I78-AC2 long field below" "Release tag 'v10.0.0' is older than tag 'v99999999999999999999.0.0'; only the highest version tag can be released." newest v10.0.0
    expect_ok "I78-AC2 ignored passes" "" newest v99999999999999999999.0.0

    for bad in v1.2 'v1.2.3;id' ""; do
        expect_error "I78-AC3 '${bad}'" "$(tag_error "$bad")" newest "$bad"
    done
    expect_error "I78-AC3 line break" "Release tag 'v1.2.3%0A' is not of the form vMAJOR.MINOR.PATCH." newest $'v1.2.3\n'
    expect_error "I78-AC3 no argument" "$(tag_error "")" newest
)

no_tags_repo="${work}/newest-no-tags"
git init -q -b main "$no_tags_repo"
git -C "$no_tags_repo" config user.name test
git -C "$no_tags_repo" config user.email test@example.com
git -C "$no_tags_repo" config commit.gpgsign false
git -C "$no_tags_repo" config tag.gpgsign false
git -C "$no_tags_repo" commit -q --allow-empty -m first
(
    cd "$no_tags_repo"
    expect_error "I78-AC4 no tags" "Release tag 'v1.0.0' is not among the fetched tags." newest v1.0.0
)

step "AC2, AC8: Companion"
companion_out="${work}/companion"
run bash "$script" companion "$version" "$companion_out"
if ((rc != 0)); then
    echo "$out" >&2
fi
expect_equal "AC2 companion exit code" "0" "$rc"
zip_path="${companion_out}/Censio-${version}-win-x64.zip"
expect_equal "AC2 companion result" "$zip_path" "$(last_line)"
expect_equal "AC2 zip list" "Censio.Companion.exe" "$(unzip -Z1 "$zip_path")"
expect_equal "AC2 publish list" "Censio.Companion.exe" "$(find "${companion_out}/publish" -mindepth 1 -printf '%P\n')"
mkdir -p "$dry_run_dir"
cp "$zip_path" "$dry_run_dir/"
dll="src/Censio.Companion/obj/Release/net10.0-windows/win-x64/Censio.Companion.dll"
expect_ok "AC2 check passes" "" check-companion "${companion_out}/publish" "$dll" "$version"
extra="${work}/companion-extra"
mkdir "$extra"
ln "${companion_out}/publish/Censio.Companion.exe" "${extra}/Censio.Companion.exe"
printf 'x' > "${extra}/x.pdb"
expect_error "AC2 extra file" "The Companion publish output holds Censio.Companion.exe, x.pdb, expected Censio.Companion.exe only." check-companion "$extra" "$dll" "$version"
printf 'not part of the bundle' > "${work}/other.dll"
expect_error "AC2 dll not bundled" "Censio.Companion.exe does not bundle the checked Censio.Companion.dll." check-companion "${companion_out}/publish" "${work}/other.dll" "$version"
expect_error "AC8 companion version" "Censio.Companion.exe shows version '${version}', expected '${other_version}'." check-companion "${companion_out}/publish" "$dll" "$other_version"

step "AC3, AC8: server image"
image_name="censio-release-test:${version}"
images+=("$image_name")
run bash "$script" image "$version" "$image_name"
if ((rc != 0)); then
    echo "$out" >&2
fi
expect_equal "AC3 image exit code" "0" "$rc"
image_id="$(last_line)"
expect_equal "AC3 image result" "$(image_id_of "$image_name")" "$image_id"
# The ID is a manifest digest in the containerd image store; the classic store has the config digest instead.
if [[ ! "$image_id" =~ ^sha256:[0-9a-f]{64}$ ]]; then
    fail "AC3 image id" "sha256 and 64 hex digits" "$image_id"
fi
expect_equal "AC3 platform" "linux/amd64" "$(docker image inspect --format '{{.Os}}/{{.Architecture}}' "$image_name")"
expect_error "AC8 image version" "The server image shows version '${version}', expected '${other_version}'." check-image "$image_name" "$other_version"

step "AC3: unhealthy image (E6)"
unhealthy_name="censio-release-test:unhealthy"
images+=("$unhealthy_name")
printf 'FROM %s\nENV CENSIO__DATADIRECTORY=relative\n' "$image_name" | docker build -q -t "$unhealthy_name" - > /dev/null
started=$SECONDS
expect_error "AC3 unhealthy" "The server image did not answer Healthy on /health within 60 s." check-image "$unhealthy_name" "$version"
if ((SECONDS - started > 70)); then
    fail "AC3 unhealthy" "an exited container ends the wait" "$((SECONDS - started)) s"
fi
expect_equal "AC3 containers left" "" "$(docker ps -a --filter name=censio-release-check- --format '{{.Names}}')"

step "AC3: image that exits at once (E6)"
exits_name="censio-release-test:exits"
images+=("$exits_name")
printf 'FROM %s\nENTRYPOINT ["false"]\n' "$image_name" | docker build -q -t "$exits_name" - > /dev/null
expect_error "AC3 exits at once" "The server image did not answer Healthy on /health within 60 s." check-image "$exits_name" "$version"
expect_equal "AC3 containers left after exit" "" "$(docker ps -a --filter name=censio-release-check- --format '{{.Names}}')"

step "AC3: push and promote-image against a local registry"
docker run -d --name "$registry_name" -p 127.0.0.1::5000 "$registry_image" > /dev/null
registry_port="$(docker port "$registry_name" 5000/tcp | sed -n '1p')"
registry_port="${registry_port##*:}"
for _ in $(seq 1 30); do
    if curl -fsS --max-time 2 "http://127.0.0.1:${registry_port}/v2/" > /dev/null 2>&1; then
        break
    fi
    sleep 1
done
registry_repo="127.0.0.1:${registry_port}/censio-test"
archive="${work}/censio-image.tar"
docker save -o "$archive" "$image_name"
expect_error "AC3 wrong image id" "The loaded image is ${image_id}, expected the checked image ${zero_digest}." push "$archive" "$zero_digest" "$registry_repo" "$version"
run bash "$script" push "$archive" "$image_id" "$registry_repo" "$version"
if ((rc != 0)); then
    echo "$out" >&2
fi
expect_equal "AC3 push exit code" "0" "$rc"
pushed_digest="$(last_line)"
expect_equal "AC3 pushed digest" "$(registry_digest "${registry_repo}:${version}")" "$pushed_digest"
pushed_kind="$(docker buildx imagetools inspect "${registry_repo}:${version}" --format '{{.Manifest.MediaType}} {{.Image.OS}}/{{.Image.Architecture}}')"
case "$pushed_kind" in
    "application/vnd.oci.image.manifest.v1+json linux/amd64" | "application/vnd.docker.distribution.manifest.v2+json linux/amd64") ;;
    *) fail "AC3 pushed manifest" "one image manifest for linux/amd64" "$pushed_kind" ;;
esac
echo "info: image ID ${image_id}, pushed digest ${pushed_digest}"

provenance_name="censio-release-test:provenance"
if build_tiny "$provenance_name" --provenance=true 2> /dev/null \
    && [[ "$(docker image inspect --format '{{.Descriptor.mediaType}}' "$provenance_name" 2> /dev/null || true)" == *index* ]]; then
    provenance_archive="${work}/provenance.tar"
    docker save -o "$provenance_archive" "$provenance_name"
    expect_error "AC3 provenance" "${registry_repo}:9.9.9 is not a single linux/amd64 image." push "$provenance_archive" "$(image_id_of "$provenance_name")" "$registry_repo" 9.9.9
else
    skipped+=("AC3 provenance: this Docker builds no attestation index")
fi

expect_error "AC3 promote form" "$(form_error digest)" promote-image "$registry_repo" "not-a-digest"
run bash "$script" promote-image "$registry_repo" "$pushed_digest"
if ((rc != 0)); then
    echo "$out" >&2
fi
expect_equal "AC3 promote exit code" "0" "$rc"
expect_equal "AC3 latest digest" "$pushed_digest" "$(registry_digest "${registry_repo}:latest")"
expect_equal "AC3 version digest kept" "$pushed_digest" "$(registry_digest "${registry_repo}:${version}")"

echo "release-check-test: all cases passed"
for note in "${skipped[@]}"; do
    echo "release-check-test: SKIPPED ${note}"
done

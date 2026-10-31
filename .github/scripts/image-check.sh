#!/usr/bin/env bash
# Builds the server image and checks it the way the acceptance criteria of feature 4 describe.
# Run from the repository root; needs docker, curl, git, dotnet and sudo (for the NAS case).
# Pushes nothing and logs in nowhere. Every failure prints "image-check: <case>: <expected> / <actual>".
set -euo pipefail

dockerfile="src/Censio.Server/Dockerfile"
work="${RUNNER_TEMP:-$(mktemp -d)}"
containers=()
declare -A started
volumes=()
images=()
dirs=()
nas_dir=""

cleanup()
{
    local name
    for name in "${containers[@]}"; do
        docker rm -f "$name" > /dev/null 2>&1 || true
    done
    for name in "${volumes[@]}"; do
        docker volume rm -f "$name" > /dev/null 2>&1 || true
    done
    for name in "${images[@]}"; do
        docker image rm -f "$name" > /dev/null 2>&1 || true
    done
    for name in "${dirs[@]}"; do
        rm -rf "$name"
    done
    if [[ -n "$nas_dir" ]]; then
        sudo rm -rf "$nas_dir"
    fi
}
trap cleanup EXIT

fail()
{
    local case_name="$1"
    local expected="$2"
    local actual="$3"

    echo "image-check: ${case_name}: ${expected} / ${actual}" >&2
    exit 1
}

step()
{
    echo "== $1"
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

inspect_image()
{
    local image="$1"
    local format="$2"

    docker image inspect --format "$format" "$image"
}

inspect_container()
{
    local name="$1"
    local format="$2"

    docker inspect --format "$format" "$name"
}

start_container()
{
    local name="$1"
    shift
    containers+=("$name")
    started["$name"]=$SECONDS
    docker run -d --name "$name" "$@" > /dev/null
}

# Returns 1 when the URL does not answer 200 within 60 s.
poll_http()
{
    local url="$1"
    local deadline=$((SECONDS + 60))

    while ((SECONDS < deadline)); do
        if curl -fsS --max-time 3 -o /dev/null "$url" 2> /dev/null; then
            return 0
        fi
        sleep 2
    done
    return 1
}

wait_http()
{
    local case_name="$1"
    local url="$2"

    if ! poll_http "$url"; then
        fail "$case_name" "200 from ${url} within 60 s" "no answer"
    fi
}

wait_healthy()
{
    local case_name="$1"
    local name="$2"
    local status="none"
    local deadline=$((started["$name"] + 60))

    while ((SECONDS < deadline)); do
        status="$(inspect_container "$name" '{{.State.Health.Status}}')"
        if [[ "$status" == "healthy" ]]; then
            return 0
        fi
        sleep 2
    done
    fail "$case_name" "healthy within 60 s" "$status"
}

image_files()
{
    local image="$1"

    docker run --rm --entrypoint find "$image" /app -type f | sort
}

# Builds an image that must fail. The build output has to hold the given line as the executed RUN command printed it
# (BuildKit prefixes it with "#<step> <seconds> "): the same text also appears in the Dockerfile's own RUN lines,
# which BuildKit echoes as step headers and in its error excerpt, so only a prefixed whole line proves the check fired.
expect_build_failure()
{
    local case_name="$1"
    local line="$2"
    shift 2
    local output

    if output="$(docker build --progress=plain "$@" 2>&1)"; then
        fail "$case_name" "docker build fails" "docker build succeeded"
    fi
    if ! sed -E 's/^#[0-9]+ [0-9.]+ //' <<< "$output" | grep -Fxq -- "$line"; then
        echo "$output" >&2
        fail "$case_name" "build output line '${line}'" "line missing"
    fi
}

step "AC1 clean: build right after checkout"
clean_image="censio-check:clean"
images+=("$clean_image")
docker build -f "$dockerfile" -t "$clean_image" .
expect_equal "AC1 clean" "amd64/linux" "$(inspect_image "$clean_image" '{{.Architecture}}/{{.Os}}')"
clean_files="$(image_files "$clean_image")"
if [[ -z "$clean_files" ]]; then
    fail "AC1 clean" "files below /app" "none"
fi

step "AC9: labels"
from_line="$(grep -E '^FROM ' "$dockerfile" | tail -n 1)"
from_ref="$(awk '{print $2}' <<< "$from_line")"
expected_name="${from_ref%%@*}"
expected_digest="${from_ref#*@}"
label_prefix="org.opencontainers.image"
expect_equal "AC9 base.name" "$expected_name" "$(inspect_image "$clean_image" "{{index .Config.Labels \"${label_prefix}.base.name\"}}")"
expect_equal "AC9 base.digest" "$expected_digest" "$(inspect_image "$clean_image" "{{index .Config.Labels \"${label_prefix}.base.digest\"}}")"
expect_equal "AC9 licenses" "MIT" "$(inspect_image "$clean_image" "{{index .Config.Labels \"${label_prefix}.licenses\"}}")"
description="$(inspect_image "$clean_image" "{{index .Config.Labels \"${label_prefix}.description\"}}")"
if [[ -z "$description" ]]; then
    fail "AC9 description" "a description label" "empty"
fi

step "AC6 config: port environment and exposed port"
expect_equal "AC6 env" "true" "$(inspect_image "$clean_image" '{{range .Config.Env}}{{if eq . "ASPNETCORE_HTTP_PORTS=8080"}}true{{end}}{{end}}')"
expect_equal "AC6 exposed" "true" "$(inspect_image "$clean_image" '{{if index .Config.ExposedPorts "8080/tcp"}}true{{end}}')"

step "AC5 config: health check timings"
expect_equal "AC5 config" "10000000000/3000000000/30000000000/3" \
    "$(inspect_image "$clean_image" '{{.Config.Healthcheck.Interval}}/{{.Config.Healthcheck.Timeout}}/{{.Config.Healthcheck.StartPeriod}}/{{.Config.Healthcheck.Retries}}')"

step "AC2: container answers"
volume="censio-check-config"
volumes+=("$volume")
docker volume create "$volume" > /dev/null
main="censio-check-main"
start_container "$main" -p 127.0.0.1:18080:8080 -v "${volume}:/config" "$clean_image"
wait_http "AC2" "http://127.0.0.1:18080/health"
expect_equal "AC2 health body" "Healthy" "$(curl -fsS --max-time 3 http://127.0.0.1:18080/health)"
page="$(curl -fsS --max-time 5 http://127.0.0.1:18080/)"
if [[ "$page" != *"Version 0.0.0"* ]]; then
    fail "AC2 start page" "contains 'Version 0.0.0'" "missing"
fi
script_path="$(grep -oE '_framework/blazor\.web[^"]*\.js' <<< "$page" | head -n 1 || true)"
if [[ -z "$script_path" ]]; then
    fail "AC2 start page" "a _framework/blazor.web script path" "missing"
fi
curl -fsS --max-time 5 -o /dev/null "http://127.0.0.1:18080/${script_path}" \
    || fail "AC2 script" "200 for ${script_path}" "request failed"
if ! docker logs "$main" 2>&1 | grep -E 'Now listening on:.*8080' > /dev/null; then
    fail "AC2 log" "'Now listening on:' with 8080" "missing"
fi

step "AC3: user"
expect_equal "AC3 id" "1654" "$(docker exec "$main" id -u)"
expect_equal "AC3 pid 1" "1654" "$(docker exec "$main" stat -c %u /proc/1)"
expect_equal "AC3 Config.User" "1654:1654" "$(inspect_image "$clean_image" '{{.Config.User}}')"

step "AC4: /config writable"
docker exec "$main" touch /config/check || fail "AC4" "touch /config/check succeeds" "failed"

step "AC5: container turns healthy"
wait_healthy "AC5" "$main"

step "AC11: docker stop"
stop_start=$SECONDS
docker stop "$main" > /dev/null
stop_seconds=$((SECONDS - stop_start))
if ((stop_seconds > 10)); then
    fail "AC11" "stop within 10 s" "${stop_seconds} s"
fi
expect_equal "AC11 exit code" "0" "$(inspect_container "$main" '{{.State.ExitCode}}')"
docker rm -f "$main" > /dev/null

step "AC6: other port"
port_container="censio-check-port"
start_container "$port_container" -e ASPNETCORE_HTTP_PORTS=9090 -p 127.0.0.1:19090:9090 "$clean_image"
wait_http "AC6 9090" "http://127.0.0.1:19090/health"
wait_healthy "AC6 9090" "$port_container"
docker rm -f "$port_container" > /dev/null
multi_container="censio-check-multi"
start_container "$multi_container" -e "ASPNETCORE_HTTP_PORTS=8080;8081" "$clean_image"
wait_healthy "AC6 two ports" "$multi_container"
docker rm -f "$multi_container" > /dev/null

step "AC13: other UID with a bind mount"
nas_dir="${work}/nas"
sudo install -d -o 99 -g 100 "$nas_dir"
nas_container="censio-check-nas"
start_container "$nas_container" --user 99:100 -p 127.0.0.1:18082:8080 -v "${nas_dir}:/config" "$clean_image"
nas_ready=true
poll_http "http://127.0.0.1:18082/health" || nas_ready=false
echo "-- container log (AC13)"
docker logs "$nas_container" 2>&1 || true
if [[ "$nas_ready" != "true" ]]; then
    fail "AC13" "200 from /health as 99:100" "no answer"
fi
expect_equal "AC13 id" "99" "$(docker exec "$nas_container" id -u)"
docker exec "$nas_container" touch /config/check || fail "AC13" "touch /config/check as 99:100 succeeds" "failed"
expect_equal "AC13 entrypoint" '["dotnet","Censio.Server.dll"]' "$(inspect_image "$clean_image" '{{json .Config.Entrypoint}}')"
scripts="$(docker run --rm --entrypoint find "$clean_image" /app -name '*.sh')"
if [[ -n "$scripts" ]]; then
    fail "AC13" "no *.sh under /app" "$scripts"
fi
docker rm -f "$nas_container" > /dev/null

step "AC7: VERSION build argument"
version_image="censio-check:version"
images+=("$version_image")
docker build -f "$dockerfile" --build-arg VERSION=1.2.3 -t "$version_image" .
version_container="censio-check-version"
start_container "$version_container" -p 127.0.0.1:18083:8080 "$version_image"
wait_http "AC7" "http://127.0.0.1:18083/health"
version_page="$(curl -fsS --max-time 5 http://127.0.0.1:18083/)"
if [[ "$version_page" != *"Version 1.2.3"* ]]; then
    fail "AC7 start page" "contains 'Version 1.2.3'" "missing"
fi
docker rm -f "$version_container" > /dev/null
semver_message="VERSION is not a semantic version: "
expect_build_failure "AC7 metacharacters" "${semver_message}1.0;touch /pwned" -f "$dockerfile" --build-arg 'VERSION=1.0;touch /pwned' --target build .
expect_build_failure "AC7 second property" "${semver_message}1.0;Foo=bar" -f "$dockerfile" --build-arg 'VERSION=1.0;Foo=bar' --target build .
expect_build_failure "AC7 two components" "${semver_message}1.0" -f "$dockerfile" --build-arg 'VERSION=1.0' --target build .
expect_build_failure "AC7 line break" "${semver_message}1.0.0" -f "$dockerfile" --build-arg $'VERSION=1.0.0\n;Foo=bar' --target build .

step "AC10: build without RequiresAspNetWebAssets fails"
copy="${work}/image-check-copy"
dirs+=("$copy")
rm -rf "$copy"
mkdir -p "$copy"
git ls-files -z | xargs -0 -I{} cp --parents {} "$copy"
sed -i '/RequiresAspNetWebAssets/d' "${copy}/src/Censio.Server/Censio.Server.csproj"
if grep -q 'RequiresAspNetWebAssets' "${copy}/src/Censio.Server/Censio.Server.csproj"; then
    fail "AC10" "RequiresAspNetWebAssets removed from the copy" "still present"
fi
expect_build_failure "AC10" 'publish output lacks wwwroot/_framework/blazor.web*.js (see docs/decisions/0007)' -f "${copy}/${dockerfile}" "$copy"

step "AC1 dirty: build after a local build and publish"
dotnet build Censio.slnx -c Release
dotnet publish src/Censio.Server/Censio.Server.csproj -c Release -o publish
dirty_image="censio-check:dirty"
images+=("$dirty_image")
docker build --no-cache -f "$dockerfile" -t "$dirty_image" .
expect_equal "AC1 dirty platform" "amd64/linux" "$(inspect_image "$dirty_image" '{{.Architecture}}/{{.Os}}')"
dirty_files="$(image_files "$dirty_image")"
expect_equal "AC1 dirty file list" "$clean_files" "$dirty_files"

echo "image-check: all cases passed"

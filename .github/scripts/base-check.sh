#!/usr/bin/env bash
# Checks that the pinned .NET Alpine base images of the server Dockerfile are not frozen: it fails when MCR
# publishes a newer .NET patch of a pinned image only on a newer Alpine minor, i.e. when the pinned
# "-alpine<X.Y>" tags stopped receiving patches and Dependabot proposes nothing any more (issue 65).
# Usage: base-check.sh [dockerfile]   (default: src/Censio.Server/Dockerfile; run from the repository root)
# Needs bash, curl and jq. Anonymous read of the public MCR tag list; no secret, nothing is written.
# Prints one line per FROM line; failures go to stderr as "::error::base-check: <subject>: ..." annotations.
set -euo pipefail

dockerfile="${1:-src/Censio.Server/Dockerfile}"
registry_host="mcr.microsoft.com"
tag_pattern='^([0-9]+)\.([0-9]+)\.([0-9]+)-alpine([0-9]+)\.([0-9]+)(-[a-z0-9-]+)$'
image_pattern='^mcr\.microsoft\.com/dotnet/([a-z0-9-]+)$'
bom=$'\xEF\xBB\xBF'

declare -A tag_cache
declare -A fetch_failed
failed=0
tags=""
fetch_error=""

cannot_check()
{
    local subject="$1"
    local reason="$2"

    echo "::error::base-check: ${subject}: cannot check: ${reason}" >&2
    failed=1
}

# Reads the tag list of one repository into "tags" (one matching-or-not tag per line); sets "fetch_error" on failure.
fetch_tags()
{
    local repository="$1"
    local response

    fetch_error=""
    tags=""

    if [[ -n "${fetch_failed[$repository]:-}" ]]; then
        fetch_error="${fetch_failed[$repository]}"
        return 1
    fi

    if [[ -n "${tag_cache[$repository]:-}" ]]; then
        tags="${tag_cache[$repository]}"
        return 0
    fi

    if ! response=$(curl -fsS --proto '=https' --proto-redir '=https' --max-time 30 --max-filesize 8M --retry 2 \
        "https://${registry_host}/v2/dotnet/${repository}/tags/list"); then
        fetch_failed[$repository]="registry request failed"
        fetch_error="${fetch_failed[$repository]}"
        return 1
    fi

    if ! tags=$(printf '%s' "$response" | jq -r \
        '.tags | if type == "array" then .[] | strings | select(test("\n") | not) else error("no tag list") end' 2>/dev/null) \
        || [[ -z "$tags" ]]; then
        tags=""
        fetch_failed[$repository]="registry answer holds no tag list"
        fetch_error="${fetch_failed[$repository]}"
        return 1
    fi

    tag_cache[$repository]="$tags"
    return 0
}

# Checks one FROM line; "subject" for an unparsable line is "line <n>".
check_from()
{
    local line_number="$1"
    local line="$2"
    local reference=""
    local word
    local -a words
    local image
    local tag
    local repository
    local subject="line ${line_number}"

    read -r -a words <<< "$line"
    for word in "${words[@]:1}"; do
        if [[ "$word" != --* ]]; then
            reference="$word"
            break
        fi
    done

    reference="${reference%%@*}"
    image="${reference%%:*}"
    tag="${reference#*:}"

    if [[ "$reference" != *:* || ! "$image" =~ $image_pattern ]]; then
        cannot_check "$subject" "not a full-version ${registry_host}/dotnet Alpine tag"
        return
    fi

    repository="${BASH_REMATCH[1]}"

    if [[ ! "$tag" =~ $tag_pattern ]]; then
        cannot_check "$subject" "not a full-version ${registry_host}/dotnet Alpine tag"
        return
    fi

    local major="${BASH_REMATCH[1]}"
    local minor="${BASH_REMATCH[2]}"
    local alpine_major="${BASH_REMATCH[4]}"
    local alpine_minor="${BASH_REMATCH[5]}"
    local suffix="${BASH_REMATCH[6]}"

    subject="${image}:${tag}"

    if ! fetch_tags "$repository"; then
        cannot_check "$subject" "$fetch_error"
        return
    fi

    local match_pattern="^${major}\\.${minor}\\.([0-9]{1,6})-alpine([0-9]{1,6})\\.([0-9]{1,6})${suffix}\$"
    local found=0
    local newest_pinned=-1
    local best_patch=-1
    local best_major=-1
    local best_minor=-1
    local candidate
    local candidate_patch
    local candidate_major
    local candidate_minor

    while IFS= read -r candidate; do
        if [[ ! "$candidate" =~ $match_pattern ]]; then
            continue
        fi

        candidate_patch=$((10#${BASH_REMATCH[1]}))
        candidate_major=$((10#${BASH_REMATCH[2]}))
        candidate_minor=$((10#${BASH_REMATCH[3]}))

        if ((candidate_major == 10#$alpine_major && candidate_minor == 10#$alpine_minor)); then
            if [[ "$candidate" == "$tag" ]]; then
                found=1
            fi

            if ((candidate_patch > newest_pinned)); then
                newest_pinned=$candidate_patch
            fi
        elif ((candidate_major > 10#$alpine_major || (candidate_major == 10#$alpine_major && candidate_minor > 10#$alpine_minor))); then
            if ((candidate_patch > best_patch
                || (candidate_patch == best_patch
                    && (candidate_major > best_major || (candidate_major == best_major && candidate_minor > best_minor))))); then
                best_patch=$candidate_patch
                best_major=$candidate_major
                best_minor=$candidate_minor
            fi
        fi
    done <<< "$tags"

    if ((found == 0)); then
        cannot_check "$subject" "tag not in the registry's list"
        return
    fi

    if ((best_patch > newest_pinned)); then
        echo "::error::base-check: ${subject}: alpine${alpine_major}.${alpine_minor} ends at ${major}.${minor}.${newest_pinned}, ${major}.${minor}.${best_patch}-alpine${best_major}.${best_minor}${suffix} exists only on a later Alpine minor; move both FROM lines and the base labels to alpine${best_major}.${best_minor} (docs/CONTRIBUTING.md, Automated checks)" >&2
        failed=1
        return
    fi

    echo "base-check: ${subject}: newest ${major}.${minor}.${newest_pinned} on alpine${alpine_major}.${alpine_minor}; no newer patch only on a later Alpine minor"
}

if [[ ! -f "$dockerfile" ]]; then
    cannot_check "$dockerfile" "file not found"
    exit 1
fi

from_lines=0
line_number=0

while IFS= read -r line || [[ -n "$line" ]]; do
    line_number=$((line_number + 1))
    line="${line%$'\r'}"

    if ((line_number == 1)); then
        line="${line#"$bom"}"
    fi

    if [[ "$line" =~ ^[[:space:]]*[Ff][Rr][Oo][Mm]([[:space:]]|$) ]]; then
        from_lines=$((from_lines + 1))
        check_from "$line_number" "$line"
    fi
done < "$dockerfile"

if ((from_lines == 0)); then
    cannot_check "$dockerfile" "no FROM line"
fi

exit "$failed"

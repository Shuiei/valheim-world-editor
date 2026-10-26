#!/usr/bin/env bash
# Compares the mods each add-on follows (addons/<Name>/mods.json: the mod's Thunderstore packages, each with the
# version the add-on was last checked against) with their latest version on Thunderstore, one line for each mod.
# With --issues, opens a GitHub issue (label "add-on mod update") for each mod that has a newer version, to check the
# add-on against it: one open issue for an add-on and a mod, brought up to date when a still newer version comes out,
# and none again for a version whose issue was closed. Every add-on needs a mods.json.
# Usage: tools/addon-mods.sh [--issues]   (needs curl and jq; with --issues, gh with Issues read and write)
set -euo pipefail
repo=$(cd "$(dirname "$0")/.." && pwd)
issues=false
[ "${1:-}" = "--issues" ] && issues=true
label="add-on mod update"

if $issues; then
  gh label create "$label" --color c5def5 --description "A mod an add-on follows has a new version on Thunderstore" --force > /dev/null
  # Every issue this script made, open or closed, read once.
  known=$(gh issue list --label "$label" --state all --limit 1000 --json number,state,body)
fi

# Opens or updates the issue for an add-on and a mod that has a newer version.
report() {
  local name=$1 version=$2 package=$3 owner=$4 pkg=$5 followed=$6 latest=$7
  local marker="<!-- addon-mod: $name $package -->" seen="<!-- latest: $latest -->"
  local page="https://thunderstore.io/c/valheim/p/$owner/$pkg/"
  local title="${pkg//_/ } $latest is out: check the $name add-on (follows $followed)"
  local body
  body="**[${pkg//_/ }]($page) $latest** is on Thunderstore ([changelog](${page}changelog/)). The $name add-on ($version)
follows $followed.

Try the add-on with $latest (its tests, and the editor with the mod on a world that uses it, saved and live):
- It still works: set the mod's version in \`addons/$name/mods.json\` to $latest, with the \`TestMod\` item that
  downloads the mod for the tests, if there is one, and the version the add-on's README says it was checked on.
- It does not: fix the add-on (a new version of it, \`addons/$name/VERSION\`) and do the same.

Either pull request closes this issue (\`Closes #<this issue>\`).

$marker
$seen"
  local open
  open=$(jq -r --arg m "$marker" 'first(.[] | select(.state == "OPEN" and (.body | contains($m)))) | "\(.number) \(.body)"' <<< "$known")
  if [ -n "$open" ]; then
    local number=${open%% *}
    if [[ $open == *"$seen"* ]]; then
      echo "  issue #$number is open"
      return
    fi

    gh issue edit "$number" --title "$title" --body "$body" > /dev/null
    gh issue comment "$number" --body "${pkg//_/ } $latest is out now: the issue is about it." > /dev/null
    echo "  issue #$number updated for $latest"
    return
  fi

  local closed
  closed=$(jq -r --arg m "$marker" --arg s "$seen" 'first(.[] | select(.state == "CLOSED" and (.body | contains($m)) and (.body | contains($s)))) | .number' <<< "$known")
  if [ -n "$closed" ]; then
    echo "  issue #$closed for $latest was closed"
    return
  fi

  echo "  issue $(gh issue create --title "$title" --body "$body" --label "$label") opened"
}

summary=${GITHUB_STEP_SUMMARY:-/dev/null}
printf '| Add-on | Mod | Followed | On Thunderstore |\n|---|---|---|---|\n' >> "$summary"
for dir in "$repo"/addons/*/; do
  name=$(basename "$dir")
  [ -f "$dir/mods.json" ] || { echo "addons/$name has no mods.json (the mods it follows and their versions)" >&2; exit 1; }
  version=$(tr -d '[:space:]' < "$dir/VERSION")
  # Read first: a mods.json jq cannot read stops the script here, not as an add-on with no mods.
  mods=$(jq -er '.mods[] | [.package, .version] | @tsv' "$dir/mods.json")
  while IFS=$'\t' read -r package followed; do
    # A Thunderstore package's full name is <owner>-<name>; owners have no dash.
    owner=${package%%-*} pkg=${package#*-}
    latest=$(curl -fsS --retry 3 "https://thunderstore.io/api/experimental/package/$owner/$pkg/" | jq -er '.latest.version_number')
    if [ "$latest" = "$followed" ]; then
      state="up to date"
    elif [ "$(printf '%s\n%s\n' "$followed" "$latest" | sort -V | tail -n 1)" = "$latest" ]; then
      state="newer"
    else
      state="older than followed (the mod's latest was taken down?)"
    fi

    echo "$name: $package $followed, Thunderstore $latest: $state"
    echo "| $name | $package | $followed | $latest${state:+ ($state)} |" >> "$summary"
    if $issues && [ "$state" = "newer" ]; then
      report "$name" "$version" "$package" "$owner" "$pkg" "$followed" "$latest"
    fi
  done <<< "$mods"
done

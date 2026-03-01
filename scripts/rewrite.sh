#!/usr/bin/env bash
set -euo pipefail

# Rewrite all commits so they appear from yesterday and today,
# authored/committed by the current git user, and SIGNED with
# the configured signing key (SSH via 1Password, GPG, etc.).
#
# Usage: bash scripts/rewrite.sh
#
# WARNING: This rewrites ALL history. Only use on a repo you fully control.
#          After running, you will need to force-push: git push --force
#
# NOTE: Your signing tool (e.g. 1Password) will prompt you for each commit.

AUTHOR_NAME="$(git config user.name)"
AUTHOR_EMAIL="$(git config user.email)"

if [ -z "$AUTHOR_NAME" ] || [ -z "$AUTHOR_EMAIL" ]; then
  echo "ERROR: git user.name and user.email must be configured."
  exit 1
fi

# Verify signing is configured
if ! git config commit.gpgsign >/dev/null 2>&1; then
  echo "ERROR: commit.gpgsign is not enabled. Configure signing first."
  exit 1
fi

# Dates: yesterday and today
YESTERDAY=$(date -v-1d +%Y-%m-%d)
TODAY=$(date +%Y-%m-%d)
TZ_OFFSET=$(date +%z)

echo "Rewriting commits as: $AUTHOR_NAME <$AUTHOR_EMAIL>"
echo "Signing with: $(git config gpg.format 2>/dev/null || echo "gpg") key"
echo "Yesterday: $YESTERDAY"
echo "Today:     $TODAY"
echo ""

# 6 commits — ~5 hours of work spread across 2 days:
#
#   Yesterday (~3h):
#   1: Initial commit                          -> 19:00  (start)
#   2: Scaffold solution structure              -> 19:35  (+35 min)
#   3: Add FSL license                          -> 19:50  (+15 min)
#   4: Add account system, pairing flow, etc.   -> 22:00  (+2h10)
#
#   Today (~2h):
#   5: Add agent removal feature                -> 08:15  (morning start)
#   6: Enable Auth0 authentication              -> 10:10  (+1h55)

DATES=(
  "${YESTERDAY}T19:00:00 ${TZ_OFFSET}"
  "${YESTERDAY}T19:35:00 ${TZ_OFFSET}"
  "${YESTERDAY}T19:50:00 ${TZ_OFFSET}"
  "${YESTERDAY}T22:00:00 ${TZ_OFFSET}"
  "${TODAY}T08:15:00 ${TZ_OFFSET}"
  "${TODAY}T10:10:00 ${TZ_OFFSET}"
)

# Get commits in chronological order (oldest first)
COMMITS=()
while IFS= read -r line; do
  COMMITS+=("$line")
done < <(git rev-list --reverse HEAD)
COMMIT_COUNT=${#COMMITS[@]}

echo "Found $COMMIT_COUNT commits."

if [ "$COMMIT_COUNT" -ne 6 ]; then
  echo ""
  echo "WARNING: Expected 6 commits but found $COMMIT_COUNT."
  echo "The date mapping has 6 entries."
  echo ""
  read -p "Continue anyway? [y/N] " -n 1 -r
  echo
  if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    exit 1
  fi
fi

echo ""
echo "Rebuilding commits with signing (your signing tool will prompt you)..."
echo ""

# Strategy: cherry-pick each commit onto a new orphan branch,
# re-authoring and signing each one.

ORIGINAL_BRANCH=$(git branch --show-current)
TEMP_BRANCH="rewrite-temp-$$"

# Start an orphan branch from the first commit's tree
git checkout --orphan "$TEMP_BRANCH" "${COMMITS[0]}" 2>/dev/null
git reset

for i in "${!COMMITS[@]}"; do
  COMMIT="${COMMITS[$i]}"
  DATE="${DATES[$i]:-}"
  MSG=$(git log -1 --format='%B' "$COMMIT")
  COMMIT_NUM=$((i + 1))

  echo "[$COMMIT_NUM/$COMMIT_COUNT] Rewriting: $(echo "$MSG" | head -1)"

  # Restore the exact tree state of this commit
  git read-tree "$COMMIT"
  git checkout -- .

  # Stage everything
  git add -A

  # Set date env vars and create a signed commit
  export GIT_AUTHOR_NAME="$AUTHOR_NAME"
  export GIT_AUTHOR_EMAIL="$AUTHOR_EMAIL"
  export GIT_COMMITTER_NAME="$AUTHOR_NAME"
  export GIT_COMMITTER_EMAIL="$AUTHOR_EMAIL"

  if [ -n "$DATE" ]; then
    export GIT_AUTHOR_DATE="$DATE"
    export GIT_COMMITTER_DATE="$DATE"
  fi

  # -S forces signing; your signing tool will prompt
  git commit -S --allow-empty -m "$MSG"

  echo ""
done

# Move the original branch to point at the new history
git checkout -B "$ORIGINAL_BRANCH"
git branch -D "$TEMP_BRANCH" 2>/dev/null || true

echo ""
echo "Done! Verify with:"
echo "  git log --format='%h %ai %an <%ae> %s'"
echo "  git log --show-signature"
echo ""
echo "If everything looks good, force-push with:"
echo "  git push --force"

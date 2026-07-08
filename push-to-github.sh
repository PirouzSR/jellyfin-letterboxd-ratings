#!/usr/bin/env bash
# One-shot publish script. Requires the GitHub CLI (`gh auth login` first).
# Usage: ./push-to-github.sh [repo-name]
set -euo pipefail
REPO_NAME="${1:-jellyfin-plugin-letterboxd}"

gh repo create "$REPO_NAME" --public --source=. --remote=origin \
  --description "Jellyfin plugin: Letterboxd star ratings with logo badge" --push

# Push the release tag -> CI builds the zip, creates the GitHub Release,
# and writes manifest.json (the Jellyfin plugin repository file) to main.
git push origin v1.0.0.0

OWNER=$(gh api user -q .login)
echo
echo "Done. Once the Actions run finishes (~2 min), add this URL in"
echo "Jellyfin -> Dashboard -> Plugins -> Repositories:"
echo
echo "  https://raw.githubusercontent.com/${OWNER}/${REPO_NAME}/main/manifest.json"

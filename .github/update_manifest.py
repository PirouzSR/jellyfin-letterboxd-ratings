#!/usr/bin/env python3
"""Regenerates manifest.json (Jellyfin plugin repository format) after a release.

Jellyfin's plugin catalog consumes a manifest.json listing plugins and their
versions; each version entry points at a downloadable zip (the GitHub release
asset) plus its MD5 checksum.
"""
import argparse
import hashlib
import json
import os
from datetime import datetime, timezone

GUID = "b8f7c1e2-4a3d-4e5f-9c6b-7d8e9f0a1b2c"
TARGET_ABI = "10.11.0.0"

parser = argparse.ArgumentParser()
parser.add_argument("--zip", required=True)
parser.add_argument("--tag", required=True)   # e.g. v1.0.0.0
parser.add_argument("--repo", required=True)  # e.g. user/jellyfin-plugin-letterboxd
args = parser.parse_args()

version = args.tag.lstrip("v")
# Jellyfin expects four-part versions.
while version.count(".") < 3:
    version += ".0"

with open(args.zip, "rb") as f:
    checksum = hashlib.md5(f.read()).hexdigest()

source_url = (
    f"https://github.com/{args.repo}/releases/download/{args.tag}/"
    + os.path.basename(args.zip)
)

manifest_path = "manifest.json"
if os.path.exists(manifest_path):
    with open(manifest_path) as f:
        manifest = json.load(f)
else:
    manifest = [{
        "guid": GUID,
        "name": "Letterboxd Ratings",
        "overview": "Show Letterboxd community star ratings on your movies.",
        "description": (
            "Fetches the Letterboxd community star rating for every movie in "
            "your library (matched via TMDB or IMDb ID), displays it as a "
            "badge with the Letterboxd logo next to your IMDb and critic "
            "ratings, and optionally appends it to the movie description."
        ),
        "owner": args.repo.split("/")[0],
        "category": "Metadata",
        "imageUrl": "",
        "versions": [],
    }]

entry = {
    "version": version,
    "changelog": f"Release {args.tag}",
    "targetAbi": TARGET_ABI,
    "sourceUrl": source_url,
    "checksum": checksum,
    "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}

versions = [v for v in manifest[0]["versions"] if v["version"] != version]
versions.insert(0, entry)
manifest[0]["versions"] = versions

with open(manifest_path, "w") as f:
    json.dump(manifest, f, indent=2)
    f.write("\n")

print(f"manifest.json updated: {version} -> {source_url} (md5 {checksum})")

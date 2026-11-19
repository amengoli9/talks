"""Compare versioned package URLs across GitHub, build and image inventories."""
import csv
import json
import os
from itertools import combinations
from pathlib import Path

root = Path(".artifacts/sbom")
sources = {
    "GitHub (default branch)": root / "github.spdx.json",
    "Microsoft (publish)": root / "microsoft/_manifest/spdx_2.2/manifest.spdx.json",
    "CycloneDX (.NET)": root / "babylog-app.cdx.json",
    "Syft (standard)": root / "babylog-container-standard.cdx.json",
    "Syft (chiseled)": root / "babylog-container-chiseled.cdx.json",
}

def inventory(path):
    doc = json.loads(path.read_text(encoding="utf-8-sig"))
    doc = doc.get("sbom", doc)
    if "spdxVersion" in doc:
        entries = [(p["name"], p.get("versionInfo"), next((r["referenceLocator"]
                    for r in p.get("externalRefs", []) if r["referenceType"].lower() == "purl"), None))
                   for p in doc["packages"]]
    else:
        entries = [(p["name"], p.get("version"), p.get("purl")) for p in doc["components"]]
    packages, unidentified = set(), []
    for name, version, purl in entries:
        if not purl:
            unidentified.append((name, version))
            continue
        key = purl.split("?", 1)[0].split("#", 1)[0]
        if "@" not in key and version:
            key += "@" + version
        if "@" not in key:
            unidentified.append((name, version))
            continue
        if key.startswith("pkg:nuget/"):
            key = key.lower()
        packages.add(key)
    return packages, unidentified

sets = {}
with (root / "unmatched-identifiers.csv").open("w", newline="") as f:
    writer = csv.writer(f)
    writer.writerow(["source", "name", "version"])
    for source, path in sources.items():
        sets[source], missing = inventory(path)
        writer.writerows((source, *row) for row in missing)
with (root / "package-matrix.csv").open("w", newline="") as f:
    writer = csv.writer(f)
    writer.writerow(["package@version", *sets])
    for key in sorted(set.union(*sets.values())):
        writer.writerow([key, *(int(key in values) for values in sets.values())])
with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as f:
    print("## NuGet package comparison\n", file=f)
    print("GitHub covers the repository default branch. Image inventories also include OS packages. "
          "See package-matrix.csv for all ecosystems and unmatched-identifiers.csv for missing identifiers.\n", file=f)
    print("| A | B | Both | Only A | Only B |\n|---|---|---:|---:|---:|", file=f)
    for a, b in combinations(sets, 2):
        left, right = ({p for p in sets[s] if p.startswith("pkg:nuget/")} for s in (a, b))
        print(f"| {a} | {b} | {len(left & right)} | {len(left-right)} | {len(right-left)} |", file=f)

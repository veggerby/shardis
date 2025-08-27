#!/usr/bin/env python3
"""Check adaptive vs fixed paging allocation delta.

Parses BenchmarkDotNet JSON output for AdaptivePagingAllocationsBenchmarks.
Fails (when enabled) if adaptive allocations exceed fixed allocations by more than the
percentage threshold (env ADAPTIVE_ALLOC_MAX_PCT, default 20) **and** the adaptive
allocation per operation is above a minimum absolute size (env ADAPTIVE_ALLOC_MIN_BYTES, default 4096).

The minimum absolute guard avoids noisy failures when absolute allocations are tiny.

Currently the CI workflow treats failures as advisory (exit code is swallowed). After
stabilising a few runs, remove the shell fallback to make the job fail on regression.

Outputs a short markdown report to stdout and writes ADAPTIVE-ALLOC-DELTA.md for artifact upload.
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path

THRESH_ENV = "ADAPTIVE_ALLOC_MAX_PCT"
DEFAULT_THRESH = 20.0
MIN_BYTES_ENV = "ADAPTIVE_ALLOC_MIN_BYTES"
DEFAULT_MIN_BYTES = 4096

def find_report_files(root: Path):
    for p in root.rglob("AdaptivePagingAllocationsBenchmarks-report*.json"):
        yield p

def load_allocations(report_path: Path):
    data = json.loads(report_path.read_text())
    # BenchmarkDotNet JSON structure: { "Benchmarks": [ { "Method": "Fixed", "Statistics": { ... } } ] }
    benches = data.get("Benchmarks") or data.get("Benchmarks", [])
    fixed_alloc = None
    adaptive_alloc = None
    for b in benches:
        method = (b.get("Method") or b.get("DisplayInfo") or "").lower()
        stats = b.get("Statistics") or {}
        # Prefer AllocatedBytes / Operations if provided, else AllocatedB (older exporter key)
        allocated_bytes = stats.get("AllocatedBytes")
        operations = stats.get("Operations") or 1
        if allocated_bytes is not None:
            per_op = allocated_bytes / operations if operations else float("nan")
        else:
            per_op = stats.get("AllocatedB")  # already per op
        if per_op is None:
            continue
        if "fixed" in method:
            fixed_alloc = per_op
        elif "adaptive" in method:
            adaptive_alloc = per_op
    return fixed_alloc, adaptive_alloc

def main():
    artifacts_root = Path("benchmarks") / "BenchmarkDotNet.Artifacts"
    if not artifacts_root.exists():
        print("::warning::Artifacts root not found; skipping allocation check")
        return 0
    reports = list(find_report_files(artifacts_root))
    if not reports:
        print("::warning::No allocation benchmark JSON reports found; skipping")
        return 0
    # Use the most recent (sorted by mtime)
    reports.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    fixed_alloc = adaptive_alloc = None
    for rpt in reports:
        f,a = load_allocations(rpt)
        if f is not None and a is not None:
            fixed_alloc, adaptive_alloc = f,a
            report_path = rpt
            break
    if fixed_alloc is None or adaptive_alloc is None:
        print("::warning::Could not extract allocation metrics; skipping")
        return 0
    thresh = float(os.environ.get(THRESH_ENV, DEFAULT_THRESH))
    min_bytes = float(os.environ.get(MIN_BYTES_ENV, DEFAULT_MIN_BYTES))
    delta_pct = (adaptive_alloc - fixed_alloc) / fixed_alloc * 100 if fixed_alloc else float("inf")
    under_min = adaptive_alloc < min_bytes and fixed_alloc < min_bytes
    status = "PASS" if under_min or delta_pct <= thresh else "FAIL"
    md = [
        "# Adaptive Paging Allocation Delta",
        "",
        f"Report: `{report_path}`",
        f"Fixed Alloc (B/op): {fixed_alloc:,.0f}",
        f"Adaptive Alloc (B/op): {adaptive_alloc:,.0f}",
        f"Delta: {delta_pct:.2f}% (threshold {thresh:.2f}%)",
        f"Min Bytes Threshold: {min_bytes:,.0f} B/op (check {'skipped' if under_min else 'applied'})",
        f"Result: **{status}**",
    ]
    out_md = "\n".join(md) + "\n"
    Path("ADAPTIVE-ALLOC-DELTA.md").write_text(out_md)
    print(out_md)
    if status == "FAIL":
        print(f"::error::Adaptive allocations exceed fixed by {delta_pct:.2f}% (> {thresh:.2f}%)")
        return 1
    return 0

if __name__ == "__main__":
    sys.exit(main())

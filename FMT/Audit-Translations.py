"""Static Traditional Chinese resource audit. Candidates need runtime/semantic review."""
import collections
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "FMT/TranslationAudit"
EXCLUDED = {"tmp", "bin", "obj", "obj2", "obj3", "packages", "plugins", "resedit", "SikRadio", "Updater", "wix", "Msi", "APMPlannerXplanes"}
CJK = re.compile(r"[\u3400-\u9fff]")
FORMAT = re.compile(r"(?<!\{)\{(\d+)(?:,[+-]?\d+)?(?::[^{}]+)?\}(?!\})")
SIMPLIFIED = set("无与为连设认关启录错发数传输显选项钟读写测试压电飞轴转维导频过载轨图层卫复标参术时将应级损坏状态归锁块滤这该删执证检请页验档预边载类并么")


def resources(path):
    result = {}
    for item in ET.parse(path).getroot().findall("data"):
        if item.get("mimetype") or item.get("type", "").split(",")[0] not in ("", "System.String"):
            continue
        key = item.get("name")
        if key.startswith(">>"):
            continue  # WinForms designer metadata, not visible/translatable text.
        if key in result:
            raise ValueError(f"Duplicate resource key: {path}:{key}")
        result[key] = item.findtext("value", "")
    return result


def main():
    paths = subprocess.check_output(["rg", "--files", "-g", "*.resx"], cwd=ROOT, text=True, encoding="utf-8").splitlines()
    paths = [Path(p) for p in paths if not {v.lower() for v in EXCLUDED}.intersection(part.lower() for part in Path(p).parts)
             and not any(part.startswith(("FMT-MQTT", "FMT-MAVLink", "Feimao", "MQTT-", "FlightLogViewer")) for part in Path(p).parts)
             and (Path(p).parts[0] != "ExtLibs" or Path(p).parts[1] in ("Controls", "Strings", "ArduPilot"))]
    localized = sorted(p for p in paths if re.search(r"\.zh(?:-[A-Za-z]+)?\.resx$", p.name))
    rows, errors, candidates = [], [], []
    for relative in localized:
        base = re.sub(r"\.zh(?:-[A-Za-z]+)?\.resx$", ".resx", str(relative))
        try:
            values = resources(ROOT / relative)
            original = resources(ROOT / base) if (ROOT / base).exists() else {}
        except Exception as exc:
            errors.append({"File": relative.as_posix(), "Error": str(exc)})
            continue
        rows.append({"File": relative.as_posix(), "Strings": len(values)})
        for key, value in values.items():
            source = original.get(key, "")
            row = {"File": relative.as_posix(), "Key": key, "Source": source, "Translation": value}
            if "\ufffd" in value:
                errors.append(dict(row, Kind="replacement-character"))
            if source and collections.Counter(FORMAT.findall(source)) != collections.Counter(FORMAT.findall(value)):
                candidates.append(dict(row, Kind="format-arguments"))
            if source.strip() and not value.strip():
                candidates.append(dict(row, Kind="empty-translation"))
            simplified = sorted(set(value) & SIMPLIFIED)
            if simplified:
                candidates.append(dict(row, Kind="simplified-characters", Characters="".join(simplified)))
            if value == source and re.search(r"[a-zA-Z]{3,}", value) and not CJK.search(value) and not key.startswith(">>"):
                candidates.append(dict(row, Kind="original-English-or-technical-label"))
    # A translated file can be partial or absent. Also inspect effective zh-TW
    # fallback, not only entries that already have a Chinese translation.
    fallback_candidates, neutral_count = [], 0
    for relative in sorted(paths):
        if re.search(r"\.[a-z]{2}(?:-[A-Za-z]+)*\.resx$", relative.name):
            continue
        try:
            neutral = resources(ROOT / relative)
        except ValueError as exc:
            # Legacy designer resources can repeat otherwise identical strings.
            errors.append({"File": relative.as_posix(), "Error": str(exc)})
            continue
        neutral_count += 1
        effective = dict(neutral)
        for culture in ("zh", "zh-Hant", "zh-TW"):
            sibling = ROOT / relative.with_name(relative.stem + "." + culture + ".resx")
            if sibling.exists():
                effective.update(resources(sibling))
        for key, source in neutral.items():
            value = effective.get(key, source)
            if value and re.search(r"[A-Za-z]{3,}", value) and not CJK.search(value):
                fallback_candidates.append({"File": relative.as_posix(), "Key": key, "Text": value,
                    "ReviewStatus": "Static fallback candidate; may be a technical name, legacy/hidden control or translated dynamically in C#"})
    catalog_path = ROOT / "FMT/Localization/Parameters.zh-TW.reviewed.json"
    pairs = json.loads(catalog_path.read_text(encoding="utf-8-sig"), object_pairs_hook=list)
    seen = set()
    for source, value in pairs:
        if source in seen or not value.strip() or "\ufffd" in value:
            errors.append({"File": str(catalog_path.relative_to(ROOT)), "Source": source, "Error": "duplicate, blank, or replacement character"})
        seen.add(source)
        for ref in re.findall(r"\b[A-Z][A-Z0-9]*_[A-Z0-9_]+\b", source):
            if ref not in value:
                errors.append({"File": str(catalog_path.relative_to(ROOT)), "Source": source, "Error": "Missing parameter reference: " + ref})
    report = {"Scope": "FMTPlanner Chinese RESX plus Controls/Strings/ArduPilot dependencies and reviewed parameter catalog",
              "CompleteSemanticReview": False,
              "Note": "Static candidates are not confirmed visible defects: runtime overrides, technical names and resource fallback require review. Other language locales and separate companion apps are excluded.",
              "ResourceFiles": len(rows), "ResourceStrings": sum(r["Strings"] for r in rows),
              "NeutralResourceFilesChecked": neutral_count, "EffectiveFallbackCandidates": fallback_candidates,
              "ReviewedParameterCatalogEntries": len(pairs), "Errors": errors,
              "CandidateCounts": dict(collections.Counter(r["Kind"] for r in candidates)), "Files": rows, "Candidates": candidates}
    OUT.mkdir(exist_ok=True)
    (OUT / "resource-audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k not in ("Candidates", "Files", "EffectiveFallbackCandidates")}, ensure_ascii=False, indent=2))
    print("Effective fallback candidates (not confirmed defects):", len(fallback_candidates))
    return bool(errors)


if __name__ == "__main__":
    raise SystemExit(main())

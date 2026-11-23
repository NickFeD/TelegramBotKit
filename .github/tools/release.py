"""Shared, fail-fast release validation. Run from the repository root; never publishes."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import subprocess
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from symbol_validation import validate_symbol_pairs


VERSION = ET.parse("Directory.Build.props").findtext(".//Version")
PROJECTS = sorted(Path("src").glob("*/*.csproj"))
TEST_PROJECTS = sorted(p for p in Path("tests").glob("*/*.csproj")
                       if ET.parse(p).findtext(".//IsTestProject") == "true")
PACKAGES = {ET.parse(p).findtext(".//PackageId"): p for p in PROJECTS
            if ET.parse(p).findtext(".//IsPackable") != "false"}
OUTPUT = Path("artifacts/packages")


def require(condition, detail):
    if not condition:
        raise SystemExit(f"Package validation failed: {detail}")


def run(*args):
    print("+", *map(str, args), flush=True)
    subprocess.run(list(map(str, args)), check=True)


def target_frameworks(project):
    tree = ET.parse(project)
    value = tree.findtext(".//TargetFrameworks") or tree.findtext(".//TargetFramework")
    require(value, f"{project}: target frameworks are missing")
    return value.split(";")


def require_runtime_assets(names, package, frameworks):
    required = {f"lib/{framework}/{package}.{extension}"
                for framework in frameworks for extension in ("dll", "xml")}
    require(required <= names, f"{package}: missing runtime assets {sorted(required - names)}")


def check_configurations(paths):
    credential = re.compile(r'"(?:Token|SecretToken|ApiKey)"\s*:\s*"(?:\d+:[A-Za-z0-9_-]{20,}|[A-Za-z0-9_-]{32,})"', re.IGNORECASE)
    for path in paths:
        if credential.search(Path(path).read_text(encoding="utf-8-sig")):
            raise SystemExit(f"Credential-like value in tracked configuration: {path}. Use local configuration or environment variables.")


def check_version():
    # Check every package, including unlisted versions, before any publication.
    for package in PACKAGES:
        url = ("https://api.nuget.org/v3-flatcontainer/"
               f"{package.lower()}/{VERSION}/{package.lower()}.nuspec")
        try:
            with urllib.request.urlopen(url, timeout=30):
                pass
        except urllib.error.HTTPError as error:
            if error.code == 404:
                print(f"Available: {package} {VERSION}")
                continue
            raise
        raise SystemExit(f"Already published: {package} {VERSION}. Choose a new version.")


def validate():
    expected = set()
    for package, project in PACKAGES.items():
        tree = ET.parse(project)
        analyzer = tree.findtext(".//IsRoslynAnalyzer") == "true"
        filename = f"{package}.{VERSION}.nupkg"
        expected.add(filename)
        with zipfile.ZipFile(OUTPUT / filename) as archive:
            names = set(archive.namelist())
            spec = ET.fromstring(archive.read(f"{package}.nuspec"))
            ns = {"n": spec.tag.split("}")[0].strip("{")}
            metadata = spec.find("n:metadata", ns)
            require(metadata.findtext("n:id", namespaces=ns) == package, f"{filename}: package ID")
            require(metadata.findtext("n:version", namespaces=ns) == VERSION, f"{filename}: package version")
            require(metadata.findtext("n:license", namespaces=ns) == "MIT", f"{filename}: license")
            require(metadata.findtext("n:readme", namespaces=ns) == "README.md", f"{filename}: readme")
            require(metadata.findtext("n:icon", namespaces=ns) == "icon.png", f"{filename}: icon")
            repo = metadata.find("n:repository", ns)
            require(repo is not None and repo.get("commit"), f"{filename}: repository commit")
            required = {"README.md", "LICENSE", "icon.png"}
            if analyzer:
                required |= {f"analyzers/dotnet/cs/{package}.dll",
                             f"analyzers/dotnet/cs/{package}.pdb"}
                require(not any(n.startswith(("lib/", "ref/")) for n in names), f"{filename}: analyzer runtime assets")
                require(not metadata.findall(".//n:dependency", ns), f"{filename}: analyzer dependencies")
            else:
                frameworks = target_frameworks(project)
                require_runtime_assets(names, package, frameworks)
                groups = metadata.findall("n:dependencies/n:group", ns)
                actual_frameworks = {group.get("targetFramework") for group in groups}
                require(actual_frameworks == set(frameworks), f"{filename}: dependency groups {actual_frameworks}")
                if package == "TelegramBotKit":
                    required.add("analyzers/dotnet/cs/TelegramBotKit.Generators.dll")
            require(required <= names, (filename, required - names))
            if package == "TelegramBotKit":
                require(not any(d.get("id", "").startswith("Microsoft.CodeAnalysis") or d.get("id") == "TelegramBotKit.Generators"
                                for d in metadata.findall(".//n:dependency", ns)), f"{filename}: compiler runtime dependency")
            for dependency in metadata.findall(".//n:dependency", ns):
                if dependency.get("id") in PACKAGES:
                    require(dependency.get("version") in (VERSION, f"[{VERSION}, )"), ET.tostring(dependency))
            pdbs = [archive.read(n) for n in names if n.endswith(".pdb")]
            assemblies = {n: archive.read(n) for n in names if n.endswith((".dll", ".exe"))}
            inline_symbols = {n: archive.read(n) for n in names if n.endswith(".pdb")}
        if tree.findtext(".//IncludeSymbols") != "false":
            symbols = f"{package}.{VERSION}.snupkg"
            expected.add(symbols)
            with zipfile.ZipFile(OUTPUT / symbols) as archive:
                symbol_files = {n: archive.read(n) for n in archive.namelist() if n.endswith(".pdb")}
                require(symbol_files, f"{symbols}: no PDB files")
                try:
                    validate_symbol_pairs(assemblies, {**inline_symbols, **symbol_files})
                except ValueError as error:
                    require(False, f"{symbols}: {error}")
                pdbs += list(symbol_files.values())
        require(pdbs and all(p.startswith(b"BSJB") for p in pdbs), f"{package}: portable PDBs")
        require(all(b"raw.githubusercontent.com/NickFeD/TelegramBotKit/" in p for p in pdbs), f"Missing SourceLink: {package}")
        print(f"Validated: {filename}")
    actual = {p.name for p in OUTPUT.glob("*.nupkg")} | {p.name for p in OUTPUT.glob("*.snupkg")}
    require(actual == expected, (actual, expected))
    (OUTPUT / "SHA256SUMS").write_text("".join(
        f"{hashlib.sha256((OUTPUT / n).read_bytes()).hexdigest()}  {n}\n"
        for n in sorted(expected)), encoding="utf-8")


def build():
    sdk = json.loads(Path("global.json").read_text())["sdk"]["version"]
    actual_sdk = subprocess.check_output(["dotnet", "--version"], text=True).strip()
    if actual_sdk != sdk:
        raise SystemExit(f"Release validation requires SDK {sdk}; selected {actual_sdk}.")
    if not TEST_PROJECTS:
        raise SystemExit("No test projects discovered; refusing an untested release.")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if any(OUTPUT.iterdir()):
        raise SystemExit(f"Use an empty {OUTPUT} directory to avoid mixing release artifacts.")
    configuration_paths = subprocess.check_output(["git", "ls-files", "*appsettings*.json"], text=True).splitlines()
    check_configurations(configuration_paths)
    os.environ["ContinuousIntegrationBuild"] = "true"
    run(sys.executable, "-m", "unittest", "discover", "-s", Path(__file__).resolve().parent, "-p", "test_*.py")
    samples = sorted(Path("samples").glob("*/*.csproj"))
    for project in [*PROJECTS, *TEST_PROJECTS, *samples]:
        run("dotnet", "restore", project, "--locked-mode")
    for project in PROJECTS:
        run("dotnet", "build", project, "-c", "Release", "--no-restore", "-warnaserror")
    for project in TEST_PROJECTS:
        run("dotnet", "test", project, "-c", "Release", "--no-restore")
    for project in samples:
        run("dotnet", "build", project, "-c", "Release", "--no-restore", "-warnaserror")
    run(sys.executable, Path(__file__).resolve().parent / "check_samples.py")
    for project in PACKAGES.values():
        run("dotnet", "pack", project, "-c", "Release", "--no-build", "-o", OUTPUT)
    validate()
    # Use an isolated cache so an already-cached published Core cannot shadow
    # the newly packed version. No separate Generators reference is permitted.
    consumer = Path("tests/Packaging/Consumer/Consumer.csproj")
    global_cache = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
    version_property = f"-p:TelegramBotKitValidationVersion={VERSION}"
    for transitive in ("false", "true"):
        mode = f"-p:UseTransitiveCore={transitive}"
        run("dotnet", "restore", consumer, "--source", OUTPUT, "--source", global_cache,
            "--packages", OUTPUT / "consumer-cache", version_property, mode, "-p:NuGetAudit=false")
        for framework in target_frameworks(consumer):
            run("dotnet", "run", "--project", consumer, "-c", "Release", "--no-restore",
                "--framework", framework, version_property, mode)
    run(sys.executable, Path(__file__).resolve().parent / "check_sdk_consumers.py", "--packages", OUTPUT)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["build", "validate", "check-version"])
    parser.add_argument("--output", type=Path, default=OUTPUT,
                        help="Empty package output directory (default: artifacts/packages).")
    arguments = parser.parse_args()
    OUTPUT = arguments.output
    command = arguments.command
    {"build": build, "validate": validate, "check-version": check_version}[command]()

"""Shared, fail-fast preview validation. Run from the repository root."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile


VERSION = ET.parse("Directory.Build.props").findtext(".//Version")
PROJECTS = sorted(Path("src").glob("*/*.csproj"))
PACKAGES = {ET.parse(p).findtext(".//PackageId"): p for p in PROJECTS}
OUTPUT = Path("artifacts/packages")


def run(*args):
    print("+", *map(str, args), flush=True)
    subprocess.run(list(map(str, args)), check=True)


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
            assert metadata.findtext("n:id", namespaces=ns) == package
            assert metadata.findtext("n:version", namespaces=ns) == VERSION
            assert metadata.findtext("n:license", namespaces=ns) == "MIT"
            assert metadata.findtext("n:readme", namespaces=ns) == "README.md"
            assert metadata.findtext("n:icon", namespaces=ns) == "icon.png"
            repo = metadata.find("n:repository", ns)
            assert repo is not None and repo.get("commit")
            required = {"README.md", "LICENSE", "icon.png"}
            if analyzer:
                required |= {f"analyzers/dotnet/cs/{package}.dll",
                             f"analyzers/dotnet/cs/{package}.pdb"}
                assert not any(n.startswith(("lib/", "ref/")) for n in names)
                assert not metadata.findall(".//n:dependency", ns)
            else:
                required |= {f"lib/net10.0/{package}.dll", f"lib/net10.0/{package}.xml"}
            assert required <= names, (filename, required - names)
            for dependency in metadata.findall(".//n:dependency", ns):
                if dependency.get("id") in PACKAGES:
                    assert dependency.get("version") in (VERSION, f"[{VERSION}, )"), ET.tostring(dependency)
            pdbs = [archive.read(n) for n in names if n.endswith(".pdb")]
        if tree.findtext(".//IncludeSymbols") != "false":
            symbols = f"{package}.{VERSION}.snupkg"
            expected.add(symbols)
            with zipfile.ZipFile(OUTPUT / symbols) as archive:
                pdbs += [archive.read(n) for n in archive.namelist() if n.endswith(".pdb")]
        assert pdbs and all(p.startswith(b"BSJB") for p in pdbs), package
        assert all(b"raw.githubusercontent.com/NickFeD/TelegramBotKit/" in p for p in pdbs), f"Missing SourceLink: {package}"
        print(f"Validated: {filename}")
    actual = {p.name for p in OUTPUT.glob("*.nupkg")} | {p.name for p in OUTPUT.glob("*.snupkg")}
    assert actual == expected, (actual, expected)
    (OUTPUT / "SHA256SUMS").write_text("".join(
        f"{hashlib.sha256((OUTPUT / n).read_bytes()).hexdigest()}  {n}\n"
        for n in sorted(expected)), encoding="utf-8")


def build():
    sdk = json.loads(Path("global.json").read_text())["sdk"]["version"]
    assert subprocess.check_output(["dotnet", "--version"], text=True).strip() == sdk
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if any(OUTPUT.iterdir()):
        raise SystemExit(f"Use an empty {OUTPUT} directory to avoid mixing release artifacts.")
    os.environ["ContinuousIntegrationBuild"] = "true"
    run("python", "-m", "unittest", "discover", "-s", "scripts", "-p", "test_release.py")
    tests = Path("tests/TelegramBotKit.Tests/TelegramBotKit.Tests.csproj")
    samples = sorted(Path("samples").glob("*/*.csproj"))
    for project in [*PROJECTS, tests, *samples]:
        run("dotnet", "restore", project, "--locked-mode")
    for project in PROJECTS:
        run("dotnet", "build", project, "-c", "Release", "--no-restore")
    run("dotnet", "test", tests, "-c", "Release", "--no-restore")
    for project in samples:
        run("dotnet", "build", project, "-c", "Release", "--no-restore")
    for project in PROJECTS:
        run("dotnet", "pack", project, "-c", "Release", "--no-build", "-o", OUTPUT)
    validate()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["build", "validate", "check-version"])
    command = parser.parse_args().command
    {"build": build, "validate": validate, "check-version": check_version}[command]()

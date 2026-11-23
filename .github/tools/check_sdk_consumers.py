"""Compile and run packed consumers with SDK 8, independently of the repository SDK pin."""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages', type=Path, required=True)
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    packages = arguments.packages.resolve()
    version = ET.parse(root / 'Directory.Build.props').findtext('.//Version')
    installed = subprocess.check_output(['dotnet', '--list-sdks'], text=True)
    versions = re.findall(r'^(8\.0\.\d+) \[', installed, re.MULTILINE)
    if not versions:
        raise SystemExit('Install .NET SDK 8 to validate the minimum supported consumer compiler.')
    sdk = max(versions, key=lambda value: tuple(map(int, value.split('.'))))
    global_cache = Path(os.environ.get('NUGET_PACKAGES', Path.home() / '.nuget/packages')).resolve()
    source = root / 'tests/Packaging/Consumer'
    with tempfile.TemporaryDirectory(prefix='TelegramBotKit-sdk8-consumer-') as directory:
        workspace = Path(directory)
        (workspace / 'global.json').write_text(
            '{"sdk":{"version":"' + sdk + '","rollForward":"disable","allowPrerelease":false}}', encoding='utf-8')
        project = ET.parse(source / 'Consumer.csproj')
        framework = project.find('.//TargetFrameworks')
        framework.tag = 'TargetFramework'
        framework.text = 'net8.0'
        project.write(workspace / 'Consumer.csproj', encoding='utf-8')
        shutil.copyfile(source / 'Program.cs', workspace / 'Program.cs')
        selected = subprocess.check_output(['dotnet', '--version'], cwd=workspace, text=True).strip()
        if selected != sdk:
            raise SystemExit(f'Expected SDK {sdk}, selected {selected}.')
        for mode in ('false', 'true'):
            properties = [f'-p:TelegramBotKitValidationVersion={version}', f'-p:UseTransitiveCore={mode}']
            subprocess.run(['dotnet', 'restore', 'Consumer.csproj', '--source', str(packages),
                            '--source', str(global_cache), '--packages', str(packages / 'consumer-cache'),
                            '-p:NuGetAudit=false', *properties], cwd=workspace, check=True)
            subprocess.run(['dotnet', 'run', '--project', 'Consumer.csproj', '-c', 'Release',
                            '--no-restore', *properties], cwd=workspace, check=True)
            print(f'SDK8_CONSUMER_OK: {sdk}, transitive={mode}', flush=True)


if __name__ == '__main__':
    main()

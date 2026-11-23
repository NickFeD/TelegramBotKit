"""Check built sample configuration without contacting Telegram or opening user databases."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import ctypes


def main():
    if os.name == 'nt':
        # Child processes inherit these flags; an unexpected failure must not open a desktop dialog.
        ctypes.windll.kernel32.SetErrorMode(0x0001 | 0x0002 | 0x8000)
    root = Path(__file__).resolve().parents[2]
    for name in ('ConsolePolling', 'MinimalPolling', 'PhotoSelector', 'Webhook'):
        project = root / 'samples' / ('TelegramBotKit.Sample.' + name)
        assembly = project / 'bin/Release/net10.0' / (project.name + '.dll')
        if not assembly.is_file():
            raise SystemExit(f'Build Release before checking {project.name}.')
        environment = os.environ.copy()
        environment.update({
            'DOTNET_ENVIRONMENT': 'Production', 'ASPNETCORE_ENVIRONMENT': 'Production',
            'TelegramBotKit__Token': '12345:TEST',
            'TELEGRAM_BOT_TOKEN': '12345:TEST',
            'Logging__EventLog__LogLevel__Default': 'None',
            'HTTPS_PROXY': 'http://127.0.0.1:9', 'HTTP_PROXY': 'http://127.0.0.1:9', 'NO_PROXY': ''
        })
        with tempfile.TemporaryDirectory(prefix='TelegramBotKit-sample-') as directory:
            shutil.copyfile(project / 'appsettings.json', Path(directory) / 'appsettings.json')
            result = subprocess.run(
                ['dotnet', str(assembly), '--TelegramBotKit:Token=', '--TELEGRAM_BOT_TOKEN='],
                cwd=directory, env=environment, capture_output=True, encoding='utf-8', errors='replace', timeout=20)
        output = result.stdout + result.stderr
        if result.returncode != 1 or not any(marker in output for marker in ('Configuration error:', 'Токен Telegram-бота не настроен')):
            # Child output may contain application configuration; never echo it into CI logs.
            diagnostics = root / 'artifacts/sample-startup'
            diagnostics.mkdir(parents=True, exist_ok=True)
            (diagnostics / (name + '.log')).write_text(output, encoding='utf-8')
            raise SystemExit(f'{project.name}: expected a configuration failure before starting delivery (exit {result.returncode}).')
        print(f'SAMPLE_CONFIGURATION_OK: {project.name}')


if __name__ == '__main__':
    main()

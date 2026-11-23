"""Regression checks for publication gates; no network or publication."""
import io
import os
from pathlib import Path
import sys
import subprocess
import tempfile
import unittest
import urllib.error
from unittest.mock import call, patch

import release
import check_sdk_consumers

SDK = release.json.loads(Path('global.json').read_text())['sdk']['version']


class VersionGateTests(unittest.TestCase):
    def test_all_packages_absent(self):
        absent = urllib.error.HTTPError('url', 404, 'absent', {}, None)
        with patch('release.urllib.request.urlopen', side_effect=absent) as request:
            release.check_version()
        self.assertEqual(request.call_count, len(release.PACKAGES))

    def test_partial_release_is_rejected(self):
        absent = urllib.error.HTTPError('url', 404, 'absent', {}, None)
        with patch('release.urllib.request.urlopen', side_effect=[absent, io.BytesIO()]):
            with self.assertRaisesRegex(SystemExit, 'Choose a new version'):
                release.check_version()

    def test_server_error_fails_closed(self):
        error = urllib.error.HTTPError('url', 503, 'unavailable', {}, None)
        with patch('release.urllib.request.urlopen', side_effect=error):
            with self.assertRaises(urllib.error.HTTPError):
                release.check_version()

    def test_transport_error_fails_closed(self):
        with patch('release.urllib.request.urlopen', side_effect=TimeoutError):
            with self.assertRaises(TimeoutError):
                release.check_version()


class BuildGateTests(unittest.TestCase):
    def test_package_validation_remains_enabled_with_optimized_python(self):
        script = "import sys; sys.path.insert(0, " + repr(str(Path(release.__file__).resolve().parent)) + "); import release; release.require(False, 'optimization-must-not-bypass-validation')"
        result = subprocess.run([sys.executable, '-O', '-c', script], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('optimization-must-not-bypass-validation', result.stderr)

    def test_build_runs_every_test_project_and_both_packed_consumers(self):
        with tempfile.TemporaryDirectory() as output, \
                patch.object(release, 'OUTPUT', Path(output)), \
                patch('release.subprocess.check_output', return_value=SDK + '\n'), \
                patch('release.run') as run, patch('release.validate') as validate, \
                patch('release.check_configurations'), \
                patch.dict(os.environ):
            release.build()
        commands = run.call_args_list
        for project in ('TelegramBotKit.Tests', 'TelegramBotKit.Sample.PhotoSelector.Tests'):
            path = Path('tests') / project / (project + '.csproj')
            self.assertIn(call('dotnet', 'restore', path, '--locked-mode'), commands)
            self.assertIn(call('dotnet', 'test', path, '-c', 'Release', '--no-restore'), commands)
        self.assertIn(call(sys.executable, '-m', 'unittest', 'discover', '-s',
                           Path(release.__file__).resolve().parent, '-p', 'test_*.py'), commands)
        consumers = [args.args for args in commands if args.args[:2] == ('dotnet', 'run')]
        self.assertEqual(len(consumers), 8)
        for framework in ('net8.0', 'net9.0', 'net10.0', 'net11.0'):
            for mode in ('false', 'true'):
                self.assertTrue(any(command[command.index('--framework') + 1] == framework
                                    and '-p:UseTransitiveCore=' + mode in command for command in consumers))
        self.assertFalse(any('push' in command.args for command in commands))
        self.assertIn(call(sys.executable, Path(release.__file__).resolve().parent / 'check_sdk_consumers.py',
                           '--packages', Path(output)), commands)
        validate.assert_called_once_with()

    def test_sdk_mismatch_stops_before_any_restore_or_tests(self):
        with patch('release.subprocess.check_output', return_value='10.0.404\n'), patch('release.run') as run:
            with self.assertRaisesRegex(SystemExit, 'requires SDK ' + SDK):
                release.build()
        run.assert_not_called()

    def test_missing_test_discovery_is_not_a_successful_release(self):
        with patch('release.subprocess.check_output', return_value=SDK + '\n'), \
                patch.object(release, 'TEST_PROJECTS', []), patch('release.run') as run:
            with self.assertRaisesRegex(SystemExit, 'No test projects'):
                release.build()
        run.assert_not_called()

    def test_nonempty_output_is_preserved_and_rejected(self):
        with tempfile.TemporaryDirectory() as output:
            sentinel = Path(output) / 'previous.nupkg'
            sentinel.write_bytes(b'previous release')
            with patch.object(release, 'OUTPUT', Path(output)), \
                    patch('release.subprocess.check_output', return_value=SDK + '\n'), patch('release.run') as run:
                with self.assertRaisesRegex(SystemExit, 'empty'):
                    release.build()
            self.assertEqual(sentinel.read_bytes(), b'previous release')
            run.assert_not_called()


class FrameworkAssetTests(unittest.TestCase):
    def test_all_runtime_frameworks_require_dll_and_xml(self):
        frameworks = ('net8.0', 'net9.0', 'net10.0', 'net11.0')
        names = {f'lib/{framework}/Example.{extension}' for framework in frameworks for extension in ('dll', 'xml')}
        release.require_runtime_assets(names, 'Example', frameworks)
        for missing in sorted(names):
            with self.subTest(missing=missing):
                with self.assertRaisesRegex(SystemExit, 'missing runtime assets') as error:
                    release.require_runtime_assets(names - {missing}, 'Example', frameworks)
                self.assertIn(missing, str(error.exception))

    def test_single_and_multiple_target_frameworks_are_discovered(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory) / 'Example.csproj'
            for element, value, expected in (
                    ('TargetFramework', 'net10.0', ['net10.0']),
                    ('TargetFrameworks', 'net8.0;net9.0;net10.0;net11.0', ['net8.0', 'net9.0', 'net10.0', 'net11.0'])):
                project.write_text(f'<Project><PropertyGroup><{element}>{value}</{element}></PropertyGroup></Project>', encoding='utf-8')
                self.assertEqual(release.target_frameworks(project), expected)
            project.write_text('<Project />', encoding='utf-8')
            with self.assertRaisesRegex(SystemExit, 'target frameworks are missing'):
                release.target_frameworks(project)


class MinimumSdkTests(unittest.TestCase):
    def test_sdk8_consumers_use_old_compiler_for_both_reference_modes(self):
        installed = '8.0.100 [sdk]\n8.0.428 [sdk]\n11.0.100 [sdk]\n'
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(sys, 'argv', ['check_sdk_consumers.py', '--packages', directory]), \
                patch('check_sdk_consumers.subprocess.check_output', side_effect=[installed, '8.0.428\n']), \
                patch('check_sdk_consumers.subprocess.run') as run:
            def check_workspace(*args, **kwargs):
                workspace = Path(kwargs['cwd'])
                self.assertEqual(release.json.loads((workspace / 'global.json').read_text())['sdk']['version'], '8.0.428')
                self.assertEqual(release.ET.parse(workspace / 'Consumer.csproj').findtext('.//TargetFramework'), 'net8.0')
            run.side_effect = check_workspace
            check_sdk_consumers.main()
        commands = [entry.args[0] for entry in run.call_args_list]
        self.assertEqual(len(commands), 4)
        for mode in ('false', 'true'):
            self.assertEqual(sum('-p:UseTransitiveCore=' + mode in command for command in commands), 2)
        self.assertEqual(sum(command[:2] == ['dotnet', 'run'] for command in commands), 2)

    def test_missing_sdk8_stops_before_consumer_restore(self):
        with patch.object(sys, 'argv', ['check_sdk_consumers.py', '--packages', 'artifacts/packages']), \
                patch('check_sdk_consumers.subprocess.check_output', return_value='11.0.100 [sdk]\n'), \
                patch('check_sdk_consumers.subprocess.run') as run:
            with self.assertRaisesRegex(SystemExit, 'Install .NET SDK 8'):
                check_sdk_consumers.main()
        run.assert_not_called()

    def test_wrong_sdk_resolution_stops_before_consumer_restore(self):
        with patch.object(sys, 'argv', ['check_sdk_consumers.py', '--packages', 'artifacts/packages']), \
                patch('check_sdk_consumers.subprocess.check_output', side_effect=['8.0.428 [sdk]\n', '11.0.100\n']), \
                patch('check_sdk_consumers.subprocess.run') as run:
            with self.assertRaisesRegex(SystemExit, 'Expected SDK 8.0.428'):
                check_sdk_consumers.main()
        run.assert_not_called()


class ConfigurationGateTests(unittest.TestCase):
    def test_credential_failure_does_not_echo_the_secret(self):
        synthetic = '123456:' + 'A' * 35
        with tempfile.TemporaryDirectory() as directory:
            settings = Path(directory) / 'appsettings.json'
            settings.write_text('{"Token":"' + synthetic + '"}', encoding='utf-8')
            with self.assertRaises(SystemExit) as error:
                release.check_configurations([settings])
            self.assertIn('tracked configuration', str(error.exception))
            self.assertNotIn(synthetic, str(error.exception))

    def test_placeholders_and_empty_tokens_are_allowed(self):
        with tempfile.TemporaryDirectory() as directory:
            settings = Path(directory) / 'appsettings.json'
            for value in ('', '<bot-token>', 'PUT_YOUR_BOT_TOKEN_HERE'):
                settings.write_text('{"Token":"' + value + '"}', encoding='utf-8')
                release.check_configurations([settings])


if __name__ == '__main__':
    unittest.main()

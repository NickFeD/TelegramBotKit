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
                patch('release.subprocess.check_output', return_value='10.0.404\n'), \
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
                           Path(release.__file__).resolve().parent, '-p', 'test_release.py'), commands)
        consumers = [args.args for args in commands if args.args[:2] == ('dotnet', 'run')]
        self.assertEqual(len(consumers), 2)
        self.assertTrue(any('-p:UseTransitiveCore=false' in command for command in consumers))
        self.assertTrue(any('-p:UseTransitiveCore=true' in command for command in consumers))
        self.assertFalse(any('push' in command.args for command in commands))
        validate.assert_called_once_with()

    def test_sdk_mismatch_stops_before_any_restore_or_tests(self):
        with patch('release.subprocess.check_output', return_value='11.0.100\n'), patch('release.run') as run:
            with self.assertRaisesRegex(SystemExit, 'requires SDK 10.0.404'):
                release.build()
        run.assert_not_called()

    def test_missing_test_discovery_is_not_a_successful_release(self):
        with patch('release.subprocess.check_output', return_value='10.0.404\n'), \
                patch.object(release, 'TEST_PROJECTS', []), patch('release.run') as run:
            with self.assertRaisesRegex(SystemExit, 'No test projects'):
                release.build()
        run.assert_not_called()

    def test_nonempty_output_is_preserved_and_rejected(self):
        with tempfile.TemporaryDirectory() as output:
            sentinel = Path(output) / 'previous.nupkg'
            sentinel.write_bytes(b'previous release')
            with patch.object(release, 'OUTPUT', Path(output)), \
                    patch('release.subprocess.check_output', return_value='10.0.404\n'), patch('release.run') as run:
                with self.assertRaisesRegex(SystemExit, 'empty'):
                    release.build()
            self.assertEqual(sentinel.read_bytes(), b'previous release')
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

"""Regression checks for publication gates; no network or publication."""
import io
import unittest
import urllib.error
from unittest.mock import patch

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


if __name__ == '__main__':
    unittest.main()

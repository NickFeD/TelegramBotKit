"""Regression tests for NuGet's DLL/PDB association requirements."""
import hashlib
import struct
import unittest
from pathlib import Path
import tempfile
from unittest.mock import patch
import zipfile

from symbol_validation import validate_symbol_pairs
import release


def pair(seed=1, pe64=False):
    pdb = bytearray(96)
    pdb[:4] = b'BSJB'
    struct.pack_into('<H', pdb, 18, 1)
    struct.pack_into('<II', pdb, 20, 64, 32)
    pdb[28:33] = b'#Pdb\0'
    identifier = bytes([seed]) * 16 + struct.pack('<I', 123)
    pdb[64:84] = identifier
    pdb[84:] = b'debug-data!!'
    binary = bytearray(1024)
    binary[:2] = b'MZ'
    struct.pack_into('<I', binary, 60, 128)
    binary[128:132] = b'PE\0\0'
    struct.pack_into('<H', binary, 134, 1)
    optional_size = 240 if pe64 else 224
    struct.pack_into('<H', binary, 148, optional_size)
    struct.pack_into('<H', binary, 152, 0x20b if pe64 else 0x10b)
    directory = 152 + (112 if pe64 else 96) + 48
    struct.pack_into('<II', binary, directory, 8192, 56)
    struct.pack_into('<IIII', binary, 152 + optional_size + 8, 512, 8192, 512, 512)
    codeview = b'RSDS' + identifier[:16] + struct.pack('<I', 1) + b'Example.pdb\0'
    checksum = b'SHA256\0' + hashlib.sha256(pdb[:64] + bytes(20) + pdb[84:]).digest()
    struct.pack_into('<IIHHIIII', binary, 512, 0, 123, 0, 0, 2, len(codeview), 0, 600)
    struct.pack_into('<IIHHIIII', binary, 540, 0, 0, 1, 0, 19, len(checksum), 0, 660)
    binary[600:600 + len(codeview)] = codeview
    binary[660:660 + len(checksum)] = checksum
    return bytes(binary), bytes(pdb)


class SymbolAssociationTests(unittest.TestCase):
    def test_release_gate_rejects_symbols_from_another_build(self):
        binary, _ = pair(1)
        _, other_pdb = pair(2)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            project = root / 'Example.csproj'
            project.write_text('<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>', encoding='utf-8')
            metadata = ('<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>'
                        '<id>Example</id><version>' + release.VERSION + '</version><license>MIT</license>'
                        '<readme>README.md</readme><icon>icon.png</icon><repository commit="abc" />'
                        '<dependencies><group targetFramework="net8.0" /></dependencies></metadata></package>')
            with zipfile.ZipFile(root / ('Example.' + release.VERSION + '.nupkg'), 'w') as archive:
                for name, content in {'Example.nuspec': metadata, 'README.md': '', 'LICENSE': '', 'icon.png': b'',
                                      'lib/net8.0/Example.dll': binary, 'lib/net8.0/Example.xml': ''}.items():
                    archive.writestr(name, content)
            with zipfile.ZipFile(root / ('Example.' + release.VERSION + '.snupkg'), 'w') as archive:
                archive.writestr('lib/net8.0/Example.pdb', other_pdb)
            with patch.object(release, 'OUTPUT', root), patch.object(release, 'PACKAGES', {'Example': project}):
                with self.assertRaisesRegex(SystemExit, 'PDB identifier does not match'):
                    release.validate()

    def test_matching_portable_symbols_accept_both_pe_formats(self):
        for pe64 in (False, True):
            with self.subTest(pe64=pe64):
                binary, pdb = pair(pe64=pe64)
                validate_symbol_pairs({'lib/net8.0/Example.dll': binary}, {'lib/net8.0/Example.pdb': pdb})

    def test_orphan_symbol_is_rejected_even_when_filename_exists_in_another_folder(self):
        binary, pdb = pair()
        with self.assertRaisesRegex(ValueError, 'corresponding DLL/EXE is missing'):
            validate_symbol_pairs({'lib/net9.0/Example.dll': binary}, {'lib/net8.0/Example.pdb': pdb})

    def test_symbols_from_another_build_are_rejected(self):
        binary, _ = pair(1)
        _, other_pdb = pair(2)
        with self.assertRaisesRegex(ValueError, 'identifier does not match'):
            validate_symbol_pairs({'Example.dll': binary}, {'Example.pdb': other_pdb})

    def test_changed_pdb_content_is_rejected_even_with_matching_identifier(self):
        binary, pdb = pair()
        changed = pdb[:-1] + bytes([pdb[-1] ^ 1])
        with self.assertRaisesRegex(ValueError, 'checksum does not match'):
            validate_symbol_pairs({'Example.dll': binary}, {'Example.pdb': changed})

    def test_missing_binary_checksum_is_rejected(self):
        binary, pdb = pair()
        changed = bytearray(binary)
        struct.pack_into('<I', changed, 152 + 96 + 48 + 4, 28)
        with self.assertRaisesRegex(ValueError, 'does not contain a PDB checksum'):
            validate_symbol_pairs({'Example.dll': changed}, {'Example.pdb': pdb})

    def test_matching_identifier_outside_debug_directory_is_not_accepted(self):
        binary, _ = pair(1)
        _, pdb = pair(2)
        changed = bytearray(binary)
        changed[900:920] = pdb[64:84]
        with self.assertRaisesRegex(ValueError, 'identifier does not match'):
            validate_symbol_pairs({'Example.dll': changed}, {'Example.pdb': pdb})

    def test_malformed_symbols_fail_closed(self):
        binary, _ = pair()
        for malformed in (b'not a PDB', b'BSJB'):
            with self.subTest(format='unknown' if malformed[:4] != b'BSJB' else 'truncated'):
                with self.assertRaises(ValueError):
                    validate_symbol_pairs({'Example.dll': binary}, {'Example.pdb': malformed})


if __name__ == '__main__':
    unittest.main()

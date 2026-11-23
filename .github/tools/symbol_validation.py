"""Validate portable PDB identities and checksums against the DLLs actually shipped."""
import hashlib
import struct


def portable_pdb_id(data):
    if data[:4] != b'BSJB':
        raise ValueError('PDB is not portable')
    version_length = struct.unpack_from('<I', data, 12)[0]
    position = (16 + version_length + 3) & ~3
    count = struct.unpack_from('<H', data, position + 2)[0]
    position += 4
    for _ in range(count):
        offset, size = struct.unpack_from('<II', data, position)
        end = data.index(b'\0', position + 8)
        name = data[position + 8:end]
        position = (end + 4) & ~3
        if name == b'#Pdb':
            if size < 20 or offset + size > len(data):
                raise ValueError('Invalid portable PDB identifier stream')
            return data[offset:offset + 20], offset
    raise ValueError('Portable PDB identifier stream is missing')


def pe_debug_records(data):
    if data[:2] != b'MZ':
        raise ValueError('Assembly is not a PE file')
    pe = struct.unpack_from('<I', data, 60)[0]
    if data[pe:pe + 4] != b'PE\0\0':
        raise ValueError('Invalid PE signature')
    sections = struct.unpack_from('<H', data, pe + 6)[0]
    optional_size = struct.unpack_from('<H', data, pe + 20)[0]
    optional = pe + 24
    magic = struct.unpack_from('<H', data, optional)[0]
    if magic not in (0x10b, 0x20b):
        raise ValueError('Unsupported PE optional header')
    directory = optional + (96 if magic == 0x10b else 112)
    address, length = struct.unpack_from('<II', data, directory + 6 * 8)
    if not address or not length or length % 28:
        raise ValueError('PE debug directory is missing or invalid')
    for index in range(sections):
        section = optional + optional_size + index * 40
        virtual_size, virtual_address, raw_size, raw = struct.unpack_from('<IIII', data, section + 8)
        if virtual_address <= address and address + length <= virtual_address + min(virtual_size, raw_size):
            offset = raw + address - virtual_address
            break
    else:
        raise ValueError('PE debug directory is outside a section')
    for position in range(offset, offset + length, 28):
        _, stamp, _, _, kind, size, _, pointer = struct.unpack_from('<IIHHIIII', data, position)
        if pointer + size > len(data):
            raise ValueError('PE debug record is truncated')
        yield kind, stamp, data[pointer:pointer + size]


def validate_symbol_pairs(assemblies, symbols):
    """Require a matching binary at the same package path for each symbol file."""
    for path, pdb in symbols.items():
        if not path.endswith('.pdb'):
            continue
        binary = next((name for name in (path[:-4] + '.dll', path[:-4] + '.exe') if name in assemblies), None)
        if binary is None:
            raise ValueError(f'{path}: corresponding DLL/EXE is missing from the NuGet package')
        try:
            identifier, id_offset = portable_pdb_id(pdb)
            records = list(pe_debug_records(assemblies[binary]))
            matches = [record for kind, stamp, record in records
                       if kind == 2 and record[:4] == b'RSDS'
                       and record[4:20] + struct.pack('<I', stamp) == identifier]
            if not matches:
                raise ValueError('PDB identifier does not match the shipped binary')
            checksums = [record for kind, _, record in records if kind == 19]
            if not checksums:
                raise ValueError('Binary does not contain a PDB checksum')
            for record in checksums:
                algorithm, expected = record.split(b'\0', 1)
                # Portable PDB checksums are computed with the 20-byte identifier zeroed.
                content = pdb[:id_offset] + bytes(20) + pdb[id_offset + 20:]
                actual = hashlib.new(algorithm.decode('ascii').lower().replace('-', ''), content).digest()
                if actual != expected:
                    raise ValueError('PDB checksum does not match the shipped binary')
        except (ValueError, IndexError, struct.error, UnicodeError) as error:
            raise ValueError(f'{path}: {error}') from error

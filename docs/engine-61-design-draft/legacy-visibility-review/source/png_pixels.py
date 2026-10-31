"""Read the fixed fixture's RGB/RGBA8 PNG pixels without image/model rendering."""
import struct
import zlib


def glyph(png):
    assert png[:8] == b'\x89PNG\r\n\x1a\n'
    pos, compressed = 8, bytearray()
    while pos < len(png):
        size, kind = struct.unpack('>I4s', png[pos:pos+8])
        data = png[pos+8:pos+8+size]
        if kind == b'IHDR':
            width, height, depth, color, comp, filt, interlace = struct.unpack('>IIBBBBB', data)
            assert (width, height, depth, comp, filt, interlace) == (1680, 1110, 8, 0, 0, 0)
            assert color in (2, 6)
        if kind == b'IDAT':
            compressed.extend(data)
        pos += size + 12
    bpp = 4 if color == 6 else 3
    stride = width*bpp
    raw = zlib.decompress(compressed)
    previous = bytearray(stride)
    result = bytearray()
    # The original PNG remains untouched; only rows needed for this exact ROI are decoded.
    for y in range(58):
        offset = y*(stride+1)
        kind = raw[offset]
        row = bytearray(raw[offset+1:offset+1+stride])
        for x in range(stride):
            a = row[x-bpp] if x >= bpp else 0
            b = previous[x]
            c = previous[x-bpp] if x >= bpp else 0
            if kind == 0: predictor = 0
            elif kind == 1: predictor = a
            elif kind == 2: predictor = b
            elif kind == 3: predictor = (a+b)//2
            elif kind == 4:
                p = a+b-c
                distances = (abs(p-a), abs(p-b), abs(p-c))
                predictor = (a,b,c)[distances.index(min(distances))]
            else: raise ValueError('Unknown PNG filter')
            row[x] = (row[x]+predictor) & 255
        if y >= 18:
            for x in range(770, 910): result.extend(row[x*bpp:x*bpp+3])
        previous = row
    return bytes(result)

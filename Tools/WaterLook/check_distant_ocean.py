"""Offline geometry/depth regression checks; not a Unity or GPU compilation test."""
from collections import Counter
from pathlib import Path
import math
import re
import struct

ROOT = Path(__file__).resolve().parents[2]
asset = (ROOT / 'Assets/_Game/Water/Meshes/OceanGrid.asset').read_text()
count = int(re.search(r'm_VertexCount: (\d+)', asset)[1])
row = math.isqrt(count)
assert row * row == count
raw = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-fA-F]+)', asset)[1])
assert len(raw) == count * 32, 'Update decoder if ocean vertex layout changes'
grid = [struct.unpack_from('<fff', raw, i * 32) for i in range(count)]


def check_rings(grid, row):
    segments = row - 1
    vertices = ([grid[i * row] for i in range(segments)] +
                [grid[segments * row + i] for i in range(segments)] +
                [grid[(segments - i) * row + segments] for i in range(segments)] +
                [grid[segments - i] for i in range(segments)])
    perimeter = vertices.copy()
    center = tuple((a + b) / 2 for a, b in zip(grid[0], grid[-1]))
    half = tuple((b - a) / 2 for a, b in zip(grid[0], grid[-1]))
    spacing = max(half[0], half[2]) * 2 / segments
    triangles = []
    start0, count0, n = 0, segments * 4, segments
    for distance in [spacing, 15, 45, 105, 225, 465, 1200, 100000]:
        start = len(vertices)
        for side in range(4):
            a, b = perimeter[side * segments], perimeter[((side + 1) % 4) * segments]
            for j in range(n):
                t = j / n
                p = [(a[k] - center[k]) * (1-t) + (b[k] - center[k]) * t for k in range(3)]
                p[0] *= (half[0] + distance) / half[0]
                p[2] *= (half[2] + distance) / half[2]
                vertices.append(tuple(p[k] + center[k] for k in range(3)))
        count1 = n * 4
        inner = outer = 0
        while inner < count0 or outer < count1:
            a, b = start0 + inner % count0, start + outer % count1
            if outer == count1 or (inner < count0 and (inner+1)*count1 <= (outer+1)*count0):
                inner += 1
                c = start0 + inner % count0
            else:
                outer += 1
                c = start + outer % count1
            triangles.append((a,b,c))
        start0, count0 = start, count1
        n = max(4,n//2)
    edges = Counter()
    total_area = 0
    for tri in triangles:
        a,b,c = [vertices[i] for i in tri]
        up = (b[2]-a[2])*(c[0]-a[0]) - (b[0]-a[0])*(c[2]-a[2])
        assert up > 0, ('Inverted or degenerate triangle', tri)
        total_area += up/2
        # Every triangle stays outside the original square: no near-water overdraw.
        assert any(all(sign*(v[axis]-center[axis]) >= half[axis]-1e-5 for v in (a,b,c))
                   for axis in (0,2) for sign in (-1,1))
        for x,y in zip(tri,tri[1:]+tri[:1]):
            edges[tuple(sorted((x,y)))] += 1
    expected = 4*(half[0]+100000)*(half[2]+100000)-4*half[0]*half[2]
    assert math.isclose(total_area, expected, rel_tol=1e-10)
    assert max(edges.values()) == 2
    boundary = {e for e,n in edges.items() if n == 1}
    expected_boundary = {tuple(sorted((i,(i+1)%(segments*4)))) for i in range(segments*4)}
    expected_boundary |= {tuple(sorted((start0+i,start0+(i+1)%count0))) for i in range(count0)}
    assert boundary == expected_boundary, 'Crack or unstitched LOD edge'
    print(f'PASS: {row}x{row} grid; {len(vertices)} outer vertices, {len(triangles)} triangles; watertight hollow rings')

check_rings(grid,row)
# Odd perimeter resolution exercises zipper joins that are not exact 2:1.
check_rings([(-300+600*x/159,0,-300+600*z/159) for z in range(160) for x in range(160)],160)

# Near/far depth ordering for GLES [-1,1] and reversed-Z [1,0], including
# beyond-camera geometry; true reconstructed depth must not hide closer islands.
n,f = .1,650
for reversed_z in [False, True]:
    def depth(z):
        d = f/(f-n)-f*n/((f-n)*z)
        return max(0,1-d) if reversed_z else min(1,d)
    for water,land in [(310,300),(649,500),(1000,600),(100000,649)]:
        assert (depth(water)<depth(land)) if reversed_z else (depth(water)>depth(land))
    for water,bottom in [(310,350),(500,600)]:
        assert (depth(water)>depth(bottom)) if reversed_z else (depth(water)<depth(bottom))
print('PASS: depth ordering before/after far clip, both graphics conventions')

shader = (ROOT/'Assets/_Game/Water/Resources/DistantOcean.shader').read_text()
assert '#include "../Shaders/OceanGerstner.hlsl"' in shader
assert 'SampleSceneDepth(' not in shader and 'SAMPLE_TEXTURE' not in shader
assert 'SV_Depth' in shader and 'TransformWorldToHClip(input.positionWS)' in shader
assert shader.count('Name "DistantOcean"') == 1
print('PASS: shared wave include, Resources inclusion, single pass, no texture sampling')

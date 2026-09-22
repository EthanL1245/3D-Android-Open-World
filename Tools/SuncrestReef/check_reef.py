"""Data/property checks; Unity Play Mode and device profiling remain required."""
import math,re
from pathlib import Path
import numpy as np
root=Path(__file__).resolve().parents[2]
source=(root/'Assets/_Game/Scripts/Reef/ReefCatalog.cs').read_text()
weights=list(map(float,re.search(r'new float\[\] \{([^}]+)',source).group(1).split(',')))
assert len(weights)==8 and sum(weights)==100
assert weights[6]==weights[7] and min(weights[6:])>max(weights[:6])
rng=np.random.default_rng(73191)
rolls=rng.random(100000)*sum(weights)
counts=np.bincount(np.searchsorted(np.cumsum(weights),rolls),minlength=8)/1000
assert np.max(abs(counts-np.array(weights)))<.5
for bait in (0,2,3):
    boosted=np.array(weights)*[2.5 if bait==2 and i in (1,6,7) else 4 if bait==3 and i in (3,4,5) else 1 for i in range(8)]
    assert boosted.sum()>0 and (boosted>0).all()
    if bait==2:assert boosted[6:].sum()/boosted.sum()>.48
    if bait==3:assert boosted[3:6].sum()/boosted.sum()>.12
# Property test for dry-area calibration across different original island sizes.
for original_area in (10000,50000,150000):
    target=original_area*.5;r=math.sqrt(target/(math.pi*.78));size=max(600,2*r+520)
    def area(radius,n):
        axis=np.linspace(-size/2,size/2,n);x,z=np.meshgrid(axis,axis)
        angle=np.arctan2(z/(radius*.78),x/radius)
        coast=1+.065*np.sin(angle*3)+.045*np.cos(angle*5)
        q=np.sqrt((x/radius)**2+(z/(radius*.78))**2)/coast
        return np.count_nonzero(q<=1)/(n*n)*size*size
    low,high=r*.65,r*1.35
    for _ in range(12):
        r=(low+high)/2
        if area(r,257)<target:low=r
        else:high=r
    ratio=area(r,513)/original_area
    assert abs(ratio-.5)<.015,(original_area,ratio)
    print(f'Original {original_area:,.0f}m² -> dry area ratio {ratio:.4f}')
# Reef shelf must remain shallow, continuous and gradual through 160m.
d=np.linspace(0,160,1601)
depth=np.where(d<30,2*(3*(d/30)**2-2*(d/30)**3),2+4*(d-30)/130)
assert np.min(np.diff(depth))>=-1e-6 and abs(depth[-1]-6)<1e-6
assert np.max(np.diff(depth)/.1)<=.101
assert np.isclose(depth[300],2)
print('PASS: shared rarity weights, bait effects, half-area targets and shallow shelf properties.')

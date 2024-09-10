from skyfield.api import load
from skyfield.api import EarthSatellite
from skyfield.api import wgs84
from skyfield.framelib import itrs
from skyfield.framelib import ICRS
from skyfield.sgp4lib import TEME
ts = load.timescale()
line1 = '1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995'
line2 = '2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468'
satellite = EarthSatellite(line1, line2, 'CZ-3C DEB', ts)
t = ts.utc(2024, 8, 26, 22, 34, 20)
k88 = wgs84.latlon(+47.91748, 19.89367, 984)
difference = satellite - k88
topocentric = difference.at(t)
print(topocentric.position.km)
ra, dec, distance = topocentric.radec()  # ICRF ("J2000")
print(t)
print(ra)
print(dec)
print(satellite.at(t))

print("itrf position")
print(satellite.at(t).frame_xyz_and_velocity(itrs)[0].m)
print("itrf velocity")
print(satellite.at(t).frame_xyz_and_velocity(itrs)[1].m_per_s)

print("icrf position")
print(satellite.at(t).frame_xyz_and_velocity(ICRS)[0].m)
print("icrf velocity")
print(satellite.at(t).frame_xyz_and_velocity(ICRS)[1].m_per_s)

print("teme position")
print(satellite.at(t).frame_xyz_and_velocity(TEME)[0].m)
print("teme velocity")
print(satellite.at(t).frame_xyz_and_velocity(TEME)[1].m_per_s)

print(32718334.75810228-32718534.030244593)
print(-17501506.30133443- (-17501127.803996515))
print(11592986.41571926-11592995.3105345)
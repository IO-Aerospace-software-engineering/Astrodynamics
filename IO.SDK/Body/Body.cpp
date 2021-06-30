#include <Body.h>
#include <CelestialBody.h>
#include <StateVector.h>
#include <TDB.h>
#include <chrono>
#include <InertialFrames.h>
#include <Builder.h>

using namespace std::chrono_literals;

IO::SDK::Body::Body::Body(const int id, const std::string &name, const double mass)
	: m_id{id},
	  m_name{name},
	  m_mass{mass > 0 ? mass : throw IO::SDK::Exception::SDKException("Mass must be a positive value")},
	  m_mu{mass * IO::SDK::Constants::G}

{
}

IO::SDK::Body::Body::Body(const int id, const std::string &name, const double mass, std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch) : Body(id, name, mass)
{
	m_orbitalParametersAtEpoch = std::move(orbitalParametersAtEpoch);
	m_orbitalParametersAtEpoch->GetCenterOfMotion()->m_satellites.push_back(this);
}

IO::SDK::Body::Body::Body(const int id, const std::string &name, const double mass, std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion) : Body(id, name, mass)
{
	m_orbitalParametersAtEpoch = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(this->ReadEphemeris(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB(0s), *centerOfMotion));
	centerOfMotion->m_satellites.push_back(this);
}

IO::SDK::Body::Body::Body(const Body &body) :Body(body.m_id, body.m_name, body.m_mass) {}

int IO::SDK::Body::Body::GetId() const
{
	return m_id;
}

const std::string IO::SDK::Body::Body::GetName() const
{
	return m_name;
}

double IO::SDK::Body::Body::GetMass() const
{
	return m_mass;
}

double IO::SDK::Body::Body::GetMu() const
{
	return m_mu;
}

const std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> &IO::SDK::Body::Body::GetOrbitalParametersAtEpoch() const
{
	return m_orbitalParametersAtEpoch;
}

const std::vector<IO::SDK::Body::Body *> &IO::SDK::Body::Body::GetSatellites() const
{
	return m_satellites;
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Body::Body::ReadEphemeris(const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch, const IO::SDK::Body::CelestialBody &relativeTo) const
{
	IO::SDK::Aberrations aberrationHelper;
	SpiceDouble vs[6];
	SpiceDouble lt;
	spkezr_c(std::to_string(m_id).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), aberrationHelper.ToString(aberration).c_str(), std::to_string(relativeTo.m_id).c_str(), vs, &lt);

	//Convert to SDK unit
	for (size_t i = 0; i < 6; i++)
	{
		vs[i] = vs[i] * 1000.0; /* code */
	}

	return IO::SDK::OrbitalParameters::StateVector(std::make_shared<IO::SDK::Body::CelestialBody>(relativeTo), vs, epoch, frame);
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Body::Body::ReadEphemeris(const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const
{
	IO::SDK::Aberrations aberrationHelper;
	SpiceDouble vs[6];
	SpiceDouble lt;
	spkezr_c(std::to_string(m_id).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), aberrationHelper.ToString(aberration).c_str(), std::to_string(m_orbitalParametersAtEpoch->GetCenterOfMotion()->m_id).c_str(), vs, &lt);
	//Convert to SDK unit
	for (size_t i = 0; i < 6; i++)
	{
		vs[i] = vs[i] * 1000.0; /* code */
	}
	return IO::SDK::OrbitalParameters::StateVector(m_orbitalParametersAtEpoch->GetCenterOfMotion(), vs, epoch, frame);
}

bool IO::SDK::Body::Body::operator==(const IO::SDK::Body::Body &rhs) const
{
	return m_id == rhs.m_id;
}

bool IO::SDK::Body::Body::operator!=(const IO::SDK::Body::Body &rhs) const
{
	return m_id != rhs.m_id;
}

std::shared_ptr<IO::SDK::Body::Body> IO::SDK::Body::Body::GetSharedPointer()
{
	return this->shared_from_this();
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> IO::SDK::Body::Body::FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> window, const Body &targetBody, const Body &oberver, const IO::SDK::Constraint &constraint, const IO::SDK::AberrationsEnum aberration, const double value, const IO::SDK::Time::TimeSpan &step) const
{
	std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> windows;
	SpiceDouble windowStart;
	SpiceDouble windowEnd;

	Aberrations abe;

	const SpiceInt NINTVL{10000};
	const SpiceInt MAXWIN{20000};

	SpiceDouble SPICE_CELL_DIST[SPICE_CELL_CTRLSZ + MAXWIN];
	SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_DIST);

	SpiceDouble SPICE_CELL_DIST_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
	SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_DIST_RESULT);

	wninsd_c(window.GetStartDate().GetSecondsFromJ2000().count(), window.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

	gfdist_c(targetBody.GetName().c_str(), abe.ToString(aberration).c_str(), oberver.GetName().c_str(), constraint.ToCharArray(), value * 1E-03, 0.0, step.GetSeconds().count(), NINTVL, &cnfine, &results);

	for (int i = 0; i < wncard_c(&results); i++)
	{
		wnfetd_c(&results, i, &windowStart, &windowEnd);
		windows.push_back(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)), IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd))));
	}
	return windows;
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> IO::SDK::Body::Body::FindWindowsOnOccultationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> searchWindow, const IO::SDK::Body::CelestialBody &targetBody, const IO::SDK::Body::CelestialBody &frontBody, const IO::SDK::OccultationType &occultationType, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize) const
{
	std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> windows;
	SpiceDouble windowStart;
	SpiceDouble windowEnd;

	Aberrations abe;

	const SpiceInt MAXWIN{20000};

	SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
	SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

	SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
	SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

	wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

	gfoclt_c(occultationType.ToCharArray(), frontBody.GetName().c_str(), "ELLIPSOID", frontBody.GetBodyFixedFrame().GetName().c_str(), targetBody.GetName().c_str(), "ELLIPSOID", targetBody.GetBodyFixedFrame().GetName().c_str(), abe.ToString(aberration).c_str(), m_name.c_str(), stepSize.GetSeconds().count(), &cnfine, &results);

	for (int i = 0; i < wncard_c(&results); i++)
	{
		wnfetd_c(&results, i, &windowStart, &windowEnd);
		windows.push_back(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)), IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd))));
	}
	return windows;
}

IO::SDK::Coordinates::Planetographic IO::SDK::Body::Body::GetSubObserverPoint(const IO::SDK::Body::CelestialBody &targetBody, const IO::SDK::AberrationsEnum &aberration, const IO::SDK::Time::DateTime &epoch) const
{
	IO::SDK::Aberrations abe;
	SpiceDouble spoint[3];
	SpiceDouble srfVector[3];
	SpiceDouble subEpoch;
	subpnt_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(), abe.ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
	SpiceDouble lat, lon, alt;
	recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX(), targetBody.GetFlattening(), &lon, &lat, &alt);

	return IO::SDK::Coordinates::Planetographic(lon, lat, alt);
}

IO::SDK::Coordinates::Planetographic IO::SDK::Body::Body::GetSubSolarPoint(const IO::SDK::Body::CelestialBody &targetBody, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const
{
	IO::SDK::Aberrations abe;
	SpiceDouble spoint[3];
	SpiceDouble srfVector[3];
	SpiceDouble subEpoch;
	subslr_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(), abe.ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
	SpiceDouble lat, lon, alt;
	recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX(), targetBody.GetFlattening(), &lon, &lat, &alt);

	return IO::SDK::Coordinates::Planetographic(lon, lat, alt);
}
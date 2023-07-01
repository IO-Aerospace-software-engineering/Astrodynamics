/**
 * @file TestManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief This class is an helper to test maneuver. You can define manually DeltaV required by maneuver
 * @version 0.x
 * @date 2021-03-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#include <ManeuverBase.h>
#include <Spacecraft.h>
#include <Engine.h>
#include <TDB.h>
#include <OrbitalParameters.h>
#include <ManeuverResult.h>
#include <Propagator.h>
#include <StateOrientation.h>
#include <InertialFrames.h>
#include <StateOrientation.h>

#include <memory>
#include<Macros.h>

class TestManeuver : public IO::Astrodynamics::Maneuvers::ManeuverBase
{
private:
protected:
    void Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;
    IO::Astrodynamics::OrbitalParameters::StateOrientation ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;
    bool CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;
    /* data */
public:
    TestManeuver(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator);
    TestManeuver(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch);
    ~TestManeuver();
    IO::Astrodynamics::Maneuvers::ManeuverResult TryExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint, double deltaV);
};

TestManeuver::TestManeuver(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator) : IO::Astrodynamics::Maneuvers::ManeuverBase(engines, propagator)
{
}

TestManeuver::TestManeuver(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch) : IO::Astrodynamics::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch)
{
}

TestManeuver::~TestManeuver()
= default;

void TestManeuver::Compute([[maybe_unused]]const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
}

IO::Astrodynamics::OrbitalParameters::StateOrientation TestManeuver::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return IO::Astrodynamics::OrbitalParameters::StateOrientation{maneuverPoint.GetEpoch(), IO::Astrodynamics::Frames::InertialFrames::GetICRF()};
}

bool TestManeuver::CanExecute([[maybe_unused]]const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return true;
}

IO::Astrodynamics::Maneuvers::ManeuverResult TestManeuver::TryExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint, double deltaV)
{
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>(deltaV, 0, 0);
    return IO::Astrodynamics::Maneuvers::ManeuverBase::TryExecute(maneuverPoint);
}

/**
 * @file TestManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief This class is an helper to test maneuver. You can define manually DeltaV required by maneuver
 * @version 0.1
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

class TestManeuver : public IO::SDK::Maneuvers::ManeuverBase
{
private:
protected:
    void Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;
    IO::SDK::OrbitalParameters::StateOrientation ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;
    bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;
    /* data */
public:
    TestManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine*> &engines, IO::SDK::Propagators::Propagator &propagator);
    TestManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine*> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch);
    ~TestManeuver();
    IO::SDK::Maneuvers::ManeuverResult TryExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint, double deltaV);
};

TestManeuver::TestManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine*> &engines, IO::SDK::Propagators::Propagator &propagator) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator)
{
}

TestManeuver::TestManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine*> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch)
{
}

TestManeuver::~TestManeuver()
= default;

void TestManeuver::Compute([[maybe_unused]]const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
}

IO::SDK::OrbitalParameters::StateOrientation TestManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return IO::SDK::OrbitalParameters::StateOrientation{maneuverPoint.GetEpoch(), IO::SDK::Frames::InertialFrames::GetICRF()};
}

bool TestManeuver::CanExecute([[maybe_unused]]const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return true;
}

IO::SDK::Maneuvers::ManeuverResult TestManeuver::TryExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint, double deltaV)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(deltaV, 0, 0);
    return IO::SDK::Maneuvers::ManeuverBase::TryExecute(maneuverPoint);
}

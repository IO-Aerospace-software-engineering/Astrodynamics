/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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
    IO::Astrodynamics::Math::Vector3D ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters& orbitalParameters) override;
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
    return IO::Astrodynamics::OrbitalParameters::StateOrientation{maneuverPoint.GetEpoch(), IO::Astrodynamics::Frames::InertialFrames::ICRF()};
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

IO::Astrodynamics::Math::Vector3D TestManeuver::ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParameters)
{
    return IO::Astrodynamics::Math::Vector3D();
}


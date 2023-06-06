/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <PropagatorException.h>
#include <ManeuverBase.h>

using namespace std::chrono_literals;

IO::Astrodynamics::Propagators::Propagator::Propagator(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft, const IO::Astrodynamics::Integrators::IntegratorBase &integrator,
                                             const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &window)
        : m_spacecraft{spacecraft}, m_integrator{const_cast<IO::Astrodynamics::Integrators::IntegratorBase &>(integrator)}, m_window{window}
{
    m_StateOrientations.emplace_back();
}

void IO::Astrodynamics::Propagators::Propagator::SetStandbyManeuver(IO::Astrodynamics::Maneuvers::ManeuverBase *standbyManeuver)
{
    m_standbyManeuver = standbyManeuver;
}

void IO::Astrodynamics::Propagators::Propagator::Propagate()
{
    //Initialize state vector
    IO::Astrodynamics::OrbitalParameters::StateVector stateVector{m_spacecraft.GetOrbitalParametersAtEpoch()->ToStateVector(m_window.GetStartDate())};
    m_stateVectors.push_back(stateVector);

    // Initial alignment, Spacecraft back points toward the center of motion
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(stateVector.GetPosition().Normalize().To(m_spacecraft.Front), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0),
                                                          stateVector.GetEpoch(), stateVector.GetFrame());

    //TODO
    // Initial alignment, Spacecraft is aligned to ICRF frame
    //    IO::Astrodynamics::OrbitalParameters::StateOrientation initialAttitude(IO::Astrodynamics::Math::Quaternion(1.0, 0.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D::Zero, stateVector.GetEpoch(),
    //                                                                 Frames::InertialFrames::GetICRF());
    AddStateOrientation(attitude);

    //Update Spacecraft orbital parameters
    while (stateVector.GetEpoch() < m_window.GetEndDate())
    {
        if (m_standbyManeuver)
        {
            //if maneuver occurs, state vectors collection will be automatically updated by maneuver object
            auto result = m_standbyManeuver->TryExecute(stateVector);
            if (!result.IsValid() && !result.CanRetryLater())
            {
                throw IO::Astrodynamics::Exception::PropagatorException("Maneuver can't be executed for this reason : " + result.GetMessage());
            }
        }

        //Integrate vector state
        stateVector = m_integrator.Integrate(m_spacecraft, m_stateVectors.back());
        m_stateVectors.push_back(stateVector);
    }

    //Write state vector data
    m_spacecraft.WriteEphemeris(m_stateVectors);

    //Write orientation data
    //Add the latest known orientation at the end of kernel
    auto latestAttitude = m_StateOrientations.back().back();
    AddStateOrientation(
            IO::Astrodynamics::OrbitalParameters::StateOrientation(latestAttitude.GetQuaternion(), IO::Astrodynamics::Math::Vector3D::Zero, m_window.GetEndDate(), latestAttitude.GetFrame()));
    m_spacecraft.WriteOrientations(m_StateOrientations);
}

const IO::Astrodynamics::OrbitalParameters::StateVector *IO::Astrodynamics::Propagators::Propagator::FindNearestLowerStateVector(const IO::Astrodynamics::Time::TDB &epoch) const
{
    if (m_stateVectors.empty())
    {
        return nullptr;
    }
    auto it = (m_stateVectors.end() - 1);

    if (epoch < m_stateVectors.begin()->GetEpoch())
    {
        return nullptr;
    }
    while (it->GetEpoch() > epoch)
    {
        it--;
    }

    return &(*it);
}

void IO::Astrodynamics::Propagators::Propagator::AddStateVector(const IO::Astrodynamics::OrbitalParameters::StateVector &sv)
{
    //Check if state vector to add is after previous state vector
    if (m_stateVectors.empty() || m_stateVectors.back().GetEpoch() < sv.GetEpoch())
    {
        m_stateVectors.push_back(sv);
    }
}

void IO::Astrodynamics::Propagators::Propagator::AddStateOrientation(const IO::Astrodynamics::OrbitalParameters::StateOrientation &so)
{
    if (!m_StateOrientations.empty() && !m_StateOrientations.back().empty() && m_StateOrientations.back().back().GetEpoch() >= so.GetEpoch())
    {
        m_StateOrientations.back().erase(--m_StateOrientations.back().end());
    }

    m_StateOrientations.back().push_back(so);
}

void IO::Astrodynamics::Propagators::Propagator::EraseDataFromEpochToEnd(const IO::Astrodynamics::Time::DateTime &epoch)
{
    if (m_stateVectors.empty())
    {
        return;
    }

    for (long i = m_stateVectors.size() - 1; i >= 0; i--)
    {
        if (m_stateVectors.at(i).GetEpoch() < epoch)
        {
            break;
        }
        m_stateVectors.erase(m_stateVectors.begin() + i);
    }
}

const std::vector<IO::Astrodynamics::OrbitalParameters::StateVector> &IO::Astrodynamics::Propagators::Propagator::GetStateVectors() const
{
    return m_stateVectors;
}

const IO::Astrodynamics::OrbitalParameters::StateOrientation *IO::Astrodynamics::Propagators::Propagator::GetLatestStateOrientation() const
{
    if (m_StateOrientations.empty() || m_StateOrientations.back().empty())
    {
        return nullptr;
    }

    return &m_StateOrientations.back().back();
}

const std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> &IO::Astrodynamics::Propagators::Propagator::GetStateOrientations() const
{
    return m_StateOrientations;
}

void IO::Astrodynamics::Propagators::Propagator::ClearStateOrientations()
{
    for (auto so: m_StateOrientations)
    {
        so.clear();
    }
}

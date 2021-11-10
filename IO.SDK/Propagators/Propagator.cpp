/**
 * @file Propagator.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Propagator.h>
#include <PropagatorException.h>
#include <algorithm>
#include <ManeuverBase.h>
#include <ZenithAttitude.h>

using namespace std::chrono_literals;

IO::SDK::Propagators::Propagator::Propagator(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, IO::SDK::Integrators::IntegratorBase &integrator, const IO::SDK::Time::Window<IO::SDK::Time::TDB> &window)
    : m_spacecraft{spacecraft}, m_integrator{integrator}, m_window{window}
{
    m_StateOrientations.push_back(std::vector<IO::SDK::OrbitalParameters::StateOrientation>{});
}

void IO::SDK::Propagators::Propagator::SetStandbyManeuver(IO::SDK::Maneuvers::ManeuverBase *standbyManeuver)
{
    m_standbyManeuver = standbyManeuver;
}

void IO::SDK::Propagators::Propagator::Propagate()
{
    //Initialize state vector
    IO::SDK::OrbitalParameters::StateVector stateVector{m_spacecraft.GetOrbitalParametersAtEpoch()->GetStateVector(m_window.GetStartDate())};
    m_stateVectors.push_back(stateVector);

    // Initial alignment, spacecraft back points toward the earth
    IO::SDK::OrbitalParameters::StateOrientation attitude(m_spacecraft.Front.To(stateVector.GetPosition().Normalize()), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), stateVector.GetEpoch(), stateVector.GetFrame());
    AddStateOrientation(attitude);

    //Update spacecraft orbital parameters
    while (stateVector.GetEpoch() < m_window.GetEndDate())
    {
        if (m_standbyManeuver)
        {
            //if maneuver occurs, state vectors collection will be automatically updated by maneuver object
            auto result = m_standbyManeuver->TryExecute(stateVector);
            if (!result.IsValid() && !result.CanRetryLater())
            {
                throw IO::SDK::Exception::PropagatorException("Maneuver can't be executed for this reason : " + result.GetMessage());
            }
        }

        //Integrate vector state
        stateVector = m_integrator.Integrate(m_spacecraft, m_stateVectors.back());
        m_stateVectors.push_back(stateVector);
    }

    //Write state vector data
    m_spacecraft.WriteEphemeris(m_stateVectors);

    //Write orientation data
    m_spacecraft.WriteOrientations(m_StateOrientations);
}

const IO::SDK::OrbitalParameters::StateVector *IO::SDK::Propagators::Propagator::FindNearestLowerStateVector(const IO::SDK::Time::TDB &epoch) const
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

void IO::SDK::Propagators::Propagator::AddStateVector(const IO::SDK::OrbitalParameters::StateVector &sv)
{
    //Check if state vector to add is after previous state vector
    if (m_stateVectors.empty() || m_stateVectors.back().GetEpoch() < sv.GetEpoch())
    {
        m_stateVectors.push_back(sv);
    }
}

void IO::SDK::Propagators::Propagator::AddStateOrientation(const IO::SDK::OrbitalParameters::StateOrientation &so)
{
    if (!m_StateOrientations.empty() && !m_StateOrientations.back().empty() && m_StateOrientations.back().back().GetEpoch() >= so.GetEpoch())
    {
        m_StateOrientations.back().erase(--m_StateOrientations.back().end());
    }

    m_StateOrientations.back().push_back(so);
}

void IO::SDK::Propagators::Propagator::EraseDataFromEpochToEnd(const IO::SDK::Time::DateTime &epoch)
{
    if (m_stateVectors.empty())
    {
        return;
    }

    for (int i = m_stateVectors.size() - 1; i >= 0; i--)
    {
        if (m_stateVectors.at(i).GetEpoch() < epoch)
        {
            break;
        }
        m_stateVectors.erase(m_stateVectors.begin() + i);
    }
}

const std::vector<IO::SDK::OrbitalParameters::StateVector> &IO::SDK::Propagators::Propagator::GetStateVectors() const
{
    return m_stateVectors;
}

const IO::SDK::OrbitalParameters::StateOrientation *IO::SDK::Propagators::Propagator::GetLatestStateOrientation() const
{
    if (m_StateOrientations.size() == 0 || m_StateOrientations.back().size() == 0)
    {
        return nullptr;
    }

    return &m_StateOrientations.back().back();
}
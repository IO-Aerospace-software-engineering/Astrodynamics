/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <ManeuverBase.h>
#include <Constants.h>

#include <TooEarlyManeuverException.h>

using namespace std::literals::chrono_literals;

IO::Astrodynamics::Maneuvers::ManeuverBase::ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator)
        : m_engines{engines}, m_spacecraft{engines[0]->GetFuelTank().GetSpacecraft()}, m_propagator{propagator}
{
    for (auto &&engine: m_engines)
    {
        m_fuelTanks.insert(&engine->GetFuelTank());
    }

    //Create dynamics fuel tank for spread thrust compute
    for (auto &&engine: m_engines)
    {
        m_dynamicFuelTanks[&engine->GetFuelTank()].EquivalentFuelFlow += engine->GetFuelFlow();
    }
}

IO::Astrodynamics::Maneuvers::ManeuverBase::ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                               const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration) : ManeuverBase(std::move(engines), propagator)
{
    const_cast<IO::Astrodynamics::Time::TimeSpan &>(m_attitudeHoldDuration) = attitudeHoldDuration;
}

IO::Astrodynamics::Maneuvers::ManeuverBase::ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                               const IO::Astrodynamics::Time::TDB &minimumEpoch) : ManeuverBase(std::move(engines), propagator)
{
    m_minimumEpoch = std::make_unique<IO::Astrodynamics::Time::TDB>(minimumEpoch);
}

IO::Astrodynamics::Maneuvers::ManeuverBase::ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                               const IO::Astrodynamics::Time::TDB &minimumEpoch, const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration) : ManeuverBase(std::move(engines), propagator,
                                                                                                                                                           minimumEpoch)
{
    const_cast<IO::Astrodynamics::Time::TimeSpan &>(m_attitudeHoldDuration) = attitudeHoldDuration;
}

void IO::Astrodynamics::Maneuvers::ManeuverBase::Handle(const IO::Astrodynamics::Time::TDB &notBeforeEpoch)
{
    if (m_minimumEpoch)
    {
        //current minimum epoch is updated only if new notBeforeEpoch is greater
        if (*m_minimumEpoch < notBeforeEpoch)
        {
            m_minimumEpoch.reset(new IO::Astrodynamics::Time::TDB(notBeforeEpoch));
        }
    } else
    {
        //We initialize minimum epoch
        m_minimumEpoch = std::make_unique<IO::Astrodynamics::Time::TDB>(notBeforeEpoch);
    }
    m_propagator.SetStandbyManeuver(this);
}

IO::Astrodynamics::Maneuvers::ManeuverResult IO::Astrodynamics::Maneuvers::ManeuverBase::TryExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::Astrodynamics::Maneuvers::ManeuverResult result;

    try
    {
        //Check if maneuver can be executed at this point
        if (!CanExecute(maneuverPoint))
        {
            // result.SetInvalid("Maneuver can't be executed at this point");
            throw IO::Astrodynamics::Exception::TooEarlyManeuverException("");
        }

        //Compute maneuver parameters
        Compute(maneuverPoint);

        //Check if maneuver parameters are compatible with vessel state
        auto validationResult = Validate();

        if (!validationResult.IsValid())
        {
            result.SetInvalid(validationResult.GetMessage());
            return result;
        }

        //Complete maneuver operations and write data to propagator
        ExecuteAt(maneuverPoint);

        if (m_nextManeuver)
        {
            //Maneuver is complete, next maneuver will be handled by propagator and can't be executed before end of this maneuver
            m_nextManeuver->Handle(m_maneuverWindow->GetEndDate());
        } else
        {
            m_propagator.SetStandbyManeuver(nullptr);
        }

        result.SetValid("Maneuver successfully executed");
    }
    catch (const IO::Astrodynamics::Exception::TooEarlyManeuverException &e)
    {
        result.SetTooEarly();
    }
    catch (const std::exception &e)
    {
        result.SetInvalid(e.what());
    }

    return result;
}

IO::Astrodynamics::Maneuvers::ManeuverResult IO::Astrodynamics::Maneuvers::ManeuverBase::Validate()
{
    //Get delta V available
    const double deltaVAvailable{IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaV(GetRemainingAvgISP(), m_spacecraft.GetMass(), m_spacecraft.GetDryOperatingMass())};

    //Check if available delta V is enough to execute maneuver
    if (deltaVAvailable < m_deltaV->Magnitude())
    {
        std::string message{"No enought delta V available. Required " + std::to_string(m_deltaV->Magnitude()) + " | Available : " + std::to_string(deltaVAvailable)};
        return IO::Astrodynamics::Maneuvers::ManeuverResult{false, message};
    }
    std::string s;
    return IO::Astrodynamics::Maneuvers::ManeuverResult{true, s};
}

void IO::Astrodynamics::Maneuvers::ManeuverBase::ExecuteAt(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    //Compute fuel required
    m_fuelBurned = IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaM(GetRemainingAvgISP(), m_spacecraft.GetMass(), m_deltaV->Magnitude());

    //Compute Thrust spreading
    SpreadThrust();

    //set maneuver window
    m_thrustWindow = std::make_unique<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>(maneuverPoint.GetEpoch() - m_thrustDuration / 2, m_thrustDuration);

    //Set attitude window
    if (m_attitudeHoldDuration > m_thrustWindow->GetLength())
    {
        m_attitudeWindow = std::make_unique<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>(m_thrustWindow->GetStartDate(), m_attitudeHoldDuration);
    } else
    {
        m_attitudeWindow = std::make_unique<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>(m_thrustWindow->GetStartDate(), m_thrustWindow->GetEndDate());
    }

    if (m_minimumEpoch && (m_attitudeWindow->GetStartDate() < *m_minimumEpoch))
    {
        throw IO::Astrodynamics::Exception::TooEarlyManeuverException("The maneuver begins too early.");
    }

    //Set maneuver window
    m_maneuverWindow = std::make_unique<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>(
            m_attitudeWindow->Merge(*m_thrustWindow).Merge(IO::Astrodynamics::Time::Window(maneuverPoint.GetEpoch(), m_maneuverHoldDuration)));

    //Find position at maneuver begin
    //Get lower value nearest maneuver begin epoch
    const IO::Astrodynamics::OrbitalParameters::OrbitalParameters *nearestLowerState = m_propagator.FindNearestLowerStateVector(m_attitudeWindow->GetStartDate());

    if (m_propagator.GetStateVectors().empty() || !nearestLowerState)
    {
        nearestLowerState = &maneuverPoint;
    }

    //propagate from the nearest value up to begin epoch
    auto beginState = nearestLowerState->ToStateVector(m_attitudeWindow->GetStartDate());

    //Compute orientation at beginning
    auto orientationBeginning = ComputeOrientation(beginState);

    //write orientation
    m_propagator.AddStateOrientation(orientationBeginning);

    //Find position at maneuver end
    //Add deltaV vector to maneuver point
    IO::Astrodynamics::OrbitalParameters::StateVector newManeuverState(maneuverPoint.GetCenterOfMotion(), maneuverPoint.ToStateVector().GetPosition(),
                                                             maneuverPoint.ToStateVector().GetVelocity() + *m_deltaV, maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());

    //Write Data in propagator
    //Erase unnecessary vector states
    m_propagator.EraseDataFromEpochToEnd(beginState.GetEpoch());

    //Write vector states at maneuver begin and end;
    m_propagator.AddStateVector(beginState);

    //Write end maneuver data only if is not punctual maneuver
    if (m_attitudeWindow->GetLength().GetSeconds().count() > 0.0)
    {
        const double stepSize{1};
        //Compute attitude until the end every 5 minutes
        auto remainingTime = m_attitudeWindow->GetEndDate() - maneuverPoint.GetEpoch();
        int interval = remainingTime.GetSeconds().count() / stepSize;
        for (int i = 1; i <= interval; ++i)
        {
            IO::Astrodynamics::Time::TimeSpan ts(std::chrono::duration<double>(i * stepSize));
            //Propagate from new maneuver point up to end maneuver epoch
            auto intermediateState = newManeuverState.ToStateVector(maneuverPoint.GetEpoch().Add(ts));

            // Compute orientation at end
            auto intermediateOrientation = ComputeOrientation(intermediateState);

            //Write orientation at end
            m_propagator.AddStateOrientation(intermediateOrientation);

            //Add state vector at end
            m_propagator.AddStateVector(intermediateState);
        }

        //Propagate from new maneuver point up to end maneuver epoch
        auto endState = newManeuverState.ToStateVector(m_attitudeWindow->GetEndDate());

        // Compute orientation at end
        auto orientationEnd = ComputeOrientation(endState);

        //Write orientation at end
        m_propagator.AddStateOrientation(orientationEnd);

        //Add state vector at end
        m_propagator.AddStateVector(endState);
    }
}

bool IO::Astrodynamics::Maneuvers::ManeuverBase::IsValid() const
{
    return m_isValid;
}

IO::Astrodynamics::Maneuvers::ManeuverBase &IO::Astrodynamics::Maneuvers::ManeuverBase::SetNextManeuver(IO::Astrodynamics::Maneuvers::ManeuverBase &nextManeuver)
{
    m_nextManeuver = &nextManeuver;
    return nextManeuver;
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> *IO::Astrodynamics::Maneuvers::ManeuverBase::GetThrustWindow() const
{
    return m_thrustWindow.get();
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> *IO::Astrodynamics::Maneuvers::ManeuverBase::GetAttitudeWindow() const
{
    return m_attitudeWindow.get();
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> *IO::Astrodynamics::Maneuvers::ManeuverBase::GetManeuverWindow() const
{
    return m_maneuverWindow.get();
}

void IO::Astrodynamics::Maneuvers::ManeuverBase::ManeuverBase::SpreadThrust()
{
    if (m_deltaV == nullptr)
    {
        return;
    }

    double cumulatedDeltaV{};
    double burnedFuel{};
    double currentAvgISP{};

    //Get the remaining thrust duration
    IO::Astrodynamics::Time::TimeSpan remainingThrustDuration{
            IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaT(GetRemainingAvgISP(), m_spacecraft.GetMass(), GetRemainingAvgFuelFlow(), m_deltaV->Magnitude())};

    //Compute spread thrust step by step
    //No analytical solution seems to exist
    while (true)
    {
        //Evaluate minimum thrust duration at each step
        IO::Astrodynamics::Time::TimeSpan minimumRemainingThrustDuration = GetMinimumRemainingThrustDuration();

        //If lower we compute thrust with available engines
        //else we have enough fuel to complete the maneuver
        if (minimumRemainingThrustDuration < remainingThrustDuration)
        {
            //We increment thrust duration used for final result
            m_thrustDuration = m_thrustDuration + minimumRemainingThrustDuration;

            //Get current average ISP
            currentAvgISP = GetRemainingAvgISP();

            //Get fuel burned during this step
            burnedFuel = Burn(minimumRemainingThrustDuration);

            //Get Spacecraft mass after burn
            double masseAfterStepBurn = m_spacecraft.GetMass();

            //We evaluate cumulated deltaV
            cumulatedDeltaV += IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaV(currentAvgISP, masseAfterStepBurn + burnedFuel, masseAfterStepBurn);

            //We compute remaining thrust duration for the next step
            remainingThrustDuration = IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaT(GetRemainingAvgISP(), masseAfterStepBurn, GetRemainingAvgFuelFlow(),
                                                                                       m_deltaV->Magnitude() - cumulatedDeltaV);

            continue;
        }
        break;
    }

    //Burn fuel from remaining thrust
    Burn(remainingThrustDuration);

    //Now maneuver can execute until the end with this configuration, we can simply add the remaining thrust duration
    m_thrustDuration = m_thrustDuration + remainingThrustDuration;
}

double IO::Astrodynamics::Maneuvers::ManeuverBase::GetRemainingAvgFuelFlow()
{
    double res{};

    for (auto &&engine: m_engines)
    {
        if (!engine->GetFuelTank().IsEmpty())
        {
            res += engine->GetFuelFlow();
        }
    }

    return res;
}

double IO::Astrodynamics::Maneuvers::ManeuverBase::GetRemainingAvgISP()
{
    double thrust{};
    for (const auto &engine: m_engines)
    {
        if (!engine->GetFuelTank().IsEmpty())
        {
            thrust += engine->GetThrust();
        }
    }

    return (thrust / IO::Astrodynamics::Constants::g0) / GetRemainingAvgFuelFlow();
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Maneuvers::ManeuverBase::GetMinimumRemainingThrustDuration()
{
    IO::Astrodynamics::Time::TimeSpan minValue{std::chrono::duration<double>(std::numeric_limits<double>::max())};

    for (auto &&tank: m_dynamicFuelTanks)
    {
        if (!tank.first->IsEmpty())
        {
            if (tank.second.GetRemainingT(tank.first->GetQuantity()) < minValue)
            {
                minValue = tank.second.GetRemainingT(tank.first->GetQuantity());
            }
        }
    }

    return minValue;
}

double IO::Astrodynamics::Maneuvers::ManeuverBase::Burn(const IO::Astrodynamics::Time::TimeSpan &duration)
{
    double totalFuelBurned{};
    for (auto engine: m_engines)
    {
        if (!engine->GetFuelTank().IsEmpty())
        {
            totalFuelBurned += engine->Burn(duration);
        }
    }

    return totalFuelBurned;
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Maneuvers::ManeuverBase::GetThrustDuration() const
{
    return m_thrustDuration;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Maneuvers::ManeuverBase::GetDeltaV() const
{
    return *m_deltaV;
}

double IO::Astrodynamics::Maneuvers::ManeuverBase::GetFuelBurned() const
{
    return m_fuelBurned;
}

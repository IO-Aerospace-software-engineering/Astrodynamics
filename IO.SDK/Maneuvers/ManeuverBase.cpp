/**
 * @file ManeuverBase.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <numeric>
#include <limits>
#include <chrono>
#include <ManeuverBase.h>

IO::SDK::Maneuvers::ManeuverBase::ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator)
    : m_engines{engines}, m_spacecraft{engines[0].GetFuelTank().GetSpacecraft()}, m_propagator{propagator}
{
    for (auto &&engine : m_engines)
    {
        m_fuelTanks.insert(&engine.GetFuelTank());
    }

    //Create dynamics fuel tank for spread thrust compute
    for (auto &&engine : m_engines)
    {
        m_dynamicFuelTanks[&engine.GetFuelTank()].EquivalentFuelFlow += engine.GetFuelFlow();
    }
}

IO::SDK::Maneuvers::ManeuverBase::ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TimeSpan &attitudeHoldDuration) : ManeuverBase(engines, propagator)
{
    const_cast<IO::SDK::Time::TimeSpan &>(m_attitudeHoldDuration) = attitudeHoldDuration;
}

IO::SDK::Maneuvers::ManeuverBase::ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch) : ManeuverBase(engines, propagator)
{
    m_minimumEpoch = std::make_unique<IO::SDK::Time::TDB>(minimumEpoch);
}

IO::SDK::Maneuvers::ManeuverBase::ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch, const IO::SDK::Time::TimeSpan &attitudeHoldDuration) : ManeuverBase(engines, propagator, minimumEpoch)
{
    const_cast<IO::SDK::Time::TimeSpan &>(m_attitudeHoldDuration) = attitudeHoldDuration;
}

void IO::SDK::Maneuvers::ManeuverBase::Handle(const IO::SDK::Time::TDB &notBeforeEpoch)
{
    if (m_minimumEpoch)
    {
        //current minimum epoch is updated only if new notBeforeEpoch is greater
        if (*m_minimumEpoch < notBeforeEpoch)
        {
            m_minimumEpoch.reset(new IO::SDK::Time::TDB(notBeforeEpoch));
        }
    }
    else
    {
        //We initialize minimum epoch
        m_minimumEpoch = std::make_unique<IO::SDK::Time::TDB>(notBeforeEpoch);
    }
    m_propagator.SetStandbyManeuver(this);
}

IO::SDK::Maneuvers::ManeuverResult IO::SDK::Maneuvers::ManeuverBase::TryExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::SDK::Maneuvers::ManeuverResult result;

    try
    {
        //Check if maneuver can be executed at this point
        if (!CanExecute(maneuverPoint))
        {
            // result.SetInvalid("Maneuver can't be executed at this point");
            result.SetTooEarly();
            return result;
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
        }
        else
        {
            m_propagator.SetStandbyManeuver(nullptr);
        }

        result.SetValid("Maneuver successfully executed");
    }
    catch (const IO::SDK::Exception::TooEarlyManeuverException &e)
    {
        result.SetTooEarly();
    }
    catch (const std::exception &e)
    {
        result.SetInvalid(e.what());
    }

    return result;
}

IO::SDK::Maneuvers::ManeuverResult IO::SDK::Maneuvers::ManeuverBase::Validate()
{
    //Get delta V available
    const double deltaVAvailable{IO::SDK::Body::Spacecraft::Engine::ComputeDeltaV(GetRemainingAvgISP(), m_spacecraft.GetMass(), m_spacecraft.GetDryOperatingMass())};

    //Check if available delta V is enough to execute maneuver
    if (deltaVAvailable < m_deltaV->Magnitude())
    {
        std::string message{"No enought delta V available. Required " + std::to_string(m_deltaV->Magnitude()) + " | Available : " + std::to_string(deltaVAvailable)};
        return IO::SDK::Maneuvers::ManeuverResult(false, message);
    }
    std::string s;
    return IO::SDK::Maneuvers::ManeuverResult(true, s);
}

void IO::SDK::Maneuvers::ManeuverBase::ExecuteAt(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    //Compute fuel required
    m_fuelBurned = IO::SDK::Body::Spacecraft::Engine::ComputeDeltaM(GetRemainingAvgISP(), m_spacecraft.GetMass(), m_deltaV->Magnitude());

    //Compute Thrust spreading
    SpreadThrust();

    //set maneuver window
    m_thrustWindow = std::make_unique<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(maneuverPoint.GetEpoch() - m_thrustDuration / 2, m_thrustDuration);


    //Set attitude window
    if (m_attitudeHoldDuration > m_thrustWindow->GetLength())
    {
        m_attitudeWindow = std::make_unique<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(m_thrustWindow->GetStartDate(), m_attitudeHoldDuration);
    }
    else
    {
        m_attitudeWindow = std::make_unique<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(m_thrustWindow->GetStartDate(), m_thrustWindow->GetEndDate());
    }

    if (m_minimumEpoch && (m_attitudeWindow->GetStartDate() < *m_minimumEpoch))
    {
        throw IO::SDK::Exception::TooEarlyManeuverException("The maneuver begins too early.");
    }

    //Set maneuver window
    m_maneuverWindow=std::make_unique<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(m_attitudeWindow->Merge(*m_thrustWindow).Merge(IO::SDK::Time::Window(maneuverPoint.GetEpoch(),m_maneuverHoldDuration)));

    //Find position at maneuver begin
    //Get lower value nearest maneuver begin epoch
    const IO::SDK::OrbitalParameters::OrbitalParameters *nearestLowerState = m_propagator.FindNearestLowerStateVector(m_attitudeWindow->GetStartDate());

    if (m_propagator.GetStateVectors().empty())
    {
        nearestLowerState = &maneuverPoint;
    }

    //propagate from nearest value up to begin epoch
    auto beginState = nearestLowerState->GetStateVector(m_attitudeWindow->GetStartDate());

    //Compute oriention at begining
    auto orientationBegining = ComputeOrientation(beginState);

    //write orientation
    m_propagator.AddStateOrientation(orientationBegining);

    //Find position at maneuver end
    //Add deltaV vector to maneuver point
    IO::SDK::OrbitalParameters::StateVector newManeuverState(maneuverPoint.GetCenterOfMotion(), maneuverPoint.GetStateVector().GetPosition(), maneuverPoint.GetStateVector().GetVelocity() + *m_deltaV, maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());

    //Write Data in propagator
    //Erase unecessary vector states
    m_propagator.EraseDataFromEpochToEnd(beginState.GetEpoch());

    //Write vector states at maneuver begin and end;
    m_propagator.AddStateVector(beginState);

    //Write end maneuver data only if is not ponctual maneuver
    if (m_attitudeWindow->GetLength().GetSeconds().count() > 0.0)
    {
        //Propagate from new maneuver point up to end maneuver epoch
        auto endState = newManeuverState.GetStateVector(m_attitudeWindow->GetEndDate());

        // Compute oriention at end
        auto orientationEnd = ComputeOrientation(endState);

        //Write orientation at end
        m_propagator.AddStateOrientation(orientationEnd);

        //Add state vector at end
        m_propagator.AddStateVector(endState);
    }
}

bool IO::SDK::Maneuvers::ManeuverBase::IsValid()
{
    return m_isValid;
}

IO::SDK::Maneuvers::ManeuverBase &IO::SDK::Maneuvers::ManeuverBase::SetNextManeuver(IO::SDK::Maneuvers::ManeuverBase &nextManeuver)
{
    m_nextManeuver = &nextManeuver;
    return nextManeuver;
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> *IO::SDK::Maneuvers::ManeuverBase::GetThrustWindow() const
{
    return m_thrustWindow.get();
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> *IO::SDK::Maneuvers::ManeuverBase::GetAttitudeWindow() const
{
    return m_attitudeWindow.get();
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> *IO::SDK::Maneuvers::ManeuverBase::GetManeuverWindow() const
{
    return m_maneuverWindow.get();
}

void IO::SDK::Maneuvers::ManeuverBase::ManeuverBase::SpreadThrust()
{
    if (m_deltaV == 0)
    {
        return;
    }

    double cumulatedDeltaV{};
    double burnedFuel{};
    double currentAvgISP{};

    //Get the remaining thrust duration
    IO::SDK::Time::TimeSpan remainingThrustDuration{IO::SDK::Body::Spacecraft::Engine::ComputeDeltaT(GetRemainingAvgISP(), m_spacecraft.GetMass(), GetRemainingAvgFuelFlow(), m_deltaV->Magnitude())};

    //Compute spread thrust step by step
    //No analytical solution seems to exist
    while (true)
    {
        //Evaluate minimum thrust duration at each step
        IO::SDK::Time::TimeSpan minimumRemainingThrustDuration = GetMinimumRemainingThrustDuration();

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

            //Get spacecraft mass after burn
            double masseAfterStepBurn = m_spacecraft.GetMass();

            //We evaluate cumulated deltaV
            cumulatedDeltaV += IO::SDK::Body::Spacecraft::Engine::ComputeDeltaV(currentAvgISP, masseAfterStepBurn + burnedFuel, masseAfterStepBurn);

            //We compute remaining thrust duration for the next step
            remainingThrustDuration = IO::SDK::Body::Spacecraft::Engine::ComputeDeltaT(GetRemainingAvgISP(), masseAfterStepBurn, GetRemainingAvgFuelFlow(), m_deltaV->Magnitude() - cumulatedDeltaV);

            continue;
        }
        break;
    }

    //Burn fuel from remaining thrust
    Burn(remainingThrustDuration);

    //Now maneuver can execute until the end with this configuration, we can simply add the remaining thrust duration
    m_thrustDuration = m_thrustDuration + remainingThrustDuration;
}

double IO::SDK::Maneuvers::ManeuverBase::GetRemainingAvgFuelFlow()
{
    double res{};

    for (auto &&engine : m_engines)
    {
        if (!engine.GetFuelTank().IsEmpty())
        {
            res += engine.GetFuelFlow();
        }
    }

    return res;
}

double IO::SDK::Maneuvers::ManeuverBase::GetRemainingAvgISP()
{
    double thrust{};
    for (const auto &engine : m_engines)
    {
        if (!engine.GetFuelTank().IsEmpty())
        {
            thrust += engine.GetThrust();
        }
    }

    return (thrust / IO::SDK::Constants::g0) / GetRemainingAvgFuelFlow();
}

IO::SDK::Time::TimeSpan IO::SDK::Maneuvers::ManeuverBase::GetMinimumRemainingThrustDuration()
{
    IO::SDK::Time::TimeSpan minValue{std::chrono::duration<double>(std::numeric_limits<double>::max())};

    for (auto &&tank : m_dynamicFuelTanks)
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

double IO::SDK::Maneuvers::ManeuverBase::Burn(const IO::SDK::Time::TimeSpan &duration)
{
    double totalFuelBurned{};
    for (auto &&engine : m_engines)
    {
        if (!engine.GetFuelTank().IsEmpty())
        {
            totalFuelBurned += const_cast<IO::SDK::Body::Spacecraft::Engine &>(engine).Burn(duration);
        }
    }

    return totalFuelBurned;
}

IO::SDK::Time::TimeSpan IO::SDK::Maneuvers::ManeuverBase::GetThrustDuration() const
{
    return m_thrustDuration;
}

IO::SDK::Math::Vector3D IO::SDK::Maneuvers::ManeuverBase::GetDeltaV() const
{
    return *m_deltaV;
}

double IO::SDK::Maneuvers::ManeuverBase::GetFuelBurned() const
{
    return m_fuelBurned;
}

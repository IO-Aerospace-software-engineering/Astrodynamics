/**
 * @file TLE.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "TLE.h"
#include <algorithm>
#include <SpiceUsr.h>
#include <Constants.h>
#include <InertialFrames.h>

IO::SDK::OrbitalParameters::TLE::TLE(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfmotion, std::string lines[3]) : OrbitalParameters(centerOfmotion,
                                                                                                                                                    Time::TDB(std::chrono::duration<double>(
                                                                                                                                                            0.0)),
                                                                                                                                                    IO::SDK::Frames::InertialFrames::GetICRF()),
                                                                                                                                  m_satelliteName{lines[0]}
{
    //Build lines
    size_t length = lines[1].length();
    for (size_t i = 0; i < 2; i++)
    {
        std::copy(lines[i + 1].data(), lines[i + 1].data() + length, m_lines[i]);
        m_lines[i][length] = '\0';
    }

    //Set elements
    SpiceDouble epoch;
    getelm_c(m_firstYear, length + 1, m_lines, &epoch, m_elements);

    const_cast<IO::SDK::Time::TDB &>(m_epoch) = Time::TDB(std::chrono::duration<double>(epoch));

    //Set period
    m_period = IO::SDK::Time::TimeSpan(std::chrono::duration<double>(IO::SDK::Constants::_2PI / (m_elements[8] / 60.0)));

    //Set stateVector
    m_stateVector = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(GetStateVector(m_epoch));

    // Set conical elements
    m_conicOrbitalElements = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(*m_stateVector);
}

std::string IO::SDK::OrbitalParameters::TLE::GetSatelliteName() const
{
    return m_satelliteName;
}

double IO::SDK::OrbitalParameters::TLE::GetBalisticCoefficient() const
{
    return m_elements[0];
}

double IO::SDK::OrbitalParameters::TLE::GetSecondDerivativeOfMeanMotion() const
{
    return m_elements[1];
}

double IO::SDK::OrbitalParameters::TLE::GetDragTerm() const
{
    return m_elements[2];
}

IO::SDK::Time::TimeSpan IO::SDK::OrbitalParameters::TLE::GetPeriod() const
{
    return m_period;
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::TLE::GetSpecificAngularMomentum() const
{
    return m_stateVector->GetSpecificAngularMomentum();
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::TLE::GetStateVector(const IO::SDK::Time::TDB &epoch) const
{
    SpiceDouble stateVector[6];

    SpiceDouble ep = epoch.GetSecondsFromJ2000().count();
    evsgp4_c(ep, const_cast<SpiceDouble *>(m_geophysics), const_cast<SpiceDouble *>(m_elements), stateVector);

    for (double & sv : stateVector)
    {
        sv = sv * 1000.0;
    }

    return StateVector{m_centerOfMotion, stateVector, epoch, m_frame};
}

double IO::SDK::OrbitalParameters::TLE::GetEccentricity() const
{
    return m_elements[5];
}

double IO::SDK::OrbitalParameters::TLE::GetSemiMajorAxis() const
{
    return m_conicOrbitalElements->GetSemiMajorAxis();
}

double IO::SDK::OrbitalParameters::TLE::GetInclination() const
{
    return m_elements[3];
}

double IO::SDK::OrbitalParameters::TLE::GetPeriapsisArgument() const
{
    return m_elements[6];
}

double IO::SDK::OrbitalParameters::TLE::GetRightAscendingNodeLongitude() const
{
    return m_elements[4];
}

double IO::SDK::OrbitalParameters::TLE::GetMeanAnomaly() const
{
    return m_elements[7];
}

double IO::SDK::OrbitalParameters::TLE::GetSpecificOrbitalEnergy() const
{
    return m_stateVector->GetSpecificOrbitalEnergy();
}

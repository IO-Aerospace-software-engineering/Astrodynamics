/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <TLE.h>
#include <algorithm>
#include <InertialFrames.h>

IO::Astrodynamics::OrbitalParameters::TLE::TLE(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfmotion, std::string lines[3]) : OrbitalParameters(centerOfmotion,
                                                                                                                                                    Time::TDB(std::chrono::duration<double>(
                                                                                                                                                            0.0)),
                                                                                                                                                                        IO::Astrodynamics::Frames::InertialFrames::ICRF()),
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

    const_cast<IO::Astrodynamics::Time::TDB &>(m_epoch) = Time::TDB(std::chrono::duration<double>(epoch));

    //Set period
    m_period = IO::Astrodynamics::Time::TimeSpan(std::chrono::duration<double>(IO::Astrodynamics::Constants::_2PI / (m_elements[8] / 60.0)));

    //Set stateVector
    m_stateVector = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(ToStateVector(m_epoch));

    // Set conical elements
    m_conicOrbitalElements = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(*m_stateVector);
}

std::string IO::Astrodynamics::OrbitalParameters::TLE::GetSatelliteName() const
{
    return m_satelliteName;
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetBalisticCoefficient() const
{
    return m_elements[0];
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetSecondDerivativeOfMeanMotion() const
{
    return m_elements[1];
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetDragTerm() const
{
    return m_elements[2];
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::OrbitalParameters::TLE::GetPeriod() const
{
    return m_period;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::TLE::GetSpecificAngularMomentum() const
{
    return m_stateVector->GetSpecificAngularMomentum();
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::TLE::ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const
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

double IO::Astrodynamics::OrbitalParameters::TLE::GetEccentricity() const
{
    return m_elements[5];
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetSemiMajorAxis() const
{
    return m_conicOrbitalElements->GetSemiMajorAxis();
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetInclination() const
{
    return m_elements[3];
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetPeriapsisArgument() const
{
    return m_elements[6];
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetRightAscendingNodeLongitude() const
{
    return m_elements[4];
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetMeanAnomaly() const
{
    return m_elements[7];
}

double IO::Astrodynamics::OrbitalParameters::TLE::GetSpecificOrbitalEnergy() const
{
    return m_stateVector->GetSpecificOrbitalEnergy();
}

/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <EquinoctialElements.h>
#include <Constants.h>

IO::Astrodynamics::OrbitalParameters::EquinoctialElements::EquinoctialElements(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, const IO::Astrodynamics::Time::TDB &epoch,
                                                                     const double semiMajorAxis, const double h, const double k, const double p, const double q, const double L,
                                                                     const double periapsisLongitudeRate, const double ascendingNodeLongitudeRate,
                                                                     const double rightAscensionOfThePole, const double declinationOfThePole, const IO::Astrodynamics::Frames::Frames &frame)
        : OrbitalParameters(centerOfMotion, epoch, frame), m_semiMajorAxis{semiMajorAxis}, m_h{h}, m_k{k}, m_p{p}, m_q{q}, m_L{L}, m_periapsisLongitudeRate{periapsisLongitudeRate},
          m_rightAscensionOfThePole{rightAscensionOfThePole}, m_declinationOfThePole{declinationOfThePole}, m_ascendingNodeLongitudeRate{ascendingNodeLongitudeRate} {
    const_cast<double &>(m_meanAnomalyRate) = std::sqrt(centerOfMotion->GetMu() / semiMajorAxis) / semiMajorAxis;

    m_elements[0] = semiMajorAxis;
    m_elements[1] = h;
    m_elements[2] = k;
    m_elements[3] = L;
    m_elements[4] = p;
    m_elements[5] = q;
    m_elements[6] = m_periapsisLongitudeRate;
    m_elements[7] = m_meanAnomalyRate + m_periapsisLongitudeRate + m_ascendingNodeLongitudeRate;
    m_elements[8] = m_ascendingNodeLongitudeRate;

    m_period = IO::Astrodynamics::Time::TimeSpan(std::chrono::duration<double>(IO::Astrodynamics::Constants::_2PI * (std::sqrt(std::pow(semiMajorAxis, 3) / centerOfMotion->GetMu()))));
}

IO::Astrodynamics::OrbitalParameters::EquinoctialElements::EquinoctialElements(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, const double semiMajorAxis,
                                                                     const double eccentricity, const double inclination, const double peregeeArgument, const double longitudeAN,
                                                                     const double meanAnomaly, const double periapsisLongitudeRate, const double ascendingNodeLongitudeRate,
                                                                     const double rightAscensionOfThePole, const double declinationOfThePole, const IO::Astrodynamics::Time::TDB &epoch,
                                                                     const IO::Astrodynamics::Frames::Frames &frame) : OrbitalParameters(centerOfMotion, epoch, frame),
                                                                                                             m_semiMajorAxis{semiMajorAxis},
                                                                                                             m_periapsisLongitudeRate{periapsisLongitudeRate},
                                                                                                             m_rightAscensionOfThePole{rightAscensionOfThePole},
                                                                                                             m_declinationOfThePole{declinationOfThePole},
                                                                                                             m_ascendingNodeLongitudeRate{ascendingNodeLongitudeRate} {

    const_cast<double &>(m_h) = eccentricity * sin(peregeeArgument + longitudeAN);
    const_cast<double &>(m_k) = eccentricity * cos(peregeeArgument + longitudeAN);
    const_cast<double &>(m_p) = tan(inclination * 0.5) * sin(longitudeAN);
    const_cast<double &>(m_q) = tan(inclination * 0.5) * cos(longitudeAN);
    const_cast<double &>(m_L) = meanAnomaly + peregeeArgument + longitudeAN;
    const_cast<double &>(m_meanAnomalyRate) = std::sqrt(centerOfMotion->GetMu() / semiMajorAxis) / semiMajorAxis;

    m_elements[0] = semiMajorAxis;
    m_elements[1] = m_h;
    m_elements[2] = m_k;
    m_elements[3] = m_L;
    m_elements[4] = m_p;
    m_elements[5] = m_q;
    m_elements[6] = m_periapsisLongitudeRate;
    m_elements[7] = m_meanAnomalyRate + m_periapsisLongitudeRate + m_ascendingNodeLongitudeRate;
    m_elements[8] = m_ascendingNodeLongitudeRate;
    m_period = IO::Astrodynamics::Time::TimeSpan(std::chrono::duration<double>(IO::Astrodynamics::Constants::_2PI * (std::sqrt(std::pow(semiMajorAxis, 3) / centerOfMotion->GetMu()))));
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetPeriod() const {
    return m_period;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetSpecificAngularMomentum() const {
    return ToStateVector(m_epoch).GetSpecificAngularMomentum();
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::EquinoctialElements::ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const {
    double state[6]{};
    eqncpv_c(epoch.GetSecondsFromJ2000().count(), m_epoch.GetSecondsFromJ2000().count(), m_elements, m_rightAscensionOfThePole, m_declinationOfThePole, state);
    return StateVector{m_centerOfMotion, IO::Astrodynamics::Math::Vector3D(state[0], state[1], state[2]), IO::Astrodynamics::Math::Vector3D(state[3], state[4], state[5]), epoch, m_frame};
}

double IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetEccentricity() const {
    return sqrt(m_h * m_h + m_k * m_k);
}

double IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetInclination() const {
    return atan2(2 * sqrt(m_q * m_q + m_p * m_p), 1 - m_q * m_q - m_p * m_p);
}

double IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetPeriapsisArgument() const {
    return atan2(m_h * m_q - m_k * m_p, m_k * m_q + m_h * m_p);
}

double IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetRightAscendingNodeLongitude() const {
    return atan2(m_p, m_q);
}

double IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetMeanAnomaly() const {
    return m_L - atan2(m_h, m_k);
}

double IO::Astrodynamics::OrbitalParameters::EquinoctialElements::GetSpecificOrbitalEnergy() const {
    return ToStateVector(m_epoch).GetSpecificOrbitalEnergy();
}

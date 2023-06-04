/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef EQUINOCTIAL_ELEMENTS_H
#define EQUINOCTIAL_ELEMENTS_H

#include <OrbitalParameters.h>
#include <CelestialBody.h>
#include <StateVector.h>
#include <TDB.h>
#include <cmath>
#include <memory>

namespace IO::SDK::OrbitalParameters
{
	class EquinoctialElements final : public OrbitalParameters
	{
	private:
		const double m_semiMajorAxis{};
		const double m_h{};
		const double m_k{};
		const double m_p{};
		const double m_q{};
		const double m_L{};
		const double m_periapsisLongitudeRate{};
		const double m_meanAnomalyRate{};
		const double m_rightAscensionOfThePole{};
		const double m_declinationOfThePole{};
		const double m_ascendingNodeLongitudeRate{};	

		IO::SDK::Time::TimeSpan m_period;
		double m_elements[9]{};

	public:
		EquinoctialElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Time::TDB &epoch, double semiMajorAxis, double h, double k, double p, double q, double L, double periapsisLongitudeRate, double ascendingNodeLongitudeRate, double rightAscensionOfThePole, double declinationOfThePole, const IO::SDK::Frames::Frames &frame);

		EquinoctialElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, double semiMajorAxis, double eccentricity, double inclination, double perigeeArgument, double longitudeAN, double meanAnomaly, double periapsisLongitudeRate, double ascendingNodeLongitudeRate, double rightAscensionOfThePole, double declinationOfThePole, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		/// <summary>
		/// Get h coefficient
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetH() const { return m_h; }

		/// <summary>
		/// Get k coefficient
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetK() const { return m_k; }

		/// <summary>
		/// Get p coefficient
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetP() const { return m_p; }

		/// <summary>
		/// Get q coefficient
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetQ() const { return m_q; }

		/// <summary>
		/// Get longitude coefficient
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetL() const { return m_L; }

		/// <summary>
		/// Get periapsis longitude rate
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetPeriapsisLongitudeRate() const { return m_periapsisLongitudeRate; }

		/// <summary>
		/// Get ascending node longitude rate
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetAscendingNodeLongitudeRate() const { return m_ascendingNodeLongitudeRate; }

		/// <summary>
		/// Get mean anomaly rate
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetMeanAnomalyRate() const { return m_meanAnomalyRate; }

		/// <summary>
		/// Get right ascension of the pole of the reference plane
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetRightAscensionOfPole() const { return m_rightAscensionOfThePole; }

		/// <summary>
		/// Get declination of the pole of the reference plane
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] inline double GetDeclinationOfPole() const { return m_declinationOfThePole; }

		// Inherited via OrbitalParameters
		[[nodiscard]] IO::SDK::Time::TimeSpan GetPeriod() const override;

		/// <summary>
		/// Get specific angular moment
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] IO::SDK::Math::Vector3D GetSpecificAngularMomentum() const override;

		/// <summary>
		/// Get state vector
		/// </summary>
		/// <param name="epoch"></param>
		/// <returns></returns>
		[[nodiscard]] StateVector ToStateVector(const IO::SDK::Time::TDB &epoch) const override;

		/// <summary>
		/// Get eccentricity
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetEccentricity() const override;

		/// <summary>
		/// Get semi major axis
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetSemiMajorAxis() const override { return m_semiMajorAxis; }

		/// <summary>
		/// Get inclination
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetInclination() const override;

		/// <summary>
		/// Get periapsis argument
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetPeriapsisArgument() const override;

		/// <summary>
		/// Get right ascending node longitude
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetRightAscendingNodeLongitude() const override;

		//Get non pure virtual function accessible
		using OrbitalParameters::GetMeanAnomaly;
		/// <summary>
		/// Get mean anomaly at epoch
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetMeanAnomaly() const override;

		/// <summary>
		/// Get specific orbital energy
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetSpecificOrbitalEnergy() const override;

		using IO::SDK::OrbitalParameters::OrbitalParameters::ToStateVector;
	};
}
#endif

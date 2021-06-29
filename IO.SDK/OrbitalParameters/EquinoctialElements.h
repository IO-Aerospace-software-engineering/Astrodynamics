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
	class EquinoctialElements : public OrbitalParameters
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
		EquinoctialElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Time::TDB &epoch, const double semiMajorAxis, const double h, const double k, const double p, const double q, const double L, const double periapsisLongitudeRate, const double ascendingNodeLongitudeRate, const double rightAscensionOfThePole, const double declinationOfThePole, const IO::SDK::Frames::Frames &frame);

		EquinoctialElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const double semiMajorAxis, const double eccentricity, const double inclination, const double peregeeArgument, const double longitudeAN, const double meanAnomaly, const double periapsisLongitudeRate, const double ascendingNodeLongitudeRate, const double rightAscensionOfThePole, const double declinationOfThePole, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		/// <summary>
		/// Get h coefficient
		/// </summary>
		/// <returns></returns>
		double GetH() const { return m_h; }

		/// <summary>
		/// Get k coefficient
		/// </summary>
		/// <returns></returns>
		double GetK() const { return m_k; }

		/// <summary>
		/// Get p coefficient
		/// </summary>
		/// <returns></returns>
		double GetP() const { return m_p; }

		/// <summary>
		/// Get q coefficient
		/// </summary>
		/// <returns></returns>
		double GetQ() const { return m_q; }

		/// <summary>
		/// Get longitude coefficient
		/// </summary>
		/// <returns></returns>
		double GetL() const { return m_L; }

		/// <summary>
		/// Get periapsis longitude rate
		/// </summary>
		/// <returns></returns>
		double GetPeriapsisLongitudeRate() const { return m_periapsisLongitudeRate; }

		/// <summary>
		/// Get ascending node longitude rate
		/// </summary>
		/// <returns></returns>
		double GetAscendingNodeLongitudeRate() const { return m_ascendingNodeLongitudeRate; }

		/// <summary>
		/// Get mean anomaly rate
		/// </summary>
		/// <returns></returns>
		double GetMeanAnomalyRate() const { return m_meanAnomalyRate; }

		/// <summary>
		/// Get right ascension of the pole of the reference plane
		/// </summary>
		/// <returns></returns>
		double GetRightAscensionOfPole() const { return m_rightAscensionOfThePole; }

		/// <summary>
		/// Get declination of the pole of the reference plane
		/// </summary>
		/// <returns></returns>
		double GetDeclinationOfPole() const { return m_declinationOfThePole; }

		// Inherited via OrbitalParameters
		virtual IO::SDK::Time::TimeSpan GetPeriod() const override;

		/// <summary>
		/// Get specific angular moment
		/// </summary>
		/// <returns></returns>
		virtual IO::SDK::Math::Vector3D GetSpecificAngularMomentum() const override;

		/// <summary>
		/// Get state vector
		/// </summary>
		/// <param name="epoch"></param>
		/// <returns></returns>
		virtual StateVector GetStateVector(const IO::SDK::Time::TDB &epoch) const override;

		/// <summary>
		/// Get eccentricity
		/// </summary>
		/// <returns></returns>
		virtual double GetEccentricity() const override;

		/// <summary>
		/// Get semi major axis
		/// </summary>
		/// <returns></returns>
		virtual double GetSemiMajorAxis() const override { return m_semiMajorAxis; }

		/// <summary>
		/// Get inclination
		/// </summary>
		/// <returns></returns>
		virtual double GetInclination() const override;

		/// <summary>
		/// Get periapsis argument
		/// </summary>
		/// <returns></returns>
		virtual double GetPeriapsisArgument() const override;

		/// <summary>
		/// Get right ascending node longitude
		/// </summary>
		/// <returns></returns>
		virtual double GetRightAscendingNodeLongitude() const override;

		//Get non pure virtual function accessible
		using OrbitalParameters::GetMeanAnomaly;
		/// <summary>
		/// Get mean anomaly at epoch
		/// </summary>
		/// <returns></returns>
		virtual double GetMeanAnomaly() const override;

		/// <summary>
		/// Get specific orbital energy
		/// </summary>
		/// <returns></returns>
		virtual double GetSpecificOrbitalEnergy() const override;

		using IO::SDK::OrbitalParameters::OrbitalParameters::GetStateVector;
	};
}
#endif

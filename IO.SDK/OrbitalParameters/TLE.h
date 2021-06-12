/**
 * @file TLE.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef TLE_H
#define TLE_H

#include <memory>

#include <OrbitalParameters.h>
#include <CelestialBody.h>
#include <StateVector.h>
#include <string>
#include <SpiceUsr.h>
#include <ConicOrbitalElements.h>
#include <chrono>
#include <TDB.h>

namespace IO::SDK::OrbitalParameters
{
	class TLE : public IO::SDK::OrbitalParameters::OrbitalParameters
	{
	private:
		SpiceChar m_lines[2][70]{};
		SpiceInt m_firstYear{1957};

		SpiceDouble m_elements[10]{};
		const std::string m_satelliteName{};
		std::unique_ptr<ConicOrbitalElements> m_conicOrbitalElements{nullptr};
		std::unique_ptr<StateVector> m_stateVector{nullptr};
		IO::SDK::Time::TimeSpan m_period;
		bool m_isDeepSpace{false};

		//J2 J3 J4 KE QO SO ER AE
		inline constexpr static SpiceDouble m_geophysics[]{1.082616e-3, -2.53881e-6, -1.65597e-6, 7.43669161e-2, 120.0, 78.0, 6378.135, 1.0};

	public:
		TLE(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfmotion, std::string lines[3]);
		virtual ~TLE() = default;

		/// <summary>
		/// Get the satellite name
		/// </summary>
		/// <returns></returns>
		std::string GetSatelliteName();

		/// <summary>
		/// Get the balistic corfficien in radians/minute**2
		/// </summary>
		/// <returns></returns>
		double GetBalisticCoefficient();

		/// <summary>
		/// Second derivative of mean motion in radians/minute**3
		/// </summary>
		/// <returns></returns>
		double GetSecondDerivativeOfMeanMotion();

		/// <summary>
		/// Return radiation pressure coefficient (or BSTAR)
		/// </summary>
		/// <returns></returns>
		double GetDragTerm();

		/// <summary>
		/// Get orbital period
		/// </summary>
		/// <returns></returns>
		virtual IO::SDK::Time::TimeSpan GetPeriod() const override;

		/// <summary>
		/// Get specific angular momentum
		/// </summary>
		/// <returns></returns>
		virtual IO::SDK::Math::Vector3D GetSpecificAngularMomentum() const override;

		/// <summary>
		/// Get State Vector at epoch
		/// </summary>
		/// <param name="epoch"></param>
		/// <returns></returns>
		virtual IO::SDK::OrbitalParameters::StateVector GetStateVector(const IO::SDK::Time::TDB &epoch) const override;

		/// <summary>
		/// Get orbital eccentricity
		/// </summary>
		/// <returns></returns>
		virtual double GetEccentricity() const override;

		/// <summary>
		/// Get orbital semi major axis
		/// </summary>
		/// <returns></returns>
		virtual double GetSemiMajorAxis() const override;

		/// <summary>
		/// Get orbital inclination
		/// </summary>
		/// <returns></returns>
		virtual double GetInclination() const override;

		/// <summary>
		/// Get orbital periapsis argument
		/// </summary>
		/// <returns></returns>
		virtual double GetPeriapsisArgument() const override;

		/// <summary>
		/// Get orbital right ascending node
		/// </summary>
		/// <returns></returns>
		virtual double GetRightAscendingNodeLongitude() const override;

		//Get non pure virtual function accessible
		using OrbitalParameters::GetMeanAnomaly;

		/// <summary>
		/// Get orbital mean anomaly
		/// </summary>
		/// <returns></returns>
		virtual double GetMeanAnomaly() const override;

		/// <summary>
		/// Get orbital specific energy
		/// </summary>
		/// <returns></returns>
		virtual double GetSpecificOrbitalEnergy() const override;
	};
}
#endif // !TLE_H

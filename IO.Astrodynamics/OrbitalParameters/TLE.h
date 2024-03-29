/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TLE_H
#define TLE_H

#include <ConicOrbitalElements.h>

namespace IO::Astrodynamics::OrbitalParameters
{
	class TLE final : public IO::Astrodynamics::OrbitalParameters::OrbitalParameters
	{
	private:
		SpiceChar m_lines[2][70]{};
		SpiceInt m_firstYear{1957};

		SpiceDouble m_elements[10]{};
		const std::string m_satelliteName{};
		std::unique_ptr<ConicOrbitalElements> m_conicOrbitalElements{nullptr};
		std::unique_ptr<StateVector> m_stateVector{nullptr};
		IO::Astrodynamics::Time::TimeSpan m_period;

		//J2 J3 J4 KE QO SO ER AE
		inline constexpr static SpiceDouble m_geophysics[]{1.082616e-3, -2.53881e-6, -1.65597e-6, 7.43669161e-2, 120.0, 78.0, 6378.135, 1.0};

	public:
		TLE(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfmotion, std::string lines[3]);
		~TLE() override = default;

		/// <summary>
		/// Get the satellite name
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] std::string GetSatelliteName() const;

		/// <summary>
		/// Get the balistic corfficien in radians/minute**2
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetBalisticCoefficient() const;

		/// <summary>
		/// Second derivative of mean motion in radians/minute**3
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetSecondDerivativeOfMeanMotion() const;

		/// <summary>
		/// Return radiation pressure coefficient (or BSTAR)
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetDragTerm() const;

		/// <summary>
		/// Get orbital period
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] IO::Astrodynamics::Time::TimeSpan GetPeriod() const override;

		/// <summary>
		/// Get specific angular momentum
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] IO::Astrodynamics::Math::Vector3D GetSpecificAngularMomentum() const override;

		/// <summary>
		/// Get State Vector at epoch
		/// </summary>
		/// <param name="epoch"></param>
		/// <returns></returns>
		[[nodiscard]] IO::Astrodynamics::OrbitalParameters::StateVector ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const override;

		/// <summary>
		/// Get orbital eccentricity
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetEccentricity() const override;

		/// <summary>
		/// Get orbital semi major axis
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetSemiMajorAxis() const override;

		/// <summary>
		/// Get orbital inclination
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetInclination() const override;

		/// <summary>
		/// Get orbital periapsis argument
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetPeriapsisArgument() const override;

		/// <summary>
		/// Get orbital right ascending node
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetRightAscendingNodeLongitude() const override;

		//Get non pure virtual function accessible
		using OrbitalParameters::GetMeanAnomaly;

		/// <summary>
		/// Get orbital mean anomaly
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetMeanAnomaly() const override;

		/// <summary>
		/// Get orbital specific energy
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] double GetSpecificOrbitalEnergy() const override;
	};
}
#endif // !TLE_H

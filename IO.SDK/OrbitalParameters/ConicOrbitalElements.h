#ifndef CONIC_ORBITAL_ELEMENTS_H
#define CONIC_ORBITAL_ELEMENTS_H

#include<memory>
#include<cmath>
#include<OrbitalParameters.h>
#include<CelestialBody.h>
#include<Constants.h>
#include<SpiceUsr.h>
#include<TimeSpan.h>
#include<chrono>
#include<TDB.h>
#include<StateVector.h>
#include<Frames.h>

namespace IO::SDK::OrbitalParameters
{
	// class StateVector;
	/// <summary>
	/// Conic orbital elements
	/// </summary>
	class ConicOrbitalElements :public IO::SDK::OrbitalParameters::OrbitalParameters
	{
	private:
		double m_perifocalDistance{};
		double m_eccentricity{};
		double m_inclination{};
		double m_ascendingNodeLongitude{};
		double m_periapsisArgument{};
		double m_meanAnomaly{};
		double m_trueAnomaly{};
		IO::SDK::Time::TimeSpan m_orbitalPeriod;
		double m_semiMajorAxis{};
		

	public:
		
		ConicOrbitalElements(
			const std::shared_ptr<IO::SDK::Body::CelestialBody>& centerOfMotion,
			const double perifocalDistance,
			const double eccentricity,
			const double inclination,
			const double ascendingNodeLongitude,
			const double periapsisArgument,
			const double meanAnomaly,
			const IO::SDK::Time::TDB& epoch, 
			const IO::SDK::Frames::Frames& frame);

		/// <summary>
		/// Instanciate conical elements from spice elements
		/// </summary>
		/// <param name="centerOfMotion"></param>
		/// <param name="spiceElements"></param>
		ConicOrbitalElements(const std::shared_ptr<IO::SDK::Body::CelestialBody>& centerOfMotion, const double spiceElements[SPICE_OSCLTX_NELTS], const IO::SDK::Frames::Frames& frame);

		ConicOrbitalElements(const IO::SDK::OrbitalParameters::StateVector& stateVector);

		ConicOrbitalElements(const ConicOrbitalElements& conicOrbitalElements) = default;

		virtual ~ConicOrbitalElements() = default;

		/// <summary>
		/// Get perifocal distance at orbital parameters epoch
		/// </summary>
		/// <returns></returns>
		double GetPerifocalDistance() const;

		/// <summary>
		/// Get eccentticity
		/// </summary>
		/// <returns></returns>
		double GetEccentricity() const override;

		/// <summary>
		/// Get inclination
		/// </summary>
		/// <returns></returns>
		double GetInclination() const override;

		/// <summary>
		/// Get ascending node longitude
		/// </summary>
		/// <returns></returns>
		double GetRightAscendingNodeLongitude() const override;

		/// <summary>
		/// Get periapsis argument
		/// </summary>
		/// <returns></returns>
		double GetPeriapsisArgument() const override;

		//Get non pure virtual function accessible
		using OrbitalParameters::GetMeanAnomaly;
		/// <summary>
		/// Get mean anomaly at epoch
		/// </summary>
		/// <returns></returns>
		double GetMeanAnomaly() const override;

		//Get non pure virtual function accessible
		using OrbitalParameters::GetTrueAnomaly;
		/// <summary>
		/// Get true anomaly at epoch
		/// </summary>
		/// <returns></returns>
		double GetTrueAnomaly() const override;

		/// <summary>
		/// Get the orbital semi major axis
		/// </summary>
		/// <returns></returns>
		double GetSemiMajorAxis() const override;

		/// <summary>
		/// Get the orbital period
		/// </summary>
		/// <returns></returns>
		IO::SDK::Time::TimeSpan GetPeriod() const override;

		/// <summary>
		/// Get the state vector at epoch
		/// </summary>
		/// <param name="epoch"></param>
		/// <returns></returns>
		IO::SDK::OrbitalParameters::StateVector GetStateVector(const IO::SDK::Time::TDB& epoch) const override;

		/// <summary>
		/// Get the specific angular momentum
		/// </summary>
		/// <returns></returns>
		IO::SDK::Math::Vector3D GetSpecificAngularMomentum() const override;

		/// <summary>
		/// Get specific orbital energy
		/// </summary>
		/// <returns></returns>
		double GetSpecificOrbitalEnergy() const override;

		using IO::SDK::OrbitalParameters::OrbitalParameters::GetStateVector;
	};
}
#endif


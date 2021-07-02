#ifndef ILLUMINATION_H
#define ILLUMINATION_H

#include <Vector3D.h>
#include<TDB.h>

namespace IO::SDK::Illumination
{
	class Illumination final
	{
	private:
		
		const IO::SDK::Math::Vector3D m_observerToSurfacePoint{};
		const double m_phaseAngle{};
		const double m_incidence{};
		const double m_emission{};
		const IO::SDK::Time::TDB m_targetEpoch;

	public:
		/// <summary>
		/// Instanciate illumination
		/// </summary>
		/// <param name="observerToSurfacePoint">Observer to surface point vector</param>
		/// <param name="phaseAngle">The phase angle in radians</param>
		/// <param name="incidence">The incidence angle in radians</param>
		/// <param name="emission">The emiision angle in radians</param>
		/// <param name="targetEpoch">TDB target epoch</param>
		Illumination(const IO::SDK::Math::Vector3D& observerToSurfacePoint, const double phaseAngle, const double incidence, const double emission, const IO::SDK::Time::TDB& targetEpoch)
			:m_observerToSurfacePoint{ observerToSurfacePoint }, m_phaseAngle{ phaseAngle }, m_incidence{ incidence }, m_emission{ emission }, m_targetEpoch{ targetEpoch } {};
		Illumination(const Illumination& illumination) = default;

		/// <summary>
		/// Get the TDB target epoch
		/// </summary>
		/// <returns></returns>
		IO::SDK::Time::TDB GetEpoch() const
		{
			return m_targetEpoch;
		}

		/// <summary>
		/// Get observer to surface point vector
		/// </summary>
		/// <returns></returns>
		const IO::SDK::Math::Vector3D& GetObserverToSurfacePoint() const
		{
			return m_observerToSurfacePoint;
		}

		/// <summary>
		/// Get the phase angle in radians
		/// </summary>
		/// <returns></returns>
		double GetPhaseAngle() const
		{
			return m_phaseAngle;
		}

		/// <summary>
		/// Get the incidence angle in radians
		/// </summary>
		/// <returns></returns>
		double GetIncidence() const
		{
			return m_incidence;
		}

		/// <summary>
		/// Get the emission angle in radians
		/// </summary>
		/// <returns></returns>
		double GetEmission() const
		{
			return m_emission;
		}
	};
}
#endif

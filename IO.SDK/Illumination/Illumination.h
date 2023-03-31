/**
 * @file Illumination.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
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
		/**
		 * @brief Construct a new Illumination object
		 * 
		 * @param observerToSurfacePoint 
		 * @param phaseAngle 
		 * @param incidence 
		 * @param emission 
		 * @param targetEpoch 
		 */
		Illumination(const IO::SDK::Math::Vector3D& observerToSurfacePoint, double phaseAngle, double incidence, double emission, const IO::SDK::Time::TDB& targetEpoch)
			:m_observerToSurfacePoint{ observerToSurfacePoint }, m_phaseAngle{ phaseAngle }, m_incidence{ incidence }, m_emission{ emission }, m_targetEpoch{ targetEpoch } {};

		/**
		 * @brief Get the Epoch
		 * 
		 * @return IO::SDK::Time::TDB 
		 */
		[[nodiscard]] inline IO::SDK::Time::TDB GetEpoch() const
		{
			return m_targetEpoch;
		}

		/**
		 * @brief Get the Observer To Surface Point
		 * 
		 * @return const IO::SDK::Math::Vector3D& 
		 */
		[[nodiscard]] inline const IO::SDK::Math::Vector3D& GetObserverToSurfacePoint() const
		{
			return m_observerToSurfacePoint;
		}

		/**
		 * @brief Get the Phase Angle
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetPhaseAngle() const
		{
			return m_phaseAngle;
		}

		/**
		 * @brief Get the Incidence
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetIncidence() const
		{
			return m_incidence;
		}

		/**
		 * @brief Get the Emission
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetEmission() const
		{
			return m_emission;
		}
	};
}
#endif

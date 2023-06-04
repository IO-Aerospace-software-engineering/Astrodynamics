/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef ILLUMINATION_H
#define ILLUMINATION_H

#include <Vector3D.h>
#include<TDB.h>

#include <utility>

namespace IO::Astrodynamics::Illumination
{
	class Illumination final
	{
	private:
		
		const IO::Astrodynamics::Math::Vector3D m_observerToSurfacePoint{};
		const double m_phaseAngle{};
		const double m_incidence{};
		const double m_emission{};
		const IO::Astrodynamics::Time::TDB m_targetEpoch;

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
		Illumination(const IO::Astrodynamics::Math::Vector3D& observerToSurfacePoint, double phaseAngle, double incidence, double emission, IO::Astrodynamics::Time::TDB  targetEpoch)
			:m_observerToSurfacePoint{ observerToSurfacePoint }, m_phaseAngle{ phaseAngle }, m_incidence{ incidence }, m_emission{ emission }, m_targetEpoch{std::move( targetEpoch )} {};

		/**
		 * @brief Get the Epoch
		 * 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] inline IO::Astrodynamics::Time::TDB GetEpoch() const
		{
			return m_targetEpoch;
		}

		/**
		 * @brief Get the Observer To Surface Point
		 * 
		 * @return const IO::Astrodynamics::Math::Vector3D&
		 */
		[[nodiscard]] inline const IO::Astrodynamics::Math::Vector3D& GetObserverToSurfacePoint() const
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

/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SURFACE_INTERCEP_POINT_H
#define SURFACE_INTERCEP_POINT_H

#include<Vector3D.h>
#include<TDB.h>

namespace IO::Astrodynamics::Coordinates
{
	/**
	 * @brief Surface intercept point
	 * 
	 */
	class SurfaceInterceptPoint
	{
	private:
		const IO::Astrodynamics::Math::Vector3D m_interceptPoint;
		const IO::Astrodynamics::Math::Vector3D m_observerToInterceptPointVector;
		const IO::Astrodynamics::Time::TDB m_interceptEpoch;
		

	public:
		/**
		 * @brief Construct a new Surface Intercept Point object
		 * 
		 * @param interceptPoint 
		 * @param observerToInterceptPoint 
		 * @param interceptEpoch 
		 */
		SurfaceInterceptPoint(const IO::Astrodynamics::Math::Vector3D& interceptPoint, const IO::Astrodynamics::Math::Vector3D& observerToInterceptPoint, IO::Astrodynamics::Time::TDB  interceptEpoch)
			:m_interceptPoint{ interceptPoint }, m_observerToInterceptPointVector{ observerToInterceptPoint }, m_interceptEpoch{std::move( interceptEpoch )}
		{};
		SurfaceInterceptPoint(const SurfaceInterceptPoint& surfaceInterceptPoint) = default;

		/**
		 * @brief Get the Intercept Point
		 * 
		 * @return const IO::Astrodynamics::Math::Vector3D&
		 */
		[[nodiscard]] const IO::Astrodynamics::Math::Vector3D& GetInterceptPoint() const
		{
			return m_interceptPoint;
		}

		/**
		 * @brief Get the Observer Intercept Point Vector
		 * 
		 * @return const IO::Astrodynamics::Math::Vector3D&
		 */
		[[nodiscard]] const IO::Astrodynamics::Math::Vector3D& GetObserverInterceptPointVector() const
		{
			return m_observerToInterceptPointVector;
		}

		/**
		 * @brief Get the Intercept Epoch
		 * 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] IO::Astrodynamics::Time::TDB GetInterceptEpoch() const
		{
			return m_interceptEpoch;
		}

	};
}
#endif

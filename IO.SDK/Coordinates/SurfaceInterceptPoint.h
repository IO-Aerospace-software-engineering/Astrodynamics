/**
 * @file SurfaceInterceptPoint.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SURFACE_INTERCEP_POINT_H
#define SURFACE_INTERCEP_POINT_H

#include<Vector3D.h>
#include<TDB.h>

namespace IO::SDK::Coordinates
{
	/**
	 * @brief Surface intercept point
	 * 
	 */
	class SurfaceInterceptPoint
	{
	private:
		const IO::SDK::Math::Vector3D m_interceptPoint;
		const IO::SDK::Math::Vector3D m_observerToInterceptPointVector;
		const IO::SDK::Time::TDB m_interceptEpoch;
		

	public:
		/**
		 * @brief Construct a new Surface Intercept Point object
		 * 
		 * @param interceptPoint 
		 * @param observerToInterceptPoint 
		 * @param interceptEpoch 
		 */
		SurfaceInterceptPoint(const IO::SDK::Math::Vector3D& interceptPoint, const IO::SDK::Math::Vector3D& observerToInterceptPoint, IO::SDK::Time::TDB  interceptEpoch)
			:m_interceptPoint{ interceptPoint }, m_observerToInterceptPointVector{ observerToInterceptPoint }, m_interceptEpoch{std::move( interceptEpoch )}
		{};
		SurfaceInterceptPoint(const SurfaceInterceptPoint& surfaceInterceptPoint) = default;

		/**
		 * @brief Get the Intercept Point
		 * 
		 * @return const IO::SDK::Math::Vector3D& 
		 */
		[[nodiscard]] const IO::SDK::Math::Vector3D& GetInterceptPoint() const
		{
			return m_interceptPoint;
		}

		/**
		 * @brief Get the Observer Intercept Point Vector
		 * 
		 * @return const IO::SDK::Math::Vector3D& 
		 */
		[[nodiscard]] const IO::SDK::Math::Vector3D& GetObserverInterceptPointVector() const
		{
			return m_observerToInterceptPointVector;
		}

		/**
		 * @brief Get the Intercept Epoch
		 * 
		 * @return IO::SDK::Time::TDB 
		 */
		[[nodiscard]] IO::SDK::Time::TDB GetInterceptEpoch() const
		{
			return m_interceptEpoch;
		}

	};
}
#endif

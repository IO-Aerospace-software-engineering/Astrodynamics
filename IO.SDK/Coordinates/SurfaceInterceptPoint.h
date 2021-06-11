#ifndef SURFACE_INTERCEP_POINT_H
#define SURFACE_INTERCEP_POINT_H

#include<Vector3D.h>
#include<TDB.h>

namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Surface intercept point
	/// </summary>
	class SurfaceInterceptPoint
	{
	private:
		const IO::SDK::Math::Vector3D m_interceptPoint;
		const IO::SDK::Time::TDB m_interceptEpoch;
		const IO::SDK::Math::Vector3D m_observerToInterceptPointVector;

	public:
		/// <summary>
		/// Instanciate surface intercept point 
		/// </summary>
		/// <param name="interceptPoint">Intercept point</param>
		/// <param name="observerTointerceptPoint">Observer to intercept point vector</param>
		/// <param name="interceptEpoch">TDB intercept epoch</param>
		SurfaceInterceptPoint(const IO::SDK::Math::Vector3D& interceptPoint, const IO::SDK::Math::Vector3D& observerToInterceptPoint, const IO::SDK::Time::TDB& interceptEpoch)
			:m_interceptPoint{ interceptPoint }, m_observerToInterceptPointVector{ observerToInterceptPoint }, m_interceptEpoch{ interceptEpoch }
		{};
		SurfaceInterceptPoint(const SurfaceInterceptPoint& surfaceInterceptPoint) = default;
		~SurfaceInterceptPoint() = default;

		/// <summary>
		/// Get the intercept point
		/// </summary>
		/// <returns></returns>
		const IO::SDK::Math::Vector3D& GetInterceptPoint() const
		{
			return m_interceptPoint;
		}

		/// <summary>
		/// Get the observer Intercept point vector
		/// </summary>
		/// <returns></returns>
		const IO::SDK::Math::Vector3D& GetObserverInterceptPointVector() const
		{
			return m_observerToInterceptPointVector;
		}

		/// <summary>
		/// Get TDB intercept epoch
		/// </summary>
		/// <returns></returns>
		IO::SDK::Time::TDB GetInterceptEpoch() const
		{
			return m_interceptEpoch;
		}

	};
}
#endif

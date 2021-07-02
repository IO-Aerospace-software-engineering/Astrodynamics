/**
 * @file SurfaceCoordinates.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SURFACE_COORDINATES_H
#define SURFACE_COORDINATES_H
#include <Vector3D.h>
#include <vector>
#include <memory>

namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Surface coordinates
	/// </summary>
	class SurfaceCoordinates
	{
	private:
		std::vector<std::unique_ptr<IO::SDK::Math::Vector3D>> m_surfacePoints;
		std::vector<std::unique_ptr<IO::SDK::Math::Vector3D>> m_surfaceNormals;

	public:
		/// <summary>
		/// Instanciate surface coordinates
		/// </summary>
		/// <param name="longitudeSpan">Longitude span</param>
		/// <param name="latitudeSpan">Latitude span</param>
		SurfaceCoordinates(const size_t longitudeSpan,const size_t latitudeSpan);

		SurfaceCoordinates(const SurfaceCoordinates& surfaceCoordinates);

		SurfaceCoordinates& operator=(const SurfaceCoordinates& surfaceCoordinates);

		const std::vector<std::unique_ptr<IO::SDK::Math::Vector3D>>& GetSurfacePoints() const
		{
			return m_surfacePoints;
		}

		const std::vector<std::unique_ptr<IO::SDK::Math::Vector3D>>& GetSurfaceNormals() const
		{
			return m_surfaceNormals;
		}

		/// <summary>
		/// Add surface point
		/// </summary>
		/// <param name="point"></param>
		void AddPoint(const IO::SDK::Math::Vector3D& point);

		/// <summary>
		/// Add normal point
		/// </summary>
		/// <param name="normal"></param>
		void AddNormal(const IO::SDK::Math::Vector3D& normal);
	};
}
#endif

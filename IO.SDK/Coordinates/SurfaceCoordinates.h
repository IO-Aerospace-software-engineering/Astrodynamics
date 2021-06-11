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
		SurfaceCoordinates(size_t longitudeSpan, size_t latitudeSpan) {
			size_t n = longitudeSpan * latitudeSpan;
			m_surfacePoints.reserve(n);
			m_surfaceNormals.reserve(n);
		}

		SurfaceCoordinates(const SurfaceCoordinates& surfaceCoordinates) {
			m_surfaceNormals.reserve(surfaceCoordinates.m_surfaceNormals.size());
			m_surfacePoints.reserve(surfaceCoordinates.m_surfacePoints.size());

			for (auto& p : surfaceCoordinates.m_surfacePoints)
			{
				AddPoint(*p);
			}

			for (auto& n : surfaceCoordinates.m_surfaceNormals)
			{
				AddNormal(*n);
			}
		}

		SurfaceCoordinates& operator=(const SurfaceCoordinates& surfaceCoordinates)
		{
			if (&surfaceCoordinates == this)
			{
				return *this;
			}

			m_surfaceNormals.clear();
			m_surfacePoints.clear();

			m_surfaceNormals.reserve(surfaceCoordinates.m_surfaceNormals.size());
			m_surfacePoints.reserve(surfaceCoordinates.m_surfacePoints.size());

			for (auto& p : surfaceCoordinates.m_surfacePoints)
			{
				AddPoint(*p);
			}

			for (auto& n : surfaceCoordinates.m_surfaceNormals)
			{
				AddNormal(*n);
			}

			return *this;
		}

		~SurfaceCoordinates() 
		{
		}

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

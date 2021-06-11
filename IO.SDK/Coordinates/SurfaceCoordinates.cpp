#include "SurfaceCoordinates.h"

void IO::SDK::Coordinates::SurfaceCoordinates::AddPoint(const IO::SDK::Math::Vector3D& point)
{
	m_surfacePoints.push_back(std::make_unique<IO::SDK::Math::Vector3D>(point.GetX(), point.GetY(), point.GetZ()));
}

void IO::SDK::Coordinates::SurfaceCoordinates::AddNormal(const IO::SDK::Math::Vector3D& normal)
{
	m_surfaceNormals.push_back(std::make_unique<IO::SDK::Math::Vector3D>(normal.GetX(), normal.GetY(), normal.GetZ()));
}

/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <SurfaceCoordinates.h>

IO::Astrodynamics::Coordinates::SurfaceCoordinates::SurfaceCoordinates(const size_t longitudeSpan, const size_t latitudeSpan)
{
	size_t n = longitudeSpan * latitudeSpan;
	m_surfacePoints.reserve(n);
	m_surfaceNormals.reserve(n);
}

IO::Astrodynamics::Coordinates::SurfaceCoordinates::SurfaceCoordinates(const SurfaceCoordinates &surfaceCoordinates)
{
	m_surfaceNormals.reserve(surfaceCoordinates.m_surfaceNormals.size());
	m_surfacePoints.reserve(surfaceCoordinates.m_surfacePoints.size());

	for (auto &p : surfaceCoordinates.m_surfacePoints)
	{
		AddPoint(*p);
	}

	for (auto &n : surfaceCoordinates.m_surfaceNormals)
	{
		AddNormal(*n);
	}
}

IO::Astrodynamics::Coordinates::SurfaceCoordinates &IO::Astrodynamics::Coordinates::SurfaceCoordinates::operator=(const SurfaceCoordinates &surfaceCoordinates)
{
	if (&surfaceCoordinates == this)
	{
		return *this;
	}

	m_surfaceNormals.clear();
	m_surfacePoints.clear();

	m_surfaceNormals.reserve(surfaceCoordinates.m_surfaceNormals.size());
	m_surfacePoints.reserve(surfaceCoordinates.m_surfacePoints.size());

	for (auto &p : surfaceCoordinates.m_surfacePoints)
	{
		AddPoint(*p);
	}

	for (auto &n : surfaceCoordinates.m_surfaceNormals)
	{
		AddNormal(*n);
	}

	return *this;
}

void IO::Astrodynamics::Coordinates::SurfaceCoordinates::AddPoint(const IO::Astrodynamics::Math::Vector3D &point)
{
	m_surfacePoints.push_back(std::make_unique<IO::Astrodynamics::Math::Vector3D>(point.GetX(), point.GetY(), point.GetZ()));
}

void IO::Astrodynamics::Coordinates::SurfaceCoordinates::AddNormal(const IO::Astrodynamics::Math::Vector3D &normal)
{
	m_surfaceNormals.push_back(std::make_unique<IO::Astrodynamics::Math::Vector3D>(normal.GetX(), normal.GetY(), normal.GetZ()));
}

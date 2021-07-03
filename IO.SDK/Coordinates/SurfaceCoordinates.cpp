/**
 * @file SurfaceCoordinates.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "SurfaceCoordinates.h"

IO::SDK::Coordinates::SurfaceCoordinates::SurfaceCoordinates(const size_t longitudeSpan, const size_t latitudeSpan)
{
	size_t n = longitudeSpan * latitudeSpan;
	m_surfacePoints.reserve(n);
	m_surfaceNormals.reserve(n);
}

IO::SDK::Coordinates::SurfaceCoordinates::SurfaceCoordinates(const SurfaceCoordinates &surfaceCoordinates)
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

IO::SDK::Coordinates::SurfaceCoordinates &IO::SDK::Coordinates::SurfaceCoordinates::operator=(const SurfaceCoordinates &surfaceCoordinates)
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

void IO::SDK::Coordinates::SurfaceCoordinates::AddPoint(const IO::SDK::Math::Vector3D &point)
{
	m_surfacePoints.push_back(std::make_unique<IO::SDK::Math::Vector3D>(point.GetX(), point.GetY(), point.GetZ()));
}

void IO::SDK::Coordinates::SurfaceCoordinates::AddNormal(const IO::SDK::Math::Vector3D &normal)
{
	m_surfaceNormals.push_back(std::make_unique<IO::SDK::Math::Vector3D>(normal.GetX(), normal.GetY(), normal.GetZ()));
}

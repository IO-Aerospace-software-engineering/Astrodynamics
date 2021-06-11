#include<gtest/gtest.h>
#include<StateVector.h>
#include<SurfaceCoordinates.h>
#include<Vector3D.h>
#include<memory>

TEST(SurfaceCoordinates, Initialization)
{
	IO::SDK::Coordinates::SurfaceCoordinates sf(5, 2);
	ASSERT_EQ(10, sf.GetSurfaceNormals().capacity());
	ASSERT_EQ(10, sf.GetSurfacePoints().capacity());
}

TEST(SurfaceCoordinates, Add)
{
	IO::SDK::Coordinates::SurfaceCoordinates sf(5, 2);
	IO::SDK::Math::Vector3D point(1.0, 2.0, 3.0);

	sf.AddPoint(point);

	ASSERT_EQ(1.0, sf.GetSurfacePoints()[0]->GetX());
	ASSERT_EQ(2.0, sf.GetSurfacePoints()[0]->GetY());
	ASSERT_EQ(3.0, sf.GetSurfacePoints()[0]->GetZ());

	IO::SDK::Math::Vector3D normal(10.0, 20.0, 30.0);

	sf.AddNormal(normal);

	ASSERT_EQ(10.0, sf.GetSurfaceNormals()[0]->GetX());
	ASSERT_EQ(20.0, sf.GetSurfaceNormals()[0]->GetY());
	ASSERT_EQ(30.0, sf.GetSurfaceNormals()[0]->GetZ());
}

TEST(SurfaceCoordinates, Copy)
{
	IO::SDK::Coordinates::SurfaceCoordinates sfFilledCopy(0,0);

	{
		IO::SDK::Coordinates::SurfaceCoordinates sf(5, 2);
		IO::SDK::Coordinates::SurfaceCoordinates sfCopy(sf);

		IO::SDK::Math::Vector3D point(1.0, 2.0, 3.0);

		sf.AddPoint(point);

		ASSERT_EQ(1.0, sf.GetSurfacePoints()[0]->GetX());
		ASSERT_EQ(2.0, sf.GetSurfacePoints()[0]->GetY());
		ASSERT_EQ(3.0, sf.GetSurfacePoints()[0]->GetZ());

		IO::SDK::Math::Vector3D normal(10.0, 20.0, 30.0);

		sf.AddNormal(normal);

		ASSERT_EQ(10.0, sf.GetSurfaceNormals()[0]->GetX());
		ASSERT_EQ(20.0, sf.GetSurfaceNormals()[0]->GetY());
		ASSERT_EQ(30.0, sf.GetSurfaceNormals()[0]->GetZ());

		ASSERT_EQ(0.0, sfCopy.GetSurfacePoints().size());
		ASSERT_EQ(0.0, sfCopy.GetSurfaceNormals().size());

		sfFilledCopy = sf;

		//Check if copies are independent from each others
		ASSERT_NE(sf.GetSurfaceNormals(), sfFilledCopy.GetSurfaceNormals());
		ASSERT_NE(sf.GetSurfaceNormals()[0], sfFilledCopy.GetSurfaceNormals()[0]);


		ASSERT_NE(sf.GetSurfaceNormals(), sfFilledCopy.GetSurfaceNormals());
		ASSERT_NE(sf.GetSurfaceNormals()[0], sfFilledCopy.GetSurfaceNormals()[0]);

		ASSERT_NE(sf.GetSurfacePoints(), sfFilledCopy.GetSurfacePoints());
		ASSERT_NE(sf.GetSurfacePoints()[0], sfFilledCopy.GetSurfacePoints()[0]);
	}//forcesf and sfcopy out of scope

	//Check if copy exists if copy source is deleted
	ASSERT_EQ(1, sfFilledCopy.GetSurfacePoints().size());
	ASSERT_EQ(1, sfFilledCopy.GetSurfaceNormals().size());
	ASSERT_EQ(1.0, sfFilledCopy.GetSurfacePoints()[0]->GetX());
	ASSERT_EQ(2.0, sfFilledCopy.GetSurfacePoints()[0]->GetY());
	ASSERT_EQ(3.0, sfFilledCopy.GetSurfacePoints()[0]->GetZ());
	ASSERT_EQ(10.0, sfFilledCopy.GetSurfaceNormals()[0]->GetX());
	ASSERT_EQ(20.0, sfFilledCopy.GetSurfaceNormals()[0]->GetY());
	ASSERT_EQ(30.0, sfFilledCopy.GetSurfaceNormals()[0]->GetZ());

}
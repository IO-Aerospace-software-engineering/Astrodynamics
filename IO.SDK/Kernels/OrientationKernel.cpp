/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <filesystem>
#include <Spacecraft.h>
#include <Builder.h>
#include <InvalidArgumentException.h>


IO::SDK::Kernels::OrientationKernel::OrientationKernel(std::string filePath, int spacecraftId, int spacecraftFrameId) : IO::SDK::Kernels::Kernel(std::move(filePath)),
                                                                                                                        m_spacecraftId{spacecraftId},
                                                                                                                        m_spacecraftFrameId{spacecraftFrameId}
{

}

IO::SDK::Kernels::OrientationKernel::~OrientationKernel() = default;

void IO::SDK::Kernels::OrientationKernel::WriteOrientations(const std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> &orientations) const
{
    if (orientations.empty())
    {
        throw IO::SDK::Exception::SDKException("Orientations array is empty");
    }

    auto frame = orientations.front().front().GetFrame();
    for (auto &&orientation: orientations)
    {
        for (auto &&orientationPart: orientation)
        {
            if (orientationPart.GetFrame() != frame)
            {
                throw IO::SDK::Exception::InvalidArgumentException(
                        "Orientations collection contains data with different frames : " + frame.GetName() + " - " + orientationPart.GetFrame().GetName() +
                        ". All orientations must have the same frame. ");
            }
        }
    }

    //Compute segment start date
    double begtime = SpacecraftClockKernel::ConvertToEncodedClock(m_spacecraftId, orientations.front().front().GetEpoch());

    //Compute segment end date
    double endtime = SpacecraftClockKernel::ConvertToEncodedClock(m_spacecraftId, orientations.back().back().GetEpoch());

    //Number of orientation data
    size_t n{};

    //numbers of intervels
    size_t nbIntervals = orientations.size();

    //Encoded clocks array
    std::vector<double> sclks;

    //Quaternion
    std::vector<std::array<double, 4>> quats;

    //Angular velocity array
    std::vector<std::array<double, 3>> av;

    //intervals start date
    std::vector<double> intervalsStarts;

    //Used to define polynomial degree
    size_t minSize{std::numeric_limits<size_t>::max()};

    for (auto &interval: orientations)
    {
        if (interval.empty())
        {
            throw IO::SDK::Exception::InvalidArgumentException("Orientation array is empty");
        }

        //Add interval start date
        intervalsStarts.push_back(SpacecraftClockKernel::ConvertToEncodedClock(m_spacecraftId, interval.begin()->GetEpoch()));

        //Number of data
        size_t intervalSize = interval.size();

        if (intervalSize < minSize)
        {
            minSize = intervalSize;
        }
        n += intervalSize;
        for (auto &orientation: interval)
        {
            //Add encoded clock
            sclks.push_back(SpacecraftClockKernel::ConvertToEncodedClock(m_spacecraftId, orientation.GetEpoch()));

            //Add orientation data
            quats.push_back({orientation.GetQuaternion().GetQ0(), orientation.GetQuaternion().GetQ1(), orientation.GetQuaternion().GetQ2(), orientation.GetQuaternion().GetQ3()});
            av.push_back({orientation.GetAngularVelocity().GetX(), orientation.GetAngularVelocity().GetY(), orientation.GetAngularVelocity().GetZ()});
        }
    }

    if (std::filesystem::exists(m_filePath))
    {
        unload_c(m_filePath.c_str());
        std::filesystem::remove(m_filePath);
    }

    //Write data
    SpiceInt handle;
    ckopn_c(m_filePath.c_str(), "CK_file", 5000, &handle);
    ckw03_c(handle, begtime, endtime, m_spacecraftFrameId, frame.ToCharArray(), true, "Seg1", n, &sclks[0], &quats[0], &av[0], nbIntervals, &intervalsStarts[0]);
    ckcls_c(handle);

    furnsh_c(m_filePath.c_str());
}

IO::SDK::OrbitalParameters::StateOrientation
IO::SDK::Kernels::OrientationKernel::ReadStateOrientation(const Body::Spacecraft::Spacecraft& spacecraft, const IO::SDK::Time::TDB &epoch, const IO::SDK::Time::TimeSpan &tolerance, const IO::SDK::Frames::Frames &frame) const
{
    //Build plateform id
    SpiceInt id = m_spacecraftId * 1000;

    //Get encoded clock
    SpiceDouble sclk = SpacecraftClockKernel::ConvertToEncodedClock(m_spacecraftId, epoch);

    //Build tolerance
    SpiceDouble tol = spacecraft.GetClock().GetTicksPerSeconds() * tolerance.GetSeconds().count();

    SpiceDouble cmat[3][3];
    SpiceDouble av[3];
    SpiceDouble clkout;
    SpiceBoolean found;

    //Get orientation and angular velocity
    ckgpav_c(id, sclk, tol, frame.ToCharArray(), cmat, av, &clkout, &found);

    if (!found)
    {
        throw IO::SDK::Exception::SDKException("No orientation found");
    }

    //Build array pointers
    double **arrayCmat;
    arrayCmat = new double *[3];
    for (int i = 0; i < 3; i++)
    {
        arrayCmat[i] = new double[3]{};
    }

    for (size_t i = 0; i < 3; i++)
    {
        for (size_t j = 0; j < 3; j++)
        {
            arrayCmat[i][j] = cmat[i][j];
        }
    }

    IO::SDK::Math::Quaternion q(IO::SDK::Math::Matrix(3, 3, arrayCmat));

    //Free memory
    for (int i = 0; i < 3; i++)
        delete[] arrayCmat[i];

    delete[] arrayCmat;

    IO::SDK::Math::Vector3D angularVelocity{av[0], av[1], av[2]};
    IO::SDK::Time::TDB tdb = spacecraft.GetClock().ConvertToTDB(clkout);

    //return state orientation
    return IO::SDK::OrbitalParameters::StateOrientation{q, angularVelocity, tdb, frame};
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> IO::SDK::Kernels::OrientationKernel::GetCoverageWindow() const
{
    SpiceDouble SPICE_CELL_CKCOV[SPICE_CELL_CTRLSZ + 2];
    SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(2, SPICE_CELL_CKCOV);

    ckcov_c(m_filePath.c_str(), m_spacecraftId * 1000, false, "SEGMENT", 0.0, "TDB", &cnfine);
    double start;
    double end;

    wnfetd_c(&cnfine, 0, &start, &end);

    return IO::SDK::Time::Window<IO::SDK::Time::TDB>{IO::SDK::Time::TDB(std::chrono::duration<double>(start)), IO::SDK::Time::TDB(std::chrono::duration<double>(end))};
}



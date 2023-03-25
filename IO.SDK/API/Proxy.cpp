#include <Proxy.h>
#include <iostream>
#include <memory>
#include <CelestialBody.h>
#include <TDB.h>
#include <chrono>
#include <InertialFrames.h>
#include <StateVectorDTO.h>

using namespace std::chrono_literals;

IO::SDK::API::DTO::ScenarioResponseDTO Propagate(IO::SDK::API::DTO::ScenarioRequestDTO s)
{
    std::cout << "fueltank 0 id :" << s.spacecrafts[0].fuelTank[0].id << std::endl;
    std::cout << "fueltank 0 capacity :" << s.spacecrafts[0].fuelTank[0].capacity << std::endl;
    std::cout << "fueltank 0 quantity :" << s.spacecrafts[0].fuelTank[0].quantity << std::endl;

    std::cout << "engine 0 id :" << s.spacecrafts[0].engines[0].id << std::endl;
    std::cout << "engine 0 fuelflow :" << s.spacecrafts[0].engines[0].fuelflow << std::endl;
    std::cout << "engine 0 isp :" << s.spacecrafts[0].engines[0].isp << std::endl;

    std::cout << "occultation 0 aberrationId :" << s.occultations[0].aberrationId << std::endl;
    std::cout << "occultation 0 backBodyId :" << s.occultations[0].backBodyId << std::endl;
    std::cout << "occultation 0 bodyId :" << s.occultations[0].bodyId << std::endl;
    std::cout << "occultation 0 frontBodyId :" << s.occultations[0].frontBodyId << std::endl;
    std::cout << "occultation 0 occultationType :" << s.occultations[0].occultationType << std::endl;
    std::cout << "occultation 0 start :" << s.occultations[0].window.start << std::endl;
    std::cout << "occultation 0 end :" << s.occultations[0].window.end << std::endl;

    std::cout << "fov 0 target id :" << s.fovs[0].targetId << std::endl;
    std::cout << "fov 0 instrument id :" << s.fovs[0].instrumentId << std::endl;
    std::cout << "fov 0 start :" << s.fovs[0].window.start << std::endl;
    std::cout << "fov 0 end :" << s.fovs[0].window.end << std::endl;

    for (size_t i = 0; i < 4; i++)
    {
        std::cout << "Celestial body :" << s.involvedCelestialBodies[i] << std::endl;
    }

    IO::SDK::API::DTO::ScenarioResponseDTO r;

    for (size_t i = 0; i < 10; i++)
    {
        r.sv[i].position.x = i * 1;
        r.sv[i].position.y = i * 2;
        r.sv[i].position.z = i * 3;
        r.sv[i].velocity.x = i * 4;
        r.sv[i].velocity.y = i * 5;
        r.sv[i].velocity.z = i * 6;
    }
    return r;
}
#include <Proxy.h>
#include <iostream>
#include <chrono>
#include <SpiceUsr.h>

using namespace std::chrono_literals;

IO::SDK::API::DTO::ScenarioDTO Propagate(IO::SDK::API::DTO::ScenarioDTO s)
{
    std::cout << "fueltank 0 id :" << s.spacecrafts[0].fuelTank[0].id << std::endl;
    std::cout << "fueltank 0 capacity :" << s.spacecrafts[0].fuelTank[0].capacity << std::endl;
    std::cout << "fueltank 0 quantity :" << s.spacecrafts[0].fuelTank[0].quantity << std::endl;

    std::cout << "engine 0 id :" << s.spacecrafts[0].engines[0].id << std::endl;
    std::cout << "engine 0 fuelflow :" << s.spacecrafts[0].engines[0].fuelflow << std::endl;
    std::cout << "engine 0 isp :" << s.spacecrafts[0].engines[0].isp << std::endl;

    std::cout << "occultation 0 aberrationId :" << s.occultations[0].aberrationId << std::endl;
    std::cout << "occultation 0 backBodyId :" << s.occultations[0].backBodyId << std::endl;
    std::cout << "occultation 0 bodyId :" << s.occultations[0].observerId << std::endl;
    std::cout << "occultation 0 frontBodyId :" << s.occultations[0].frontId << std::endl;
    std::cout << "occultation 0 occultationType :" << s.occultations[0].type << std::endl;

    std::cout << "fov 0 target id :" << s.inFieldOfViews[0].targetId << std::endl;
    std::cout << "fov 0 instrument id :" << s.inFieldOfViews[0].instrumentId << std::endl;

    IO::SDK::API::DTO::ScenarioDTO r;

    for (size_t i = 0; i < 10; i++)
    {
        r.spacecrafts[0].stateVectors[i].position.x = i * 1;
        r.spacecrafts[0].stateVectors[i].position.y = i * 2;
        r.spacecrafts[0].stateVectors[i].position.z = i * 3;
        r.spacecrafts[0].stateVectors[i].velocity.x = i * 4;
        r.spacecrafts[0].stateVectors[i].velocity.y = i * 5;
        r.spacecrafts[0].stateVectors[i].velocity.z = i * 6;
    }
    return r;
}

const char *GetSpiceVersionProxy()
{
    const char *version;
    version = tkvrsn_c("TOOLKIT");
    return version;
}
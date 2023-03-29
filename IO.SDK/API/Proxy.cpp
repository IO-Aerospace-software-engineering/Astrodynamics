#include <Proxy.h>
#include <iostream>
#include <SpiceUsr.h>
#include <cstring>
#include <Scenario.h>
#include "Converters.cpp"


IO::SDK::API::DTO::ScenarioDTO Execute(IO::SDK::API::DTO::ScenarioDTO s)
{
    IO::SDK::Scenario scenario(s.Name, ToWindow(s.Window));

    IO::SDK::API::DTO::ScenarioDTO res;
    res.Name = strdup(s.Name);
    res.Window = s.Window;
//    res.name = "s.name";
//    std::cout << s.Name << std::endl;
//    std::cout << s.Window.start << std::endl;
//    std::cout << s.Window.end << std::endl;
//    res.window = s.window;
//    std::cout << "fueltank 0 id :" << s.spacecraft.fuelTank[0].id << std::endl;
//    std::cout << "fueltank 0 capacity :" << s.spacecraft.fuelTank[0].capacity << std::endl;
//    std::cout << "fueltank 0 quantity :" << s.spacecraft.fuelTank[0].quantity << std::endl;
//
//    std::cout << "engine 0 id :" << s.spacecraft.engines[0].id << std::endl;
//    std::cout << "engine 0 fuelflow :" << s.spacecraft.engines[0].fuelflow << std::endl;
//    std::cout << "engine 0 isp :" << s.spacecraft.engines[0].isp << std::endl;
//
//    std::cout << "occultation 0 aberrationId :" << s.occultations[0].aberrationId << std::endl;
//    std::cout << "occultation 0 backBodyId :" << s.occultations[0].backBodyId << std::endl;
//    std::cout << "occultation 0 bodyId :" << s.occultations[0].observerId << std::endl;
//    std::cout << "occultation 0 frontBodyId :" << s.occultations[0].frontId << std::endl;
//    std::cout << "occultation 0 occultationType :" << s.occultations[0].type << std::endl;
//
//    std::cout << "fov 0 target id :" << s.spacecraft.instruments[0].inFieldOfViews[0].targetId << std::endl;
//    std::cout << "fov 0 instrument id :" << s.spacecraft.instruments[0].id << std::endl;


//    for (size_t i = 0; i < 10; i++)
//    {
//        s.spacecraft.stateVectors[i].position.x = i * 1;
//        s.spacecraft.stateVectors[i].position.y = i * 2;
//        s.spacecraft.stateVectors[i].position.z = i * 3;
//        s.spacecraft.stateVectors[i].velocity.x = i * 4;
//        s.spacecraft.stateVectors[i].velocity.y = i * 5;
//        s.spacecraft.stateVectors[i].velocity.z = i * 6;
//    }
//    return res;

    return res;
}

const char *GetSpiceVersionProxy()
{
    const char *version;
    version = tkvrsn_c("TOOLKIT");
    return version;
}
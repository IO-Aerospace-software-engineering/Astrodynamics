/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by famille on 02/06/23.
//

#ifndef IOSDK_RADECDTO_H
#define IOSDK_RADECDTO_H

namespace IO::Astrodynamics::API::DTO {

    struct RaDecDTO {
        double rightAscension = 0.0, declination = 0.0, range = 0.0;
    };

} // DTO

#endif //IOSDK_RADECDTO_H

//
// Created by s.guillet on 15/06/2023.
//

#ifndef IO_HORIZONTALDTO_H
#define IO_HORIZONTALDTO_H
namespace IO::Astrodynamics::API::DTO {

    struct HorizontalDTO {
        double azimuth = 0.0, elevation = 0.0, range = 0.0;
    };

} // DTO
#endif //IO_HORIZONTALDTO_H

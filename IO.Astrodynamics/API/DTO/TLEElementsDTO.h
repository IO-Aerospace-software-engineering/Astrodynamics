/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 7/4/23.
//

#ifndef IO_TLEELEMENTSDTO_H
#define IO_TLEELEMENTSDTO_H
namespace IO::Astrodynamics::API::DTO
{
    struct TLEElementsDTO
    {
        double BalisticCoefficient{};
        double SecondDerivativeOfMeanMotion{};
        double DragTerm{};
        double Epoch{};
        double A{};
        double E{};
        double I{};
        double W{};
        double O{};
        double M{};
    };
}
#endif //IO_TLEELEMENTSDTO_H

/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef BUILDER_H
#define BUILDER_H
#include <SpiceUsr.h>

namespace IO::SDK::Spice
{
    class Builder
    {
    private:
        /* data */
    public:
        /**
         * @brief Create a Spice Double Cell
         * 
         * @param size 
         * @param SPICE_CELL 
         * @return SpiceCell 
         */
        static SpiceCell CreateDoubleCell(int size, double *SPICE_CELL);
    };

} // namespace IO::SDK::Spice

#endif

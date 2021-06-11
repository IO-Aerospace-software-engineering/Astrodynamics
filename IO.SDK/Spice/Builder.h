/**
 * @file Builder.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-03
 * 
 * @copyright Copyright (c) 2021
 * 
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
        static SpiceCell CreateDoubleCell(const SpiceInt size, SpiceDouble *SPICE_CELL);
    };

} // namespace IO::SDK::Spice

#endif

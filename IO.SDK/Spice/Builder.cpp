/**
 * @file Builder.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Builder.h>
#include <SpiceUsr.h>

SpiceCell IO::SDK::Spice::Builder::CreateDoubleCell(const int size,double* SPICE_CELL)
{
    return {SPICE_DP, 0, size, 0, SPICETRUE, SPICEFALSE, SPICEFALSE, (void *)SPICE_CELL, (void *)&(SPICE_CELL[SPICE_CELL_CTRLSZ])};
}
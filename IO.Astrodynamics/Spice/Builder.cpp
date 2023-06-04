/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Builder.h>

SpiceCell IO::Astrodynamics::Spice::Builder::CreateDoubleCell(const int size,double* SPICE_CELL)
{
    return {SPICE_DP, 0, size, 0, SPICETRUE, SPICEFALSE, SPICEFALSE, (void *)SPICE_CELL, (void *)&(SPICE_CELL[SPICE_CELL_CTRLSZ])};
}
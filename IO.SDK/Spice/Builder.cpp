#include <Builder.h>

SpiceCell IO::SDK::Spice::Builder::CreateDoubleCell(const SpiceInt size,SpiceDouble* SPICE_CELL)
{
    return {SPICE_DP, 0, size, 0, SPICETRUE, SPICEFALSE, SPICEFALSE, (void *)SPICE_CELL, (void *)&(SPICE_CELL[SPICE_CELL_CTRLSZ])};
}
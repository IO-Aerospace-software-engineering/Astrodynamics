/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SOLAR_SYSTEM_KERNELS_H
#define SOLAR_SYSTEM_KERNELS_H

#include <string>

namespace IO::Astrodynamics::Kernels
{
	/**
	 * @brief Loads and unloads kernels. Every load and unload of the library first unloads the SPKs, CKs, binary PCKs and
	 * EKs whose file, named by an absolute path, was deleted since their load (#668, second round): KEEPER's unload of a
	 * binary kernel opens the other loaded kernels of its kind again, and a deleted one would stay open in SPICE for good.
	 */
	class KernelsLoader final
	{
    public :
        static void Load(const std::string& path);
        static void Unload(const std::string& path);

        /**
         * @brief Unloads one file by the name SPICE holds it under (unload_c), after the deleted binary kernels. The kernel
         * objects (Kernel, whose destructor unloads its file) and the writers of SPKs and CKs unload through it
         */
        static void UnloadFile(const std::string& path);
	};
}

#endif // !SOLAR_SYSTEM_KERNELS_H

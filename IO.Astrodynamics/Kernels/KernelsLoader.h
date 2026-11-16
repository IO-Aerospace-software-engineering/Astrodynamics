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
	 * The existence of the file is tested only for a name SPICE holds as an absolute path. A name SPICE holds relative to the
	 * working directory, whether a meta-kernel's KERNELS_TO_LOAD or a direct C++ caller of LoadKernelsProxy gave it, is left
	 * to SPICE whether its file was deleted or not, since after a change of working directory the name no longer tells.
	 * KEEPER's own reopening of such a kernel, after the unload of another kernel of its kind, then fails with
	 * SPICE(FILENOTFOUND) once the working directory changed or the file was deleted (the .NET layer always passes full
	 * paths; fourth round of #668).
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

        /**
         * @brief Unloads every load of one file by the name SPICE holds it under, after the deleted binary kernels: unload_c
         * until KEEPER no longer finds the name (kinfo_c), stopping at a failure, which is left signalled. furnsh_c of a file
         * already loaded adds an entry of the same handle to KEEPER, and each unload_c removes one, so that a single unload
         * of a file loaded twice (by the caller and by a kernel object, or twice by the caller) left it loaded: the writers of
         * SPKs and CKs, which replace a file, unload it this way (#706: before, a load left in KEEPER held the replaced file,
         * deleted but open, and SPICE went on reading its data)
         */
        static void UnloadEveryLoad(const std::string& path);
	};
}

#endif // !SOLAR_SYSTEM_KERNELS_H

/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <KernelsLoader.h>
#include<cstring>
#include<filesystem>
#include<vector>
#include<SpiceUsr.h>
#include <SDKException.h>

namespace
{
    // The lengths of the strings kdata_c writes: a file name of FURNSH holds 255 characters
    constexpr SpiceInt FileLength{256};
    constexpr SpiceInt TypeLength{32};

    // Whether the file of a loaded kernel no longer exists, its name taken as SPICE holds it. A name the file system cannot
    // test counts as existing, so that nothing is unloaded on a doubt, and so does a relative name, whether a meta-kernel's
    // KERNELS_TO_LOAD or a direct C++ caller of LoadKernelsProxy gave it: it is relative to the working directory of its
    // load, which may have changed since, so that the name no longer tells whether the file SPICE holds open was deleted
    // (review of the second round of #668). Such a kernel is left to SPICE, deleted or not: KEEPER's own reopening of it,
    // after the unload of another kernel of its kind, fails with SPICE(FILENOTFOUND) once the working directory changed or
    // the file was deleted (fourth round of #668). The .NET layer loads by full path
    bool WasDeleted(const char *file)
    {
        std::error_code error;
        if (!std::filesystem::path(file).is_absolute())
        {
            return false;
        }
        bool exists = std::filesystem::exists(file, error);
        return !error && !exists;
    }

    // Closes a binary kernel by its handle through its subsystem, true for the kinds the library closes this way
    bool CloseByHandle(const char *type, SpiceInt handle)
    {
        if (std::strcmp(type, "SPK") == 0) spkuef_c(handle);
        else if (std::strcmp(type, "CK") == 0) ckupf_c(handle);
        else if (std::strcmp(type, "PCK") == 0) pckuof_c(handle);
        else if (std::strcmp(type, "EK") == 0) ekuef_c(handle);
        else return false;
        return true;
    }

    // Whether the failure SPICE signals is a kernel file that does not exist: the reopening of another deleted kernel
    bool FailedOnAMissingFile()
    {
        if (!failed_c()) return false;
        SpiceChar message[42]{};
        getmsg_c("SHORT", sizeof(message), message);
        return std::strcmp(message, "SPICE(FILENOTFOUND)") == 0;
    }

    /**
     * Unloads every SPK, CK, binary PCK and EK whose file was deleted since its load (#668, second round). KEEPER's UNLOAD
     * opens again the other loaded kernels of the kind it unloads (unload_c --> UNLOAD --> SPKLEF, CKLPF, PCKLOF, EKLEF):
     * for a deleted file, DAFOPR fails before the subsystem touches its table, so the file stays open in the DAF and
     * subsystem tables while KEEPER stores the handle 0 for it. From then on neither its unload nor kclear_c closes it
     * (measured for SPKs, CKs and PCKs; the EK not measured). Once CSPICE's handle manager has released its logical unit
     * (more than 23 in use), the searches that reach it fail to reconnect it (SPICE(FILEOPENFAIL)): for a PCK every IAU
     * frame, for a CK the reads of epochs not already read; for an SPK the reads of the Moon, L1, L2 and Mars after 25 more
     * DAFs did not fail in the measured layout, the orphan handle staying open (CSPICE N0067). Only native code holds the
     * handle, so the kernels are first closed by the handle KEEPER still holds, then dropped from KEEPER by unload_c, whose
     * reopening of the remaining kernels of the kind fails, as expected, on the next deleted file, already closed; the last
     * unload_c reopens every remaining kernel of the kind in load order, which restores their precedence. A DSK is left to
     * KEEPER: its subsystem has no public routine that closes it by handle. A kernel SPICE holds under a relative name is left
     * to KEEPER too (WasDeleted). Every unload_c of a binary kernel by the library runs after it (KernelsLoader::UnloadFile):
     * the native writers unload the file they replace and, through their kernel object, the file they wrote, each an SPK or
     * CK unload that opens the others again (review of the second round of #668). The unloads of the text kernels the
     * library writes (frames, instruments, clocks) open no binary kernel again.
     */
    void UnloadDeletedBinaryKernels()
    {
        SpiceInt count{};
        ktotal_c("ALL", &count);
        std::vector<std::string> deleted;
        for (SpiceInt i = 0; i < count && !failed_c(); ++i)
        {
            SpiceChar file[FileLength]{};
            SpiceChar type[TypeLength]{};
            SpiceChar source[FileLength]{};
            SpiceInt handle{};
            SpiceBoolean found{};
            kdata_c(i, "ALL", FileLength, TypeLength, FileLength, file, type, source, &handle, &found);
            if (!found || handle == 0 || !WasDeleted(file))
            {
                continue;
            }

            if (CloseByHandle(type, handle))
            {
                deleted.emplace_back(file);
            }
        }

        for (const auto &file: deleted)
        {
            if (failed_c()) return;
            unload_c(file.c_str());
            if (FailedOnAMissingFile())
            {
                reset_c();
            }
        }
    }

    void LoadPath(const std::string &path)
    {
        if (std::filesystem::is_regular_file(path))
        {
            unload_c(path.c_str());
            furnsh_c(path.c_str());
        } else if (std::filesystem::is_directory(path))
        {
            for (const auto &entry: std::filesystem::directory_iterator(path))
            {
                LoadPath(entry.path().string());
            }
        }
    }

    // A directory is walked as it is now; any other path is handed to SPICE by its name, whether it still exists or not:
    // unload_c unloads a file by the name it was loaded under, and does nothing for a name it does not hold
    void UnloadPath(const std::string &path)
    {
        std::error_code error;
        if (std::filesystem::is_directory(path, error))
        {
            for (const auto &entry: std::filesystem::directory_iterator(path))
            {
                UnloadPath(entry.path().string());
            }
        } else
        {
            unload_c(path.c_str());
        }
    }
}

void IO::Astrodynamics::Kernels::KernelsLoader::Load(const std::string &path)
{
    UnloadDeletedBinaryKernels();
    LoadPath(path);
}

void IO::Astrodynamics::Kernels::KernelsLoader::Unload(const std::string &path)
{
    UnloadDeletedBinaryKernels();
    UnloadPath(path);
}

void IO::Astrodynamics::Kernels::KernelsLoader::UnloadFile(const std::string &path)
{
    UnloadDeletedBinaryKernels();
    unload_c(path.c_str());
}

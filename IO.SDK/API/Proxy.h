
#include <ScenarioDTO.h>

#ifdef __cplusplus
extern "C" {
#endif
#ifdef _WIN32
#  ifdef MODULE_API_EXPORTS
#    define MODULE_API __declspec(dllexport)
#  else
#    define MODULE_API __declspec(dllimport)
#  endif
#else
#  define MODULE_API
#endif
/**
 * Get the spice version
 * @return version
 */
    MODULE_API const char *GetSpiceVersionProxy();

    /**
     * Write ephemeris into binary file (spk)
     * @param filePath
     * @param objectId
     * @param sv
     * @param size
     * @return
     */
MODULE_API bool WriteEphemerisProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateVectorDTO sv[100000], int size);

/**
 * Write orientation data into binary file (ck)
 * @param filePath
 * @param objectId
 * @param so
 * @param size
 * @return
 */
MODULE_API bool WriteOrientationProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateOrientationDTO so[100000], int size);

/**
 * Propagate a scenario
 * @param scenarioDto
 */
MODULE_API void PropagateProxy(IO::SDK::API::DTO::ScenarioDTO& scenarioDto);

/**
 * Evaluate launch windows
 * @param launchDto
 */
MODULE_API void LaunchProxy(IO::SDK::API::DTO::LaunchDTO& launchDto);

/**
 * Load generic kernels
 * @param directoryPath
 */
MODULE_API void LoadGenericKernelsProxy(const char * directoryPath);
#ifdef __cplusplus
}
#endif


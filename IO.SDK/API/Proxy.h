
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
//	MODULE_API IO::SDK::API::DTO::ScenarioDTO Execute(IO::SDK::API::DTO::ScenarioDTO s);
MODULE_API const char *GetSpiceVersionProxy();
MODULE_API bool WriteEphemerisProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateVectorDTO sv[100000], int size);
MODULE_API bool WriteOrientationProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateOrientationDTO so[100000], int size);
MODULE_API IO::SDK::API::DTO::ScenarioDTO PropagateProxy(IO::SDK::API::DTO::ScenarioDTO scenarioDto);
#ifdef __cplusplus
}
#endif


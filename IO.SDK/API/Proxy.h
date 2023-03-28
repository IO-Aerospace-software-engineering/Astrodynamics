
#include <SpacecraftDTO.h>
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
	MODULE_API IO::SDK::API::DTO::ScenarioDTO Propagate(IO::SDK::API::DTO::ScenarioDTO s);
#ifdef __cplusplus
}
#endif
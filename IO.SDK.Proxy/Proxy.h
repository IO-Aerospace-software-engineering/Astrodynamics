
#include <Models/SpacecraftDTO.h>
#include <Models/ScenarioResponseDTO.h>
#include <Models/ScenarioRequestDTO.h>

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
	MODULE_API IO::SDK::Proxy::Models::ScenarioResponseDTO Propagate(IO::SDK::Proxy::Models::ScenarioRequestDTO s);
#ifdef __cplusplus
}
#endif
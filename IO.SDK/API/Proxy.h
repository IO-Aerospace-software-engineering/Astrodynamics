
#include <SpacecraftDTO.h>
#include <ScenarioResponseDTO.h>
#include <ScenarioRequestDTO.h>

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
	MODULE_API IO::SDK::API::DTO::ScenarioResponseDTO Propagate(IO::SDK::API::DTO::ScenarioRequestDTO s);
#ifdef __cplusplus
}
#endif
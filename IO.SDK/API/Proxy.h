
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
	MODULE_API IO::SDK::API::DTO::ScenarioDTO Execute(IO::SDK::API::DTO::ScenarioDTO s);
	MODULE_API const char *GetSpiceVersionProxy();
#ifdef __cplusplus
}
#endif


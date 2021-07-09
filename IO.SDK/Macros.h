#if defined(WIN32) || defined(_WIN32)
#define UNUSED(x)
#else
#define UNUSED(x) x __attribute__((unused))
#endif
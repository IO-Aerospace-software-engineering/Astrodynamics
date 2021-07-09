/**
 * @file IlluminationAngle.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-08
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef ILLUMINATION_ANGLE_H
#define ILLUMINATION_ANGLE_H
#include <string>

namespace IO::SDK
{
    class IlluminationAngle
    {
    private:
        const std::string m_name;

        static IlluminationAngle _Phase;
        static IlluminationAngle _Incidence;
        static IlluminationAngle _Emission;

    public:
        IlluminationAngle(const std::string &name);

        const char *ToCharArray() const;

        static IlluminationAngle& Phase();
        static IlluminationAngle& Incidence();
        static IlluminationAngle& Emission();
    };

} // namespace IO::SDK

#endif
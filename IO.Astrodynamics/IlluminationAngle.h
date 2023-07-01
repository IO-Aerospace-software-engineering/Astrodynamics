/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef ILLUMINATION_ANGLE_H
#define ILLUMINATION_ANGLE_H
#include <string>

namespace IO::Astrodynamics
{
    class IlluminationAngle
    {
    private:
        const std::string m_name;

        static IlluminationAngle mPhase;
        static IlluminationAngle mIncidence;
        static IlluminationAngle mEmission;

    public:
        explicit IlluminationAngle(std::string name);
        ~IlluminationAngle() = default;

        IlluminationAngle &operator=(const IlluminationAngle &other)
        {
            if (this == &other)
                return *this;

            const_cast<std::string &>(m_name) = other.m_name;
            return *this;
        }

        [[nodiscard]] const char *ToCharArray() const;

        static IlluminationAngle& Phase();
        static IlluminationAngle& Incidence();
        static IlluminationAngle& Emission();
        static IlluminationAngle ToIlluminationAngleType(const std::string &illuminationAngleType) ;
    };

} // namespace IO::Astrodynamics

#endif
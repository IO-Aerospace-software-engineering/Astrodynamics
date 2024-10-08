/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef OCCULTATION_TYPE_H
#define OCCULTATION_TYPE_H

#include<string>

namespace IO::Astrodynamics
{
    class OccultationType
    {
    private:
        const std::string m_name;
        static OccultationType mFull;
        static OccultationType mAnnular;
        static OccultationType mPartial;
        static OccultationType mAny;
    public:
        /**
         * @brief Construct a new Occultation Type object
         * 
         * @param name 
         */
        explicit OccultationType(std::string name);

        ~OccultationType() = default;

        OccultationType &operator=(const OccultationType &other)
        {
            if (this == &other)
                return *this;

            const_cast<std::string &>(m_name) = other.m_name;
            return *this;
        }

        /**
         * @brief Get occultation type char array
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

        static IO::Astrodynamics::OccultationType &Full();

        static IO::Astrodynamics::OccultationType &Annular();

        static IO::Astrodynamics::OccultationType &Partial();

        static IO::Astrodynamics::OccultationType &Any();

        static IO::Astrodynamics::OccultationType ToOccultationType(const std::string &occultationType) ;
    };


} // namespace IO::Astrodynamics

#endif
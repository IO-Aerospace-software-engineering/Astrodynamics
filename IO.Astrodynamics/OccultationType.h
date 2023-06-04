/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef OCCULTATION_TYPE_H
#define OCCULTATION_TYPE_H

#include<string>

namespace IO::SDK
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

        static IO::SDK::OccultationType &Full();

        static IO::SDK::OccultationType &Annular();

        static IO::SDK::OccultationType &Partial();

        static IO::SDK::OccultationType &Any();

        static IO::SDK::OccultationType ToOccultationType(const std::string &occultationType) ;
    };


} // namespace IO::SDK

#endif
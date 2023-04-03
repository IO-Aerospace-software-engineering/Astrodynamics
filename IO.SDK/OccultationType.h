/**
 * @file OccultationType.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-08
 * 
 * @copyright Copyright (c) 2021
 * 
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

        /**
         * @brief Get occultation type char array
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

        static IO::SDK::OccultationType& Full();
        static IO::SDK::OccultationType& Annular();
        static IO::SDK::OccultationType& Partial();
        static IO::SDK::OccultationType& Any();
    };
    

    
} // namespace IO::SDK

#endif
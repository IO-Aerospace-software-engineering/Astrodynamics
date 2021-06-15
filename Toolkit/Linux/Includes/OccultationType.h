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
    public:
        OccultationType(const std::string &name);

        static OccultationType Full;
        static OccultationType Annular;
        static OccultationType Partial;
        static OccultationType Any;

        const char *ToCharArray() const;
    };
    

    
} // namespace IO::SDK

#endif
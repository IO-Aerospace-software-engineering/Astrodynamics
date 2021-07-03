/**
 * @file Constraints.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-07
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef CONSTRAINT_H
#define CONSTRAINT_H

#include <string>

namespace IO::SDK
{
    class Constraint
    {
    private:
        const std::string m_name;

    public:
        /**
         * @brief Construct a new Constraint object
         * 
         * @param name 
         */
        Constraint(const std::string &name);

        static Constraint GreaterThan;
        static Constraint LowerThan;
        static Constraint Equal;
        static Constraint AbsMin;
        static Constraint AbsMax;
        static Constraint LocalMin;
        static Constraint LocalMax;

        /**
         * @brief Get char array constraint name
         * 
         * @return const char* 
         */
        const char *ToCharArray() const;
    };

} // namespace IO::SDK

#endif
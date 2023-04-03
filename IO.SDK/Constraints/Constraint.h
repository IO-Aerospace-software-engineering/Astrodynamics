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

namespace IO::SDK::Constraints
{
    class Constraint
    {
    private:
        const std::string m_name;
        static Constraint mGreaterThan;
        static Constraint mLowerThan;

    public:
        /**
         * @brief Construct a new Constraint object
         * 
         * @param name 
         */
        explicit Constraint(std::string name);

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
        [[nodiscard]] const char *ToCharArray() const;

        static IO::SDK::Constraints::Constraint& GreaterThan();
        static IO::SDK::Constraints::Constraint& LowerThan();
    };

} // namespace IO::SDK

#endif
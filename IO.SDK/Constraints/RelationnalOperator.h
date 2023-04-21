/**
 * @file Constraints.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
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
    class RelationnalOperator
    {
    private:
        const std::string m_name;
        static RelationnalOperator mGreaterThan;
        static RelationnalOperator mLowerThan;
        static RelationnalOperator mEqual;
        static RelationnalOperator mAbsMin;
        static RelationnalOperator mAbsMax;
        static RelationnalOperator mLocalMin;
        static RelationnalOperator mLocalMax;

    public:
        /**
         * @brief Construct a new RelationnalOperator object
         * 
         * @param name 
         */
        explicit RelationnalOperator(std::string name);



        /**
         * @brief Get char array constraint name
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

        static IO::SDK::Constraints::RelationnalOperator& GreaterThan();
        static IO::SDK::Constraints::RelationnalOperator& LowerThan();
        static IO::SDK::Constraints::RelationnalOperator& Equal();
        static IO::SDK::Constraints::RelationnalOperator& AbsMin();
        static IO::SDK::Constraints::RelationnalOperator& AbsMax();
        static IO::SDK::Constraints::RelationnalOperator& LocalMin();
        static IO::SDK::Constraints::RelationnalOperator& LocalMax();
   };

} // namespace IO::SDK

#endif
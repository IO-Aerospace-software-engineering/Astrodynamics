/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef CONSTRAINT_H
#define CONSTRAINT_H

#include <string>

namespace IO::SDK::Constraints
{
    class RelationalOperator
    {
    private:
        const std::string m_name;
        static RelationalOperator mGreaterThan;
        static RelationalOperator mLowerThan;
        static RelationalOperator mEqual;
        static RelationalOperator mAbsMin;
        static RelationalOperator mAbsMax;
        static RelationalOperator mLocalMin;
        static RelationalOperator mLocalMax;

    public:
        /**
         * @brief Construct a new RelationalOperator object
         * 
         * @param name 
         */
        explicit RelationalOperator(std::string name);

        RelationalOperator(const RelationalOperator &relationalOperator) = default;
        IO::SDK::Constraints::RelationalOperator &operator=(const RelationalOperator &other);

        /**
         * @brief Get char array constraint name
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

        static IO::SDK::Constraints::RelationalOperator &GreaterThan();

        static IO::SDK::Constraints::RelationalOperator &LowerThan();

        static IO::SDK::Constraints::RelationalOperator &Equal();

        static IO::SDK::Constraints::RelationalOperator &AbsMin();

        static IO::SDK::Constraints::RelationalOperator &AbsMax();

        static IO::SDK::Constraints::RelationalOperator &LocalMin();

        static IO::SDK::Constraints::RelationalOperator &LocalMax();

        static IO::SDK::Constraints::RelationalOperator ToRelationalOperator(const std::string &relationalOperator);
    };

} // namespace IO::SDK

#endif
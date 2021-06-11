#include <Constraint.h>


IO::SDK::Constraint IO::SDK::Constraint::GreaterThan(std::string(">"));
IO::SDK::Constraint IO::SDK::Constraint::LowerThan(std::string("<"));
IO::SDK::Constraint IO::SDK::Constraint::Equal(std::string("="));
IO::SDK::Constraint IO::SDK::Constraint::AbsMin(std::string("ABSMIN"));
IO::SDK::Constraint IO::SDK::Constraint::AbsMax(std::string("ABSMAX"));
IO::SDK::Constraint IO::SDK::Constraint::LocalMin(std::string("LOCMIN"));
IO::SDK::Constraint IO::SDK::Constraint::LocalMax(std::string("LOCMAX"));

IO::SDK::Constraint::Constraint(const std::string& name) : m_name{name}
{
}

const char *IO::SDK::Constraint::ToCharArray() const
{
    return m_name.c_str();
}
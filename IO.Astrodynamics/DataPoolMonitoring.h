/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef KERNEL_POOL_MONITORING_H
#define KERNEL_POOL_MONITORING_H

#include<vector>
#include<string>

namespace IO::SDK
{
	/**
	 * @brief Class used to read raw data pool
	 * 
	 */
	class DataPoolMonitoring
	{
	public:
		static DataPoolMonitoring& Instance();

		[[nodiscard]]static std::vector<std::string> GetStringProperty(const std::string& propertyName, int nbValuesExpected) ;
        [[nodiscard]]static std::vector<int> GetIntegerProperty(const std::string& propertyName, int nbValuesExpected);
        [[nodiscard]]static std::vector<double> GetDoubleProperty(const std::string& propertyName, int nbValuesExpected);
        DataPoolMonitoring(const DataPoolMonitoring&) = delete;
        DataPoolMonitoring& operator= (const DataPoolMonitoring&) = delete;

	private:



		static DataPoolMonitoring m_instance;
		DataPoolMonitoring() = default;
		~DataPoolMonitoring() = default;
	};
}

#endif // !KERNEL_POOL_MONITORING_H



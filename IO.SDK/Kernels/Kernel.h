/**
 * @file Kernel.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef KERNEL_H
#define KERNEL_H
#include <string>
#include <Window.h>
#include <TDB.h>

namespace IO::SDK::Kernels
{
	class Kernel
	{
	protected:
		const std::string m_filePath;
		std::string m_comments;
		bool m_isLoaded{false};
		bool m_fileExists{false};
		Kernel(const std::string &fileName);
		/**
		 * @brief Define the best Lagrange polynomial degree
		 * 
		 * @param dataSize - Size of data set
		 * @return int 
		 */
		static int DefinePolynomialDegree(const int dataSize, const int maximumDegree) ;

	public:
		virtual ~Kernel();

		/**
		 * @brief Get the Path
		 * 
		 * @return std::string 
		 */
		std::string GetPath() const;

		/**
		 * @brief 
		 * 
		 * @return true 
		 * @return false 
		 */
		bool IsLoaded() const;

		/**
		 * @brief Get the Coverage Window
		 * 
		 * @return IO::SDK::Time::Window<IO::SDK::Time::TDB> 
		 */
		virtual IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const = 0;

		/**
		 * @brief Add comment to kernel
		 * 
		 * @param comment 
		 */
		virtual void AddComment(const std::string &comment) const;

		/**
		 * @brief Read comment from kernel
		 * 
		 * @return std::string 
		 */
		virtual std::string ReadComment() const;
	};

}
#endif
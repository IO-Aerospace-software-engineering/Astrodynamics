#ifndef KERNEL_H
#define KERNEL_H
#include <string>
#include <SpiceUsr.h>
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
		int DefinePolynomialDegree(const int dataSize, const int maximumDegree) const;

	public:
		virtual ~Kernel();

		/// <summary>
		/// get Kernel file path
		/// </summary>
		/// <returns></returns>
		std::string GetPath() const;

		/// <summary>
		/// Know if Kernel is loaded
		/// </summary>
		/// <returns></returns>
		bool IsLoaded() const;

		/// <summary>
		/// Get Kernel coverage window
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		virtual IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const = 0;

		/// <summary>
		/// Add comment to kernel
		/// </summary>
		/// <param name="comment">Comment to add. It must not exceed 80 chars</param>
		virtual void AddComment(const std::string &comment) const;

		/// <summary>
		/// Read kernel comment
		/// </summary>
		/// <returns></returns>
		virtual std::string ReadComment() const;
	};

}
#endif
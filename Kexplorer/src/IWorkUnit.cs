using System;

namespace Kexplorer
{
	/// <summary>
	/// Used for Invoking things.
	/// </summary>
	public delegate void InvokeDelegate();





	/// <summary>
	/// Summary description for IWorkUnit.
	/// </summary>
	public interface IWorkUnit
	{


		/// <summary>
		/// Run a job.
		/// </summary>
		/// <returns>Can be null.  A new job that needs to be added to work to be done.</returns>
		IWorkUnit DoJob();


		/// <summary>
		/// Stop whatever is going on.
		/// </summary>
		void Abort();


		/// <summary>
		/// Receive a notification that you were aborted.  Do what needs to be done, to re-do work the next go around,
		/// usually, set all related KExploerer nodes to Stale.
		/// </summary>
		void YouWereAborted();

	}
}

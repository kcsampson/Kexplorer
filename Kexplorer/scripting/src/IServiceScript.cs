using System;
using System.ServiceProcess;
using System.Windows.Forms;

namespace Kexplorer.scripting
{

	public delegate bool ServiceValidator( ServiceController service );
	/// <summary>
	/// Summary description for IServiceScript.
	/// </summary>
	public interface IServiceScript : IScript
	{

		// Called when on the selected services.
		void Run( ServiceController[] services, Form mainForm, DataGrid serviceGrid );


		ServiceValidator Validator { get; }
	}
}

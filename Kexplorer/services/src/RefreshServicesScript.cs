using System;
using System.ServiceProcess;
using System.Windows.Forms;

namespace Kexplorer.services
{
	/// <summary>
	/// Summary description for RefreshServicesScript.
	/// </summary>
	public class RefreshServicesScript : BaseServiceScript
	{
		public RefreshServicesScript()
		{
			this.LongName = "Refresh";

			this.ScriptShortCut = Shortcut.F5;

			this.Active = true;

		}

		public override void Run(ServiceController[] services, Form mainForm, DataGrid serviceGrid)
		{
			this.RefreshGridFromService( mainForm, serviceGrid );
		}
	}
}

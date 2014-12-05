using System;
using System.ServiceProcess;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.services
{
	/// <summary>
	/// Summary description for StopServiceScript.
	/// </summary>
	public class StartServiceScript : BaseServiceScript
	{
        private bool needsRunAs = false;
		public StartServiceScript()
		{
			this.LongName = "Start";

			this.Active = true;

			this.Description = "Starts the selected services";


			this.Validator = new ServiceValidator( this.OnlyStartable );
		}

		public override void Run(ServiceController[] services, Form mainForm, DataGrid serviceGrid)
		{
			foreach ( ServiceController service in services )
			{
                if (!needsRunAs)
                {
                    try
                    {
                        service.Start();
                    }
                    catch (Exception e)
                    {
                        needsRunAs = true;
                    }
                }


                if (needsRunAs)
                {
                    this.ScriptHelper.RunProgram("net", "start " + service.ServiceName, null, true);
                }


			}

            System.Threading.Thread.Sleep(500);

			this.RefreshGridFromService( mainForm, serviceGrid );


		}


		private bool OnlyStartable( ServiceController service )
		{

			return service.Status == ServiceControllerStatus.Stopped;
		}
	}
}

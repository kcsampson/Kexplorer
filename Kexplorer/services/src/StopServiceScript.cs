using System;
using System.ServiceProcess;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.services
{
	/// <summary>
	/// Summary description for StopServiceScript.
	/// </summary>
	public class StopServiceScript : BaseServiceScript
	{
        private static bool needsRunAs = false;
		public StopServiceScript()
		{
			this.LongName = "Stop";

			this.Active = true;

			this.Description = "Stops the selected services";


			this.Validator = new ServiceValidator( this.OnlyStoppableAndStarted );
		}

		public override void Run(ServiceController[] services, Form mainForm, DataGrid serviceGrid)
		{
			foreach ( ServiceController service in services )
			{
                if (!needsRunAs)
                {
                    try
                    {
                        service.Stop();
                    }
                    catch (Exception e)
                    {
                        needsRunAs = true;
                    }
                }

                if (needsRunAs)
                {

                    this.ScriptHelper.RunProgram("net", "stop " + service.ServiceName, null, true);
                }


            }

            System.Threading.Thread.Sleep(500);

			this.RefreshGridFromService( mainForm, serviceGrid );


		}


		private bool OnlyStoppableAndStarted( ServiceController service )
		{

			return service.CanStop;
		}
	}
}

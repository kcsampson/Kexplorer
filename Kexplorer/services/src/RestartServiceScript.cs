using System;
using System.ServiceProcess;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.services
{
	/// <summary>
	/// Summary description for StopServiceScript.
	/// </summary>
	public class RestartServiceScript : BaseServiceScript
	{
        private bool needsRunAs = false;
        public RestartServiceScript()
		{
			this.LongName = "Restart";

			this.Active = true;

			this.Description = "Restarts the selected services";


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
                        service.WaitForStatus(ServiceControllerStatus.Stopped);
          
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running);
                    }
                    catch (Exception e)
                    {
                        needsRunAs = true;
                    }
                }


                if (needsRunAs)
                {
                    this.ScriptHelper.RunProgram("net", "stop " + service.ServiceName, null, true);
                    service.WaitForStatus(ServiceControllerStatus.Stopped);
                    System.Threading.Thread.Sleep(500);                    
                    this.ScriptHelper.RunProgram("net", "start " + service.ServiceName, null, true);
                    service.WaitForStatus(ServiceControllerStatus.Running);
                }


			}

            //System.Threading.Thread.Sleep(500);

			this.RefreshGridFromService( mainForm, serviceGrid );


		}


        private bool OnlyStoppableAndStarted(ServiceController service)
        {

            return service.CanStop;
        }
	}
}

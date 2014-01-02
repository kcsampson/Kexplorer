using System;
using System.Collections;
using System.Data;
using System.ServiceProcess;
using System.Windows.Forms;
using Kexplorer.scripting;
using Kexplorer.scripts;

namespace Kexplorer.services
{
	/// <summary>
	/// Summary description for BaseServiceScript.
	/// </summary>
	public abstract class BaseServiceScript : BaseScript, IServiceScript
	{

		private DataView tempView = null;
		public BaseServiceScript()
		{

		}


		public abstract void Run(ServiceController[] services, Form mainForm, DataGrid serviceGrid);


		private ServiceValidator validator = null;
		public ServiceValidator Validator
		{
			get {return this.validator; }
			set { this.validator = value; }
		}



		public void RefreshGridFromService( Form mainForm, DataGrid serviceGrid )
		{
			DataGrid dgrid = serviceGrid;

			this.tempView = (DataView)dgrid.DataSource;
			
			CurrencyManager bm = (CurrencyManager)dgrid.BindingContext[ this.tempView ];
			ArrayList arrSelectedRows = new ArrayList();
			
			
			this.tempView = (DataView) bm.List;	

			
			mainForm.Invoke( new InvokeDelegate( this.RefreshDataInGUIThread ));


	
			serviceGrid.Refresh();

		

		}

		private void RefreshDataInGUIThread()
		{

			foreach ( DataRow row in this.tempView.Table.Rows )
			{
				ServiceController service = (ServiceController)row["ServiceControllerObject"];

				service.Refresh();

				row["Status"] = service.Status.ToString();


				row["CanPauseAndContinue"] = service.CanPauseAndContinue;

				row["CanShutdown"] = service.CanShutdown;

				row["CanStop"] = service.CanStop;

			}




			
		}
	}
}

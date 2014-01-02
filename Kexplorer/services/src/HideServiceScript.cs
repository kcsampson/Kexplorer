using System;
using System.Collections;
using System.Data;
using System.ServiceProcess;
using System.Windows.Forms;

namespace Kexplorer.services
{
	/// <summary>
	/// Summary description for HideServiceScript.
	/// </summary>
	public class HideServiceScript : BaseServiceScript
	{
		private ArrayList rowsToDelete = null;
		private DataView dView = null;
		
		public HideServiceScript()
		{

			this.LongName = "Hide from View";

			this.Description = "Hide the selected service from the view.";

			this.ScriptShortCut = Shortcut.Del;

		}

		public override void Run(ServiceController[] services, Form mainForm, DataGrid serviceGrid)
		{



			DataGrid dgrid = serviceGrid;

			this.dView = (DataView)dgrid.DataSource;
			
			CurrencyManager bm = (CurrencyManager)dgrid.BindingContext[ dView ];
			ArrayList arrSelectedRows = new ArrayList();
			
			
			this.dView = (DataView) bm.List;	

			ArrayList sortedItems = new ArrayList();
			sortedItems.AddRange( dView.Table.Rows );

			DataRow[] rows = (DataRow[])sortedItems.ToArray( typeof( DataRow ));

			if ( this.dView.Sort != null && this.dView.Sort.Length > 0 )
			{
				try 
				{
					Array.Sort( rows, new  ServiceMgrWorkUnit.RowComparer( this.dView.Sort ));
				} 
				catch (Exception e)
				{
					Console.WriteLine("Unable to sort the rows as they were in the grid:" + e.Message );
				}
			}
	
			

			// See if multi's are selected
			this.rowsToDelete = new ArrayList();
			for ( int i = 0; i < rows.Length; i++ )
			{
				
				if ( dgrid.IsSelected(i))
				{
					
					rowsToDelete.Add(rows[i] );
				}
			
			}


			
			mainForm.Invoke( new InvokeDelegate( this.DeleteRowsInGUIThread ));


		}


		private void DeleteRowsInGUIThread()
		{
			foreach ( DataRow row in this.rowsToDelete )
			{
				dView.Table.Rows.Remove( row );
			}
		}
	}
}

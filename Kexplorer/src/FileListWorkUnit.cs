using System;
using System.Data;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer
{
	/// <summary>
	/// Summary description for FileListWorkUnit.
	/// </summary>
	public class FileListWorkUnit : IWorkUnit
	{
		#region Member Variables --------------------------------------------------------
		private KExplorerNode kNode = null;
		private ISimpleKexplorerGUI kForm = null;
		private IWorkGUIFlagger guiFlagger = null;

		private DataTable table = null;
		private DataView view = null;

		private bool stop = false;


		#endregion ----------------------------------------------------------------------

		#region Constructor -------------------------------------------------------------
		//-------------------------------------------------------------------
		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="node"></param>
		/// <param name="newForm"></param>
		/// <param name="flagger"></param>
		public FileListWorkUnit(KExplorerNode node,  ISimpleKexplorerGUI newForm, IWorkGUIFlagger flagger )
		{

			this.kForm = newForm;
			this.kNode = node;
			this.guiFlagger = flagger;

		}
		#endregion

		#region IWorkUnit Members -------------------------------------------------------

		//-------------------------------------------------------------------------------
		/// <summary>
		/// Start the job.
		/// </summary>
		/// <returns></returns>
		public IWorkUnit DoJob()
		{
			this.table = new DataTable("Files");

			DataColumn c = null;

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.IO.FileInfo");
			c.ColumnName = "Name";
			c.ReadOnly = true;
			c.Unique = true;
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "Ext";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.Int64");
			c.ColumnName = "Size";            
			c.ReadOnly = true;
			c.Unique = false;
            
            
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "Attrib";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);
			
			c = new DataColumn();
			c.DataType = System.Type.GetType("System.DateTime");
			c.ColumnName = "LastUpdate";
			c.ReadOnly =true;
			c.Unique = false;
		
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "UpdTime";
			c.ReadOnly = true;
			c.Unique = false;

			table.Columns.Add( c );


			try 
			{
    

				FileInfo[] files = DirPerfStat.Instance().GetFiles( this.kNode.DirInfo );


				foreach( FileInfo file in files )
				{
					if ( this.stop )
					{
						return null;
					}
					DataRow row = this.table.NewRow();

					row["Name"] = file;

					row["LastUpdate"] = file.LastWriteTime;

					row["Ext"] = file.Extension;

					row["Size"] = file.Length;


					row["Attrib"] = file.Attributes.ToString();

					row["UpdTime"] = file.LastWriteTime.TimeOfDay.ToString();
						
					this.table.Rows.Add( row );
				
				}
				if ( this.stop )
				{
					return null;
				}


			} 
			catch (Exception)
			{
			}
			// Insert code to create and populate columns.
			this.view = new DataView(table);


            
			
			if ( !this.stop )
			{
				this.kForm.MainForm.Invoke( new InvokeDelegate( this.SetDataToDataGrid));
			}
			
			return null;
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Set a flag to stop right now.
		/// </summary>
		public void Abort()
		{
			this.stop = true;
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Upon notification of being aborted, set all related nodes to stale.
		/// </summary>
		public void YouWereAborted()
		{
			// Nothing to do, we're pretty much stateless.
		}

		#endregion ----------------------------------------------------------------------

		#region Private Methods ---------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Run in the Form's thread with Invoke...
		/// </summary>
		private void SetDataToDataGrid()
		{
			if ( this.kNode.DirInfo == null )
			{
				return;
			}
            
				//this.kForm.DataGrid1.CaptionText = this.kNode.DirInfo.FullName;

                this.view.Sort = "LastUpdate desc";
			
			this.kForm.DataGridView1.DataSource = this.view;
            try
            {
            }
            catch (Exception e)
            {
                
            }

			try 
			{
               

					this.kForm.DataGridView1.Columns["Name"].Width = 250;
                    string NRFormat = "###,###,###,##0";
                this.kForm.DataGridView1.Columns["Size"].DefaultCellStyle.Format = NRFormat;
                this.kForm.DataGridView1.Columns["Size"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                  // this.kForm.DataGrid1.TableStyles["Files"].GridColumnStyles["Size"].Alignment = HorizontalAlignment.Right;
   
															

			} 
			catch (Exception e )
			{
				Console.WriteLine("Error setting Name column width."  + e.Message );
			}
		}
		#endregion ----------------------------------------------------------------------
	}
}

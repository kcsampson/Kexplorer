using System;
using System.Data;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Collections;


namespace Kexplorer
{
	/// <summary>
	/// Summary description for FileListWorkUnit.
	/// </summary>
	public class FtpFileListWorkUnit : IWorkUnit
	{
		#region Member Variables --------------------------------------------------------
		private KexplorerFtpNode kNode = null;
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
		public FtpFileListWorkUnit(KexplorerFtpNode node,  ISimpleKexplorerGUI newForm, IWorkGUIFlagger flagger )
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
			c.DataType = System.Type.GetType("System.String");
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
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "Size";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);


			
			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "LastUpdate";
			c.ReadOnly =true;
			c.Unique = false;
		
			table.Columns.Add(c);


			try 
			{
				FtpFileInfo[] files = GetFiles( this.kNode );


				foreach( FtpFileInfo file in files )
				{
					if ( this.stop )
					{
						return null;
					}
					DataRow row = this.table.NewRow();

					//row["Entry"] = file.ftplistline;
                    row["Name"] = file.name;

					 row["LastUpdate"] = file.lastupdate;

					row["Ext"] = file.ext;

					row["Size"] = file.size;

						
					this.table.Rows.Add( row );
				
				}
				if ( this.stop )
				{
					return null;
				}

			} 
			catch (Exception e)
			{
                String x = e.Message;
                Console.WriteLine(x);

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

			this.kForm.DataGridView1.DataSource = this.view;

			try 
			{

		       this.kForm.DataGridView1.Columns["Name"].Width = 320;
               string NRFormat = "###,###,###,##0";
               this.kForm.DataGridView1.Columns["Size"].DefaultCellStyle.Format = NRFormat;
               this.kForm.DataGridView1.Columns["Size"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
					
			}
				
																

			
			catch (Exception e )
			{
				Console.WriteLine("Error setting Name column width."  + e.Message );
			}
		}


        /**
         * Get the files.
         */
        private FtpFileInfo[] GetFiles(KexplorerFtpNode knode)
        {


            ArrayList files = new ArrayList();
            string ftpfullpath = "ftp://" + kNode.Site.host +knode.Path;
            FtpWebResponse ftpResponse = null;
            FtpWebRequest ftp = null;
            try
            {

                ftp = (FtpWebRequest)FtpWebRequest.Create(ftpfullpath);


                ftp.Credentials = new NetworkCredential(kNode.Site.username, kNode.Site.pwd);
                //userid and password for the ftp server to given  

                ftp.KeepAlive = true;
                ftp.UseBinary = true;

                ftp.Method = WebRequestMethods.Ftp.ListDirectoryDetails;



                ftpResponse = (FtpWebResponse)ftp.GetResponse();


                Stream responseStream = null;

                responseStream = ftpResponse.GetResponseStream();


                string strFile = null;

                try
                {
                    StreamReader reader = new StreamReader(responseStream);
                    while (true)
                    {
                        strFile = null;
                        try
                        {
                            strFile = reader.ReadLine();
                        }
                        catch (IOException e)
                        {
                            break;
                        }



                        if (strFile != null)
                        {
                            FtpFileInfo newFileInfo = new FtpFileInfo(strFile, kNode.Site.type);
                            if ( !newFileInfo.isDir )
                            {

                                files.Add( newFileInfo );
                                
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    String x = e.Message;

                }



                try
                {
                    ftpResponse.Close();
                }
                catch (Exception e)
                {
                }
            }

            catch (WebException e)
            {

                return null;
            }
            finally
            {
                if (ftpResponse != null)
                {
                    ftpResponse.Close();
                }
            }


            return (FtpFileInfo[])files.ToArray(typeof(FtpFileInfo));

        }


		#endregion ----------------------------------------------------------------------
	}
}

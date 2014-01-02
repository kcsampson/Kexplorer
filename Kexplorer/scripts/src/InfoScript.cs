using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Kexplorer.res;

namespace Kexplorer.scripts
{
	public delegate void KillCallback( BackgroundRun runner );
	/// <summary>
	/// Provide info on the selected item.
	/// </summary>
	public class InfoScript : BaseFileAndFolderScript
	{


		private ArrayList runners = new ArrayList();
		
		public InfoScript()
		{

			this.LongName = "Info";

			this.Active = true;

			this.Description = "Information on the selected folder or file.";

			this.ScriptShortCut = Shortcut.AltF1;
		
		}


		~InfoScript()
		{

			foreach ( BackgroundRun run in this.runners )
			{

				run.WasKilled = null;

				run.Kill = true;
			}
		}


		/// <summary>
		/// Just work for the first file.
		/// </summary>
		/// <param name="folder"></param>
		/// <param name="files"></param>
		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			
			if ( files != null && files.Length > 0 )
			{

				FileInfo file = files[0];
				string info = "File:           " + file.Name + "\r"
							+ "Size:           " + file.Length + "\r"
							+ "Last updated:   " + file.LastWriteTime.ToString();


				MessageBox.Show( info, "File Info", MessageBoxButtons.OK );

				
			}
		}

		public override void Run(KExplorerNode folder)
		{

			BackgroundRun runner = new BackgroundRun( folder.DirInfo);

			runner.WasKilled = new KillCallback( this.Killed );

			this.runners.Add( runner );
			
		}


		/// <summary>
		/// Called when a background runner says he's been killed.
		/// </summary>
		/// <param name="runner"></param>
		private void Killed( BackgroundRun runner )
		{
			this.runners.Remove( runner );

		}





	}


	/// <summary>
	/// Run the directory info in it's own Thread of a dialog window.
	/// Another thread walks through the sub-dirs and gets the info
	/// A cancel button let's the user abort if it's taking too long.
	/// </summary>
	public class BackgroundRun 
	{
		private int subFolderCount = 0;
		private int filesCount = 0;
		private long filesSize = 0;
		private DirectoryInfo startFrom = null;
		private bool kill = false;

		private Thread guiThread = null;
		private Thread statsThread = null;

		private KillCallback wasKilled = null;


		private ThreadedInfoForm infoForm = null;


		private bool complete = false;

		private bool displayComplete = false;



		public BackgroundRun( DirectoryInfo newStartFrom )
		{
			this.startFrom = newStartFrom;

			this.statsThread = new Thread( new ThreadStart( this.StartStats ));


			this.statsThread.Start();

			Thread.Sleep( 30 );

			this.infoForm = new ThreadedInfoForm( this.startFrom.Name );
			

			this.infoForm.CancelCallback = new InvokeDelegate( this.GuiCancelCallback );


			this.infoForm.Show();

			this.guiThread = new Thread( new ThreadStart( this.StartGUIUpdating));

			this.guiThread.Start();

		}


		private void StartStats()
		{

			this.DoStats( this.startFrom );

			this.complete = true;
		}

		private void DoStats( DirectoryInfo dir )
		{

			if ( this.kill )
			{
				return;
			}
			foreach ( FileInfo file in dir.GetFiles() )
			{

				if ( this.kill )
				{
					return;
				}
				this.filesCount++;
				try 
				{
					this.filesSize += file.Length;
				} 
				catch ( Exception e )
				{
					try 
					{
						Console.WriteLine("File not found???.."+e.Message);
					
						Console.WriteLine("Wil count it but adds no size..."+file.FullName );
					} 
					catch (Exception ee)
					{
						Console.WriteLine("Even an exception on the above message."+ee.Message);
					}
				}
			}

			foreach ( DirectoryInfo subdir in dir.GetDirectories() )
			{
				if ( this.kill )
				{
					return;
				}
				this.subFolderCount++;

				this.DoStats( subdir );

				FileSystemInfo x = dir.GetFileSystemInfos()[0];

			}

	
		}


		private void StartGUIUpdating()
		{

	
			while ( !this.kill  )
			{

				Thread.Sleep( 250 );


					if ( !this.displayComplete )
					{
						if ( this.complete )
						{
							this.displayComplete = true;

						}

						this.infoForm.Invoke( new InvokeDelegate( this.UpdateGUI ));
					}
				

			}


			if (this.infoForm.Visible)
			{
				this.CloseForm();
			}


			if ( this.wasKilled != null )
			{

				this.wasKilled( this );
			}
			
		}





		private void CloseForm()
		{
			this.infoForm.Close();
		}

	
		private void UpdateGUI()
		{

			this.infoForm.FilesLabel.Text = this.filesCount.ToString("#,##0");

			long fsize = this.filesSize;

			if ( (fsize >> 35 ) > 0 )
			{
				this.infoForm.SizeLabel.Text = (fsize >> 30).ToString("#,##0") + " Gig";
			} 
			else if ( (fsize >> 25 ) > 0) 
			{
				this.infoForm.SizeLabel.Text = (fsize >> 20).ToString("#,##0") + " Meg";
			}
			else if ( ( fsize >> 15 ) > 0)
			{
				this.infoForm.SizeLabel.Text = (fsize >>10 ).ToString("#,##0") + " Kbytes";
			}			
			else 
			{
				this.infoForm.SizeLabel.Text = fsize.ToString("#,##0") + " bytes";
			}

			this.infoForm.SubFolderLabel.Text = this.subFolderCount.ToString("#,##0");

			if ( this.displayComplete )
			{

				this.infoForm.OKCancelBtn.Text = "OK";
			}
		}



		private void GuiCancelCallback()
		{


			this.kill = true;

		}


		public bool Kill 
		{
			get { return this.kill; }
			set { this.kill = true; }
		}


		/// <summary>
		/// Call this callback, when the callback comes from the GUI.
		/// The parent will de=register this thread thing from his runners.
		/// </summary>
		public KillCallback WasKilled
		{

			get { return this.wasKilled;}
			set { this.wasKilled = value; }

		}
	}
}

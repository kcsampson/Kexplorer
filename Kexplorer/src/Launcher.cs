using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using Kexplorer.scripting;

namespace Kexplorer
{
	/// <summary>
	/// Summary description for Launcher.
	/// </summary>
	public class Launcher
	{
		#region Member Variables --------------------------------------------------------
		private XmlDocument launchers = null;

		private static string defaultLauncherPath = "//Launcher[@ext='*']";
		#endregion ----------------------------------------------------------------------

		#region Constructor -------------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Simple Constructor
		/// </summary>
		public Launcher()
		{

		}
		#endregion ----------------------------------------------------------------------

		#region Public methods ----------------------------------------------------------

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// The launchers is an Xml in the same directory as the program. launchers.xml
		/// It holds a list of extensions, and cmd.  With alternate options.
		/// </summary>
		public void Initialize()
		{
		

			// See if the launchers.xml is in the application path.
			this.launchers = new XmlDocument(); 
			try 
			{
				string launcherFileName = Application.StartupPath + "\\launchers.xml";
				this.launchers.Load( launcherFileName );
			}
			catch ( Exception )
			{
				// File may not exist yet.
			}



		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Launch the given file based on extension.
		/// </summary>
		/// <param name="fileInfo"></param>
		public void Launch( FileInfo fileInfo )
		{
			string xpath= "";
			string[] executables = new string[]{ ".exe",".cmd",".bat",".lnk"};
			if ( fileInfo.Extension != null &&
				fileInfo.Extension.Length > 1 ){

				string extension = fileInfo.Extension.ToLower();

				xpath = "//Launcher[@ext='"+extension.Substring(1)+"']";
			}

			string ext = fileInfo.Extension.ToLower();
			foreach ( string exe in executables )
			{
				if ( ext.Equals( exe ))
				{
					Process cmd = new Process();

					cmd.StartInfo.FileName =fileInfo.FullName;

						cmd.StartInfo.WorkingDirectory = fileInfo.DirectoryName;

					cmd.Start();
					return;

				}
			}
			XmlNode launchNode = this.launchers.SelectSingleNode( xpath );

			if ( launchNode == null )
			{
				launchNode = this.launchers.SelectSingleNode( Launcher.defaultLauncherPath );
			}
			if ( launchNode != null )
			{
				string command = launchNode.Attributes["command"].Value;

				string option = null;
				if ( launchNode.Attributes.GetNamedItem("options") != null )
				{
					option = launchNode.Attributes["options"].Value;
				}

				string preOption = null;

				if (launchNode.Attributes.GetNamedItem("preOption") != null )
				{
					preOption = launchNode.Attributes["preOption"].Value;
				}


				this.LaunchProgramOnFile( fileInfo, preOption, command, option);

			}
		}

		
		#endregion ----------------------------------------------------------------------

		#region Private Methods ---------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Launch a program
		/// </summary>
		/// <param name="exe"></param>
		/// <param name="file"></param>
		/// <param name="preOption"></param>
		/// <param name="options"></param>
		private void LaunchProgramOnFile( FileInfo file, string preOption, string exe, string options )
		{
			Process cmd = new Process();

			cmd.StartInfo.FileName = exe;

			if ( preOption != null && preOption.Length > 0 )
			{
				cmd.StartInfo.Arguments = preOption + " ";
			}

			cmd.StartInfo.Arguments += "\"" +file.Name + "\""; 
			if ( options != null )
			{
				cmd.StartInfo.Arguments += " " + options;
			}


			cmd.StartInfo.WorkingDirectory = file.Directory.FullName;

	

			cmd.Start();
		}
		#endregion ----------------------------------------------------------------------
	}
}

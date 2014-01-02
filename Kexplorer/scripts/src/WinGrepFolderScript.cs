using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Kexplorer.res;
using System.Collections.Generic;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for WinGrepFolderScript.
	/// </summary>
	public class WinGrepFolderScript : BaseFolderScript
	{
		public WinGrepFolderScript()
		{
			this.LongName = "WinGrep";
			this.Description = "Launch WinGrep in the selected directory.";

			this.Active = true;
		
		}

		public override void Run(KExplorerNode folder)
		{
			string searchString = QuickDialog.DoQuickDialog( "WinGrep Search", "Search String");

			if ( searchString == null )
			{
				return;
			}

			string parFileName = Application.StartupPath + "\\wingrepinput.par";
			StreamWriter parFile = File.CreateText( parFileName );


			parFile.Write("[General Search Parameters]\n" );
			parFile.Write("Search String=" + searchString + "\n" );

			parFile.Write("Skip Text Files=False\n");
			parFile.Write("Skip Binary Files=True\n");
			parFile.Write("Recurse Subdirectories=True\n" );
			parFile.Write("Count Files First=False\n");


			parFile.Write("[Active File Specifications]\n");

            IList<String> extensions = this.ScriptHelper.GetValueList("//WinGrepExt");

            if (extensions != null && extensions.Count > 0)
            {
                int i = 0;

                foreach ( String x in extensions ){
                    parFile.Write( i.ToString() + "=*." + x + "\n" );
                    i++;
                }

            }
            else
            {


                parFile.Write("0=*.cs\n");
                parFile.Write("1=*.txt\n");
                parFile.Write("2=*.xml\n");
                parFile.Write("3=*.java\n");
                parFile.Write("4=*.csproj\n");
                parFile.Write("5=*.sln\n");
                parFile.Write("6=*.mak\n");
                parFile.Write("7=*.xslt\n");
                parFile.Write("8=*.php\n");
                parFile.Write("9=*.php5\n");
                parFile.Write("10=*.h\n");
                parFile.Write("11=*.cpp\n");
                parFile.Write("12=*.inc\n");
                parFile.Write("13=*.*66\n");
                parFile.Write("14=*.sql\n");
                parFile.Write("15=*.otl\n");
                parFile.Write("16=*.config\n");
                parFile.Write("17=*.aspx\n");
                parFile.Write("18=*.ascx\n");
                parFile.Write("19=*.properties\n");
            }


			parFile.Write("[Active Directory Specifications]\n");
			parFile.Write("0="+folder.DirInfo.FullName+"\n");

			parFile.Flush();

			parFile.Close();



			

			this.ScriptHelper.RunProgram( @"grep32.exe"
				, "-F"+parFileName
				, folder );
		}
	}
}

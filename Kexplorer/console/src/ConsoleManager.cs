using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Kexplorer.console
{
	/// <summary>
	/// Manages the threading of the Standard Input and Output streams
	/// of a Console process.
	/// </summary>
	public class ConsoleManager
	{

		StreamReader outputReader = null;

		StreamWriter inputWriter = null;

		Process cmd = new Process();

		Thread readerThread = null;

		bool killed = false;


		public ConsoleManager()
		{

		}



		/// <summary>
		/// Kick of the Console background process.  Start threads to get input and output.
		/// </summary>
		public void StartConsoleProcess()
		{
			this.killed = false;

			this.cmd.StartInfo.FileName = "cmd.exe";		

			this.cmd.StartInfo.WorkingDirectory = "C:\\";

			ProcessStartInfo psI = new ProcessStartInfo("cmd");
			psI.UseShellExecute = false;
			psI.RedirectStandardInput = true;
			psI.RedirectStandardOutput = true;
			psI.RedirectStandardError = true;
			psI.CreateNoWindow = true;
			cmd.StartInfo = psI;
			cmd.Start();
			this.inputWriter = cmd.StandardInput;
			this.outputReader = cmd.StandardOutput;
			this.inputWriter.AutoFlush = true;


			this.readerThread = new Thread( new ThreadStart( this.ReaderThreadRunner));

			cmd.Start();
		}




		public void ReaderThreadRunner()
		{
			while ( !this.killed )
			{
			}
		}
	}
}

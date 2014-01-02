using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace Kexplorer
{
	/// <summary>
	/// Directory Performance Statitics.  Used to capture how fast commands
	/// work with the File System
	/// </summary>
	public class DirPerfStat
	{
		#region Singleton Implementation ------------------------------------------------
		private static DirPerfStat instance = null;

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Singleton implementation
		/// </summary>
		/// <returns></returns>
		public static DirPerfStat Instance()
		{
			if ( DirPerfStat.instance == null )
			{
				DirPerfStat.instance = new DirPerfStat();
			}

			return DirPerfStat.instance;
		}
		#endregion ----------------------------------------------------------------------

		#region Member Variables --------------------------------------------------------

		private int getDirsCallCount = 0;

		private int getFilesCallCount = 0;

		private DirectoryInfo slowestGetDirsCall = null;

		private long slowestGetDirsTime = 0;

		private long slowestGetFilesTime = 0;

		private DirectoryInfo slowestGetFilesCall = null;


		private int makeDirInfoCount = 0;

		private long slowestMakeDirInfoTime = 0;

		private string slowerMakeDirInfo = null;

		private int lowPriorityThreadAbortCount = 0;

		private int highPriorityThreadAbortCount = 0;

		private int getDirectoiesThreadAbortCount = 0;

		private int getDirectoriesRetries = 0;

		private int getDirectoriesSuccessfulRetries = 0;



		// Once a GetDirectories() or GetFiles() takes this long, abort it.
		private long tickTimeOut = 1000000;

		private int tries = 3;



		#endregion ----------------------------------------------------------------------
		
		#region Constructor ------------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Simple Constructor
		/// </summary>
		private DirPerfStat()
		{

		}
		#endregion ----------------------------------------------------------------------

		#region Public Methods ----------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Any time we abort a thread, count it.
		/// </summary>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void IncrementHighPriorityThreadAborts()
		{
			this.highPriorityThreadAbortCount++;
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Any time we abort a thread, count it.
		/// </summary>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void IncrementLowPriorityThreadAborts()
		{
			this.lowPriorityThreadAbortCount++;
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// See how long it takes to Instantiate a DirectoryInfo() object.
		/// </summary>
		/// <param name="init"></param>
		/// <returns></returns>
		public DirectoryInfo MakeDirectoryInfo( string init )
		{

			this.makeDirInfoCount++;
			DateTime startTime = DateTime.Now;

			DirectoryInfo di = new DirectoryInfo( init );

			DateTime endTime = DateTime.Now;

			TimeSpan diff = endTime.Subtract( startTime );

			if ( this.slowestMakeDirInfoTime < diff.Ticks )
			{
				this.slowestMakeDirInfoTime = diff.Ticks;
				this.slowerMakeDirInfo = init;
										    
			}
			return di;

		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Record statistics on calling Get Files.
		/// </summary>
		/// <param name="dirInfo"></param>
		/// <returns></returns>
		public FileInfo[] GetFiles( DirectoryInfo dirInfo )
		{
			if ( dirInfo == null )
			{
				return null;
			}
			this.getFilesCallCount++;

			DateTime startTime = DateTime.Now;

			FileInfo[] result = dirInfo.GetFiles();

			DateTime endTime = DateTime.Now;

			TimeSpan diff = endTime.Subtract( startTime );

			if ( this.slowestGetFilesTime < diff.Ticks)
			{

				this.slowestGetFilesTime = diff.Ticks;
				this.slowestGetFilesCall = dirInfo;
			}

			Array.Sort( result, new FileInfoComparer() );

			return result;
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Record Statistics on called Get Directories.
		/// </summary>
		/// <param name="dirInfo"></param>
		/// <returns></returns>
		public DirectoryInfo[] GetDirectories( DirectoryInfo dirInfo )
		{
			this.getDirsCallCount++;

			DateTime startTime = DateTime.Now;


			DirectoryInfo[] result = null;
			
			for ( int i = 0; i < this.tries; i++ )
			{
				result = this.ThreadMonitoredGetDirectories( dirInfo );
				if ( result != null )
				{
					if ( i > 0 )
					{
						// It worked on a retry.
						this.getDirectoriesSuccessfulRetries++;
					}
					break;
				}
				this.getDirectoriesRetries++;
			}

			DateTime endTime = DateTime.Now;

			TimeSpan diff = endTime.Subtract(startTime);

			if ( this.slowestGetDirsTime < diff.Ticks )
			{

				this.slowestGetDirsTime = diff.Ticks;
				this.slowestGetDirsCall = dirInfo;
			}

            if (result != null)
            {
                Array.Sort(result, new DirInfoComparer());
            }

			return result;
		}


		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Write the results out to an xml file.
		/// </summary>
		public void WriteResults()
		{
			
			XmlDocument doc = new XmlDocument();


			StringBuilder output = new StringBuilder();

			output.Append( "<DirPerfStats>\r");

			output.Append( "<Result ");
			output.Append( "GetDirectoryRetries= \""+ this.getDirectoriesRetries.ToString() + "\" \r");
			output.Append( "GetDirectoryAborts= \""+ this.getDirectoiesThreadAbortCount.ToString() + "\" \r");
			output.Append( "GetDirectorySuccessfulRetries= \""+ this.getDirectoriesSuccessfulRetries.ToString() + "\" \r");
			output.Append( "HighPriorityThreadAborts= \""+ this.highPriorityThreadAbortCount.ToString() + "\" \r");
			output.Append( "LowPriorityThreadAborts= \""+ this.lowPriorityThreadAbortCount.ToString() + "\" \r");
			output.Append( "MakeDirInfoCalls= \""+ this.makeDirInfoCount.ToString() + "\" \r");
			output.Append( "SlowestMakeDirInfoCallTicks= \""+ this.slowestMakeDirInfoTime.ToString() + "\" \r");
			output.Append( "SlowestMakeDirInfoCallPath= \""+ this.slowerMakeDirInfo + "\" \r");


			output.Append( "GetDirsCalls= \""+ this.getDirsCallCount.ToString() + "\" \r");
			output.Append( "SlowestDirsCallTicks= \""+ this.slowestGetDirsTime.ToString() + "\" \r");
			output.Append( "SlowestDirsCallPath= \""+ this.slowestGetDirsCall.FullName + "\" \r");

			output.Append( "GetFilesCalls= \""+ this.getFilesCallCount.ToString() + "\" \r");
			output.Append( "SlowestFilesCallTicks= \""+ this.slowestGetFilesTime.ToString() + "\" \r");
			if ( this.slowestGetFilesCall != null )
			{
				output.Append( "SlowestFilesCallPath= \""+ this.slowestGetFilesCall.FullName + "\" \r");
			} 
			else 
			{
				output.Append( "SlowestFilesCallPath= \""+ "Unknown" + "\" \r");
			}

			output.Append( "");
			output.Append( "  />");
			output.Append( " </DirPerfStats>\r");

			doc.LoadXml( output.ToString());

			doc.Save(Application.StartupPath + "\\DirPerfStat.xml");

		}

		#endregion ----------------------------------------------------------------------

		#region Private Methods ---------------------------------------------------------
		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Do the work in a separate thread, monitor the time it takes, and kill it if
		/// it takes too long.
		/// </summary>
		/// <returns></returns>
		private DirectoryInfo[] ThreadMonitoredGetDirectories( DirectoryInfo startDir )
		{
			GetDirectoryiesThread dirThread = new GetDirectoryiesThread( startDir);


			dirThread.Start();

			DateTime startTime = DateTime.Now;
			while ( dirThread.WorkingDirInfoArr == null )
			{
				Thread.Sleep(1);

				if (DateTime.Now.Subtract(startTime).Ticks > this.tickTimeOut) 
				{
					dirThread.Abort();
					this.getDirectoiesThreadAbortCount++;
					break;
				}

			}

			return dirThread.WorkingDirInfoArr;
			 


		}





		#endregion ----------------------------------------------------------------------


		#region Inner Classes
		private class GetDirectoryiesThread
		{
			private DirectoryInfo[] workingDirInfoArr = null;
			private Thread thread;

			private DirectoryInfo workingSingleDirInfo = null;

			public GetDirectoryiesThread( DirectoryInfo sourceDir)
				
			{
				this.thread = new Thread(new ThreadStart(this.ThreadGetDirectories ) );

				if ( sourceDir == null )
				{
					Console.WriteLine( "Problem in setup..." );
				}
				this.workingSingleDirInfo = sourceDir;

			}

			public void Start()
			{
				this.thread.Start();
			}

			public void Abort()
			{
				this.thread.Abort();
			}

			/// <summary>
			/// This is the actual thread function.
			/// </summary>
			public void ThreadGetDirectories( )
			{
                try
                {
                    this.workingDirInfoArr = this.workingSingleDirInfo.GetDirectories();
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (NullReferenceException)
                {
                }
                catch (System.UnauthorizedAccessException)
                {
                }

			}


			public DirectoryInfo[] WorkingDirInfoArr 
			{
				get { return this.workingDirInfoArr; }
			}
		
			
		}
		}
		#endregion
	}


	#region IComparers for sorting ------------------------------------------------------
	//---------------------------------------------------------------------------------//
	/// <summary>
	/// Compare FileInfos by name.
	/// </summary>
	public class FileInfoComparer : IComparer
	{
		public int Compare(object x, object y)
		{
			FileInfo xx = (FileInfo)x;
			FileInfo yy = (FileInfo)y;

			string xname = xx.Name;

			string yname = yy.Name;

			if ( xname == null )
			{
				xname = "";
			}
			if ( yname == null )
			{
				yname = "";
			}

			return xname.CompareTo( yname );
		}

	}

	//---------------------------------------------------------------------------------//
	/// <summary>
	/// Compare DirectoryInfos by name
	/// </summary>
	public class DirInfoComparer : IComparer
	{
		public int Compare(object x, object y)
		{
			DirectoryInfo xx = (DirectoryInfo)x;
			DirectoryInfo yy = (DirectoryInfo)y;

			string xname = xx.Name;

			string yname = yy.Name;

			if ( xname == null )
			{
				xname = "";
			}
			if ( yname == null )
			{
				yname = "";
			}

			return xname.CompareTo( yname );
		}
	}

	#endregion --------------------------------------------------------------------------




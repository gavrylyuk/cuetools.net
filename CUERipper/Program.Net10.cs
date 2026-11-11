using System;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CUETools.Processor;
using CUETools.Processor.Settings;

namespace CUERipper
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.SetDefaultFont(new System.Drawing.Font("Microsoft Sans Serif", 8.25F));
            Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			CUEConfig config = new CUEConfig();
			config.Load(new SettingsReader("CUERipper", "settings.txt", Application.ExecutablePath));
			try { Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(config.language); }
			catch { }

			Application.Run(new frmCUERipper());
		}

	}
}

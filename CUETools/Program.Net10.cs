using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;


using System.Text;
using System.Threading;
using System.Windows.Forms;
using CUETools.Processor;
using CUETools.Processor.Settings;

namespace JDP
{
	static class Program {
		[STAThread]
		static void Main(string[] args)
		{
			Application.SetColorMode(UiHelp.LoadTheme());
			if (args.Length > 1 && args[0].Length > 1 && args[0][0] == '/')
			{
				Application.SetDefaultFont(new System.Drawing.Font("Microsoft Sans Serif", 8.25F));
            Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				frmBatch batch = new frmBatch();
				batch.Profile = args[0].Substring(1);

				if (args.Length == 2 && args[1][0] != '@')
					batch.InputPath = args[1];
				else for (int i = 1; i < args.Length; i++)
				{
					if (args[i][0] == '@')
					{
						string lineStr;
						StreamReader sr;
						try
						{
							sr = new StreamReader(args[i].Substring(1), CUETools.Codecs.AudioTextEncoding.Ansi);
							while ((lineStr = sr.ReadLine()) != null)
								batch.AddInputPath(lineStr);
						}
						catch
						{
							batch.AddInputPath(args[i]);
						}
					} else
						batch.AddInputPath(args[i]);
				}
				Application.Run(batch);
				return;
			}

			string myId = "BZ92759C-63Q7-444e-ADA6-E495634A493D";
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "parallel_experiment"))) myId += "-parallel-experimental";
			Application.SetDefaultFont(new System.Drawing.Font("Microsoft Sans Serif", 8.25F));
            Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			CUEConfig config = new CUEConfig();
			config.Load(new SettingsReader("CUE Tools", "settings.txt", Application.ExecutablePath));
			try { Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(config.language); }
			catch { }
			frmCUETools form = new frmCUETools();
            _ = form.Handle;
			if (!config.oneInstance || SingletonController.IamFirst(myId, new SingletonController.ReceiveDelegate(form.OnSecondCall)))
			{
				if (args.Length == 1)
					form.InputPath = args[0];
				Application.Run(form);
			}
			else
			{
				List<string> newArgs = new List<string>();
				foreach (string arg in args)
					newArgs.Add(Path.GetFullPath(arg));
				SingletonController.Send(myId, newArgs.ToArray());
			}
			SingletonController.Cleanup();
		}
	}

}

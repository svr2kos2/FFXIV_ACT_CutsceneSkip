using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FFXIV_ACT_CutsceneSkip
{
	public class CutsceneSkip : IActPluginV1
    {
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool ReadProcessMemory(
			IntPtr hProcess,
			IntPtr lpBaseAddress,
			[Out] byte[] lpBuffer,
			int dwSize,
			IntPtr lpNumberOfBytesRead);

		[DllImport("kernel32.dll")]
		static extern bool WriteProcessMemory(
			 IntPtr hProcess,
			 IntPtr lpBaseAddress,
			 byte[] lpBuffer,
			 Int32 nSize,
			 IntPtr lpNumberOfBytesWritten);

		static int Search(byte[] src, byte[] pattern)
		{
			for (int i = 0; i < src.Length; ++i)
			{
				for (int j = 0; i + j < src.Length; ++j)
				{
					if (j == pattern.Length)
						return i;
					if (pattern[j] != 0x2e && src[i + j] != pattern[j])
						break;
				}
			}
			return 0;
		}


		Label statusLabel = null;
		TabPage screenSpace = null;
		Process process = null;
		IntPtr baseAddress = IntPtr.Zero;
		Timer updateTimer = null;

		CheckBox toggleAlwaysEnable;
		Label lableAlwayEnable;


		bool SyncConfig(bool write = false)
		{
			ActPluginData actPluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);

			var filePath = actPluginData.pluginFile.DirectoryName;
			filePath = filePath + "\\cutscene_skip.cfg";
			if (write == false && File.Exists(filePath))
			{
				using (StreamReader sr = new StreamReader(filePath))
				{
					return bool.Parse(sr.ReadLine());
				}
			}
			else
			{
				using (StreamWriter sw = new StreamWriter(filePath))
				{
					sw.WriteLine(write);
					return false;
				}
			}
		}


		public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
		{
			screenSpace = pluginScreenSpace;
			statusLabel = pluginStatusText;

			toggleAlwaysEnable = new CheckBox();
			toggleAlwaysEnable.Location = new System.Drawing.Point(10, 10);
			toggleAlwaysEnable.Name = "toggleAlwaysEnable";
			toggleAlwaysEnable.Size = new System.Drawing.Size(20, 20);
			toggleAlwaysEnable.Checked = SyncConfig();
			toggleAlwaysEnable.CheckedChanged += EnableAlwaysActive;

			lableAlwayEnable = new Label();
			lableAlwayEnable.Location = new System.Drawing.Point(30, 12);
			lableAlwayEnable.Name = "lableAlwayEnable";
			lableAlwayEnable.Text = "保持开启状态";
			lableAlwayEnable.Size = new System.Drawing.Size(64, 20);

			screenSpace.Controls.Add(toggleAlwaysEnable);
			screenSpace.Controls.Add(lableAlwayEnable);

			process = null;
			Init();

			updateTimer = new Timer();
			updateTimer.Interval = 3000;
			updateTimer.Tick += Update;
			updateTimer.Start();
		}

		void Init()
		{
			Task.Run(() =>
			{
				while (process == null)
				{
					process = Process.GetProcessesByName("ffxiv_dx11").FirstOrDefault();
					statusLabel.Text = "FFXIV(dx11 only) not found.";
					System.Threading.Thread.Sleep(1000);
				}
			}).ContinueWith((t) =>
			{
				try
				{
					byte[] moduleData = new byte[process.MainModule.ModuleMemorySize];
					if (!ReadProcessMemory(process.Handle, process.MainModule.BaseAddress, moduleData, process.MainModule.ModuleMemorySize, IntPtr.Zero))
						throw new Exception("ReadProcessMemory failed.");
					byte[] pattern = { 0x2e, 0x32, 0xdb, 0xeb, 0x2e, 0x48, 0x8b, 0x01 };
					int match = Search(moduleData, pattern);
					if (match == 0)
						throw new Exception("Cannot find target bytes.");
					baseAddress = new IntPtr(match + process.MainModule.BaseAddress.ToInt64());
					if (!WriteProcessMemory(process.Handle, baseAddress, new byte[] { 0x2e }, 1, IntPtr.Zero))
						throw new Exception("WriteProcessMemory failed.");
					statusLabel.Text = "Working pid="+ process.Id;
					ActGlobals.oFormActMain.OnLogLineRead += this.oFormActMain_OnLogLineRead;
				}
				catch (Exception e)
				{
					statusLabel.Text = e.Message;
					process = null;
				}
			});
		}

		void Update(object sender, EventArgs e)
		{
			//statusLabel.Text = "Refeshing";
			if (process == null || process.HasExited || baseAddress == IntPtr.Zero)
			{
				Init();
			}
			else
			{
				if (statusLabel != null && !statusLabel.Text.Contains("Working :D"))
					Init();
			}
		}
		public void DeInitPlugin()
		{
			if (updateTimer != null && updateTimer.Enabled)
				updateTimer.Stop();
			if (process !=null && baseAddress!=IntPtr.Zero)
            {
				WriteProcessMemory(process.Handle, baseAddress, new byte[] { 0x04 }, 1, IntPtr.Zero);
				statusLabel.Text = "Exit :|";
            } else
				//statusLabel.Text = "Error :(";
			ActGlobals.oFormActMain.OnLogLineRead -= this.oFormActMain_OnLogLineRead;
			SyncConfig(toggleAlwaysEnable.Checked);
		}

		void SetActive(bool bActive)
        {
			if(statusLabel.Text.Contains("Working :D"))
            {
				try
				{
					WriteProcessMemory(process.Handle, baseAddress, new byte[] { (byte)(bActive ? 0x2e : 0x04) }, 1, IntPtr.Zero);
				}
				catch { }
            }
        }

		void EnableAlwaysActive(object sender, EventArgs e)
		{
			if(toggleAlwaysEnable.Checked)
				SetActive(true);
		}

		public void oFormActMain_OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
		{
			
			//MessageBox.Show("on log linke");
			if (statusLabel != null)
            {
				try
                {
					//ActPluginData actPluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);
					//var filePath = actPluginData.pluginFile.DirectoryName;
					//filePath = filePath + "\\loglines.cfg";
					//using (StreamWriter sw = new StreamWriter(filePath, true))
					//{
						//sw.WriteLine(logInfo.originalLogLine);
						if (logInfo.originalLogLine.Contains("Territory"))
						{
							if (toggleAlwaysEnable.Checked || logInfo.originalLogLine.Contains("Territory 01:E0:") || logInfo.originalLogLine.Contains("Territory 01:D9:"))
							{
								SetActive(true);
								statusLabel.Text = "Working :D enabled";
							}
							else
							{
								SetActive(false);
								statusLabel.Text = "Working :D disabled";
							}
						}
					//}
				} catch
                {
					statusLabel.Text = "Error :(";
				}
			}
		}

	}
}

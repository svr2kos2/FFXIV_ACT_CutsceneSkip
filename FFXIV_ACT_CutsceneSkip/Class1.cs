using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace FFXIV_ACT_CutsceneSkip
{
	public class MainCalss : IActPluginV1
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
		Timer retryTimer = null;

		public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
		{
			screenSpace = pluginScreenSpace;
			statusLabel = pluginStatusText;
			retryTimer = new Timer();
			try
            {
				process = Process.GetProcessesByName("ffxiv_dx11").FirstOrDefault();
				if (process == null)
					throw new Exception("You need to start FFXIV(DX11) to initialize this plugin.");
				byte[] moduleData = new byte[process.MainModule.ModuleMemorySize];
				if(!ReadProcessMemory(process.Handle, process.MainModule.BaseAddress, moduleData, process.MainModule.ModuleMemorySize, IntPtr.Zero))
					throw new Exception("ReadProcessMemory failed.");
				byte[] pattern = { 0x2e, 0x32, 0xdb, 0xeb, 0x2e, 0x48, 0x8b, 0x01 };
				int match = Search(moduleData, pattern);
				if (match == 0)
					throw new Exception("Cannot find target bytes.");
				baseAddress = new IntPtr(match + process.MainModule.BaseAddress.ToInt64());
				if(!WriteProcessMemory(process.Handle, baseAddress, new byte[] { 0x2e }, 1, IntPtr.Zero))
					throw new Exception("WriteProcessMemory failed.");
				statusLabel.Text = "Working :D";
				ActGlobals.oFormActMain.OnLogLineRead += this.oFormActMain_OnLogLineRead;
			} catch(Exception e)
            {
				statusLabel.Text = e.Message;
				process = null;
			}
			retryTimer.Interval = 3000;
			retryTimer.Tick += Refresh;
			retryTimer.Start();
		}

		void Refresh(object sender, EventArgs e)
		{
			if (process == null || process.HasExited || baseAddress == IntPtr.Zero)
			{
				ActPluginData actPluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);
				actPluginData.cbEnabled.Checked = false;
				actPluginData.cbEnabled.Checked = true;
			}
			else
			{
				try
				{
					byte[] current = new byte[1];
					if (!ReadProcessMemory(process.Handle, baseAddress, current, 1, IntPtr.Zero))
						throw new Exception("ReadProcessMemory failed.");
					if (current[0] != 0x2e)
						throw new Exception("Update.");
				}
				catch
				{
					ActPluginData actPluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);
					actPluginData.cbEnabled.Checked = false;
					actPluginData.cbEnabled.Checked = true;
				}
			}
		}
		public void DeInitPlugin()
		{
			if (retryTimer != null && retryTimer.Enabled)
				retryTimer.Stop();
			if (process !=null && baseAddress!=IntPtr.Zero)
            {
				WriteProcessMemory(process.Handle, baseAddress, new byte[] { 0x04 }, 1, IntPtr.Zero);
				statusLabel.Text = "Exit :|";
            } else
				statusLabel.Text = "Error :(";
			ActGlobals.oFormActMain.OnLogLineRead -= this.oFormActMain_OnLogLineRead;
		}

		void SetActive(bool bActive)
        {
			if(statusLabel.Text == "Working :D")
            {
				try
				{
					WriteProcessMemory(process.Handle, baseAddress, new byte[] { (byte)(bActive ? 0x2e : 0x04) }, 1, IntPtr.Zero);

				}
				catch { }
            }
        }

		public void oFormActMain_OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
		{
			
			//MessageBox.Show("on log linke");
			if (statusLabel != null)
            {
				try
                {
					ActPluginData actPluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);
					var filePath = actPluginData.pluginFile.DirectoryName;
					filePath = filePath + "\\loglines.cfg";
					using (StreamWriter sw = new StreamWriter(filePath, true))
					{
						sw.WriteLine(logInfo.originalLogLine);
						if (logInfo.originalLogLine.Contains("Territory"))
						{
							if (logInfo.originalLogLine.Contains("天幕魔导城最终决战") || logInfo.originalLogLine.Contains("帝国南方堡外围激战"))
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
					}
				} catch
                {
					statusLabel.Text = "Error :(";
				}
			}
		}

	}
}

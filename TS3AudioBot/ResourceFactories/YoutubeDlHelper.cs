// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.ResourceFactories
{
	using Config;
	using Localization;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;

	internal static class YoutubeDlHelper
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public static ConfPath DataObj { private get; set; }
		private static string YoutubeDlPath => DataObj?.Path.Value;

		public static R<(string title, IList<string> links), LocalStr> FindAndRunYoutubeDl(string id)
		{
			var ytdlPath = FindYoutubeDl(id);
			if (ytdlPath == null)
				return new LocalStr(strings.error_ytdl_not_found);

			return RunYoutubeDl(ytdlPath.Value.ytdlpath, ytdlPath.Value.param);
		}

		public static (string ytdlpath, string param)? FindYoutubeDl(string id)
		{
			string param = $"--no-warnings --get-title --get-url --format bestaudio/best --id -- {id}";

			// Default path youtube-dl is suggesting to install
			const string defaultYtDlPath = "/usr/local/bin/youtube-dl";
			if (File.Exists(defaultYtDlPath))
				return (defaultYtDlPath, param);

			if (YoutubeDlPath == null)
				return null;

			string fullCustomPath;
			try { fullCustomPath = Path.GetFullPath(YoutubeDlPath); }
			catch (ArgumentException ex)
			{
				Log.Warn(ex, "Your youtube-dl path contains invalid characters");
				return null;
			}

			// Example: /home/teamspeak/youtube-dl where 'youtube-dl' is the binary
			if (File.Exists(fullCustomPath) || File.Exists(fullCustomPath + ".exe"))
				return (fullCustomPath, param);

			// Example: /home/teamspeak where the binary 'youtube-dl' lies in ./teamspeak/
			string fullCustomPathWithoutFile = Path.Combine(fullCustomPath, "youtube-dl");
			if (File.Exists(fullCustomPathWithoutFile) || File.Exists(fullCustomPathWithoutFile + ".exe"))
				return (fullCustomPathWithoutFile, param);

			// Example: /home/teamspeak/youtube-dl where 'youtube-dl' is the github project folder
			string fullCustomPathGhProject = Path.Combine(fullCustomPath, "youtube_dl", "__main__.py");
			if (File.Exists(fullCustomPathGhProject))
				return ("python", $"\"{fullCustomPathGhProject}\" {param}");

			return null;
		}

		public static R<(string title, IList<string> links), LocalStr> RunYoutubeDl(string path, string args)
		{
			try
			{
				using (var tmproc = new Process())
				{
					tmproc.StartInfo.FileName = path;
					tmproc.StartInfo.Arguments = args;
					tmproc.StartInfo.UseShellExecute = false;
					tmproc.StartInfo.CreateNoWindow = true;
					tmproc.StartInfo.RedirectStandardOutput = true;
					tmproc.StartInfo.RedirectStandardError = true;
					tmproc.EnableRaisingEvents = true;
					tmproc.Start();
					tmproc.WaitForExit(10000);

					using (var reader = tmproc.StandardError)
					{
						string result = reader.ReadToEnd();
						if (!string.IsNullOrEmpty(result))
						{
							Log.Error("youtube-dl failed to load the resource:\n{0}", result);
							return new LocalStr(strings.error_ytdl_song_failed_to_load);
						}
					}

					return ParseResponse(tmproc.StandardOutput);
				}
			}
			catch (Win32Exception ex)
			{
				Log.Error(ex, "Failed to run youtube-dl: {0}", ex.Message);
				return new LocalStr(strings.error_ytdl_failed_to_run);
			}
		}

		public static (string title, IList<string> links) ParseResponse(StreamReader stream)
		{
			string title = stream.ReadLine();

			var urlOptions = new List<string>();
			string line;
			while ((line = stream.ReadLine()) != null)
				urlOptions.Add(line);

			return (title, urlOptions);
		}
	}
}

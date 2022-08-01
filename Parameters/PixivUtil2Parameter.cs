﻿namespace ParallelPixivUtil2.Parameters
{
	public sealed record PixivUtil2Parameter(string Executable, string PythonExecutable, string PythonScript, bool IsPythonScript, string ParameterFormat, string WorkingDirectory, string LogPath) : AbstractParameter(ParameterFormat)
	{
		public override string FileName => IsPythonScript ? PythonExecutable : Executable;

		public override string ExtraParameters => IsPythonScript ? (PythonScript + ' ') : "";

		protected override IDictionary<string, string> ParameterTokens
		{
			get
			{
				var dict = new Dictionary<string, string>
				{
					["logPath"] = LogPath,
				};

				if (Ipc != null)
					dict["ipcAddress"] = Ipc?.Identifier + '|' + Ipc?.IPCCommunicationAddress + '|' + Ipc?.IPCTaskAddress;

				if (DatabasePath != null)
					dict["databasePath"] = DatabasePath;

				if (Aria2InputPath != null)
					dict["aria2InputPath"] = Aria2InputPath;

				if (Member != null)
				{
					dict["memberID"] = Member?.MemberID.ToString()!;
					dict["page"] = Member?.Page!.ToString()!;
					dict["fileIndex"] = Member?.Page!.FileIndex.ToString()!;
				}

				return dict;
			}
		}

		public string? Aria2InputPath
		{
			get; set;
		}

		public string? DatabasePath
		{
			get; set;
		}

		public MemberParameter? Member
		{
			get;set;
		}

		public IpcParameter? Ipc
		{
			get; set;
		}
	}

	public struct MemberParameter
	{
		public long? MemberID
		{
			get; set;
		}

		public MemberPage? Page
		{
			get; set;
		}
	}

	public struct IpcParameter
	{
		public string Identifier
		{
			get;
			set;
		}

		public string IPCCommunicationAddress
		{
			get;
			set;
		}

		public string IPCTaskAddress
		{
			get;
			set;
		}
	}
}

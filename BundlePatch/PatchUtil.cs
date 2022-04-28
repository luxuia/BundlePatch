using System;
using System.Collections.Generic;
using System.IO;

namespace BundlePatch
{
	public static class PatchUtil
	{
		public class PatchInfoData
		{
			public class Patch
			{
				public int offset;
				public int size;
				public bool is_patch;
			}

			public List<Patch> infos = new List<Patch>();

			public string srcFileName;
			public string dstFileName;
			public string patchFileName;

			public string srcMD5;
			public string dstMD5;
		}

		public static string DummyHeader = "XX19";

		public static void CopyLength(this Stream stream, Stream tostream, int length)
        {
			var bytes = new byte[length];
			stream.Read(bytes);

			tostream.Write(bytes);
        }

		public static string GetBaseName(string path)
        {
			return path + ".base";
        }

		public static string GetPatchName(string path)
        {
			return path + ".bytes";
        }
	}
}


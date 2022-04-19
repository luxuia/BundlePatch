using System;
using AssetStudio;
using K4os.Compression.LZ4;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BundlePatch
{
    public class ObjectStreaming
    {

        public long offset; //ulong
        public uint size;
        public string path;

        public string name;
        public AssetStudio.Object obj;

    }

    public class PatchMgr 
	{
        BundleFile bundle;
		public PatchMgr(BundleFile bundle)
		{
            this.bundle = bundle;
		}

        static byte[] GetBytes(MemoryStream stream)
        {
            var ret = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(ret);
            return ret;
        }

        public EndianBinaryWriter Write(Stream stream, List<AssetStudio.Object> topatchlist = null, bool reshuffle = false)
        {
            var m_Header = bundle.m_Header;

            var writer = new EndianBinaryWriter(stream);
            writer.WriteStringNull(m_Header.signature);
            writer.Write(m_Header.version);
            writer.WriteStringNull(m_Header.unityVersion);
            writer.WriteStringNull(m_Header.unityRevision);
            // FS Header
            /*
            m_Header.size = reader.ReadInt64();
            m_Header.compressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.flags = reader.ReadUInt32();
            if (m_Header.signature != "UnityFS")
            {
                reader.ReadByte();
            }
             * 
             */

            var blockData = GetBlockData(topatchlist, reshuffle);

            var compressedBytes = new byte[m_Header.uncompressedBlocksInfoSize];

            uint compressedSize = 0;
            uint uncompressedSize = 0;
            GetBlockInfo(compressedBytes, out uncompressedSize, out compressedSize);

            var size = writer.Position
                + Marshal.SizeOf(m_Header.size)
                + Marshal.SizeOf(m_Header.compressedBlocksInfoSize)
                + Marshal.SizeOf(m_Header.uncompressedBlocksInfoSize)
                + Marshal.SizeOf(m_Header.flags);
            // align
            if (size % 16 != 0) size += (16 - (size % 16));

            size += compressedSize + blockData.Length;

            writer.Write(size);
            writer.Write(compressedSize);
            writer.Write(uncompressedSize);
            writer.Write(m_Header.flags);

            //BLOCK INFO
            writer.AlignStream(16);

            writer.Write(compressedBytes, 0, (int)compressedSize);
            writer.Write(blockData);

            return writer;
        }

        public static List<ObjectStreaming> GetStreamingDatas(AssetStudio.Object patch)
        {
            List<ObjectStreaming> list = new List<ObjectStreaming>();

            var name = patch is NamedObject ? (patch as NamedObject).m_Name : patch.GetType().ToString();

            list.Add(new ObjectStreaming()
            {
                path = patch.assetsFile.fileName,
                offset = patch.reader.byteStart,
                size = patch.reader.byteSize,
                name = name,
                obj = patch

            });

            if (patch is IExternalData extdata && extdata.m_StreamData.size > 0)
            {
                list.Add(new ObjectStreaming()
                {
                    path = Path.GetFileName(extdata.m_StreamData.path),
                    offset = extdata.m_StreamData.offset,
                    size = extdata.m_StreamData.size,
                    name = name,
                    obj = patch,
                });
            }
            return list;
        }

        private byte[] GetBlockData(List<AssetStudio.Object> patchlist, bool reshuffle = true)
        {
            Dictionary<string, List<ObjectStreaming>> patchdic = new Dictionary<string, List<ObjectStreaming>>();
            if (patchlist != null)
            {
                foreach (var patch in patchlist)
                {
                    foreach (var info in GetStreamingDatas(patch))
                    {
                        List<ObjectStreaming> list;
                        if (!patchdic.TryGetValue(info.path, out list))
                        {
                            list = new List<ObjectStreaming>();
                        }
                        list.Add(info);
                        patchdic[info.path] = list;
                    }
                }
            }
            var stream = new MemoryStream();

            var fileList = bundle.fileList;

            var fileSize = new List<uint>();
  
            foreach (var file in fileList)
            {
                //file.stream.Seek(0, SeekOrigin.Begin);
                var fileStreamLastPos = 0u;

                List<ObjectStreaming> list;
                if (patchdic.TryGetValue(file.path, out list))
                {
                    var buffer = (file.stream as MemoryStream).GetBuffer();
                    foreach (var streamdata in list)
                    {
                        if (reshuffle)
                        {
                            // block 0, serialize file Header
                            if (fileStreamLastPos != streamdata.offset)
                            {
                                var size = (uint)streamdata.offset - fileStreamLastPos;
                                fileSize.Add(size);
                                Console.WriteLine("UnExcepect Streaming Data Offset Get: {0} Expect: {1}", streamdata.offset, fileStreamLastPos);
                                fileStreamLastPos += size;
                            }
                            fileSize.Add(streamdata.size);
                            fileStreamLastPos += streamdata.size;
                        }
                        else
                        {
                            //file.stream.Position = stream.offset;
                            Array.Clear(buffer, (int)streamdata.offset, (int)streamdata.size);
                        }
                    }
                }

                file.stream.Position = 0;
                file.stream.CopyTo(stream);
            }

            stream.Position = 0;
            //stream = new MemoryStream();

            int BLOCK_SIZE = 128 * 1024;
            if (fileSize.Count == 0)
            {
                var total = (int)stream.Length;
                while (total > 0)
                {
                    var size = Math.Min(BLOCK_SIZE, total);
                    fileSize.Add((uint)size);
                    total -= size;
                }
            }

            var m_BlocksInfo = bundle.m_BlocksInfo;

            var flags = bundle.m_BlocksInfo[0].flags;
            var blocksinfo = new List<BundleFile.StorageBlock>();

            var retstream = new MemoryStream();
            var fileidx = 0;

            var blockidx = 0;
            // 按文件分组压缩
            while (fileidx < fileSize.Count)
            {
                uint compressedSize = 0;
                int uncompressedSize = 0;
                while (fileidx < fileSize.Count && (uncompressedSize + fileSize[fileidx] < BLOCK_SIZE || uncompressedSize == 0))
                {
                    uncompressedSize += (int)fileSize[fileidx];
                    fileidx++;
                }

                var realuncompressed = new byte[uncompressedSize];
                var compressed = new byte[uncompressedSize];
                var readsize = stream.Read(realuncompressed, 0, uncompressedSize);
                var encodesize = LZ4Codec.Encode(realuncompressed, compressed, LZ4Level.L12_MAX);
                retstream.Write(compressed, 0, encodesize);

                blocksinfo.Add(new BundleFile.StorageBlock()
                {
                    compressedSize = (uint)encodesize,
                    uncompressedSize = (uint)uncompressedSize,
                    flags = flags,
                });

            }

            bundle.m_BlocksInfo = blocksinfo.ToArray();

            var ret = new byte[retstream.Length];
            retstream.Position = 0;
            retstream.Read(ret);
            return ret;
        }

        private void GetBlockInfo(byte[] compressedBytes, out uint uncompressedSize, out uint compressedSize)
        {
            var m_Header = bundle.m_Header;
            var m_BlocksInfo = bundle.m_BlocksInfo;
            var m_DirectoryInfo = bundle.m_DirectoryInfo;

            // = new byte[m_Header.uncompressedBlocksInfoSize];
            var blocksinfostream = new MemoryStream();
            byte[] blocksinfobytes;

            using (var blocksinfoWriter = new EndianBinaryWriter(blocksinfostream))
            {
                blocksinfoWriter.Write(new byte[16]); // Hash
                blocksinfoWriter.Write(m_BlocksInfo.Length);

                foreach (var block in m_BlocksInfo)
                {
                    blocksinfoWriter.Write(block.uncompressedSize);
                    blocksinfoWriter.Write(block.compressedSize);
                    blocksinfoWriter.Write(block.flags);
                }

                blocksinfoWriter.Write(m_DirectoryInfo.Length);

                foreach (var node in m_DirectoryInfo)
                {
                    blocksinfoWriter.Write(node.offset);
                    blocksinfoWriter.Write(node.size);
                    blocksinfoWriter.Write(node.flags);
                    blocksinfoWriter.WriteStringNull(node.path);
                }
                blocksinfobytes = GetBytes(blocksinfostream);
            }

            compressedSize = (uint)LZ4Codec.Encode(blocksinfobytes, compressedBytes, LZ4Level.L12_MAX);
            uncompressedSize = (uint)blocksinfobytes.Length;

            foreach (var compresst in (LZ4Level[])Enum.GetValues(typeof(LZ4Level)))
            {
                var size = LZ4Codec.Encode(blocksinfobytes, compressedBytes, compresst);
                Console.WriteLine("true : {0} type {1} now {2} ", m_Header.compressedBlocksInfoSize, compresst, size);
            }
        }
    }
}


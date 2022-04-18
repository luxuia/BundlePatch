using System;
using AssetStudio;
using K4os.Compression.LZ4;

namespace BundlePatch
{
	public class PatchMgr 
	{
        BundleFile bundle;
		public PatchMgr(BundleFile bundle)
		{
            this.bundle = bundle;
		}

        public EndianBinaryWriter Write(Stream stream, List<NamedObject> topatchlist)
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

            var blockData = GetBlockData(topatchlist);

            var compressedBytes = new byte[m_Header.uncompressedBlocksInfoSize];
            var compressedSize = GetBlockInfo(compressedBytes);


            writer.Write(m_Header.size);
            writer.Write(compressedSize);
            writer.Write(m_Header.uncompressedBlocksInfoSize);
            writer.Write(m_Header.flags);

            //BLOCK INFO
            writer.AlignStream(16);

            writer.Write(compressedBytes, 0, compressedSize);
            writer.Write(blockData);

            return writer;
        }


        private byte[] GetBlockData(List<NamedObject> topatchlist)
        {
            Dictionary<string, List<StreamingInfo>> patchdic = new Dictionary<string, List<StreamingInfo>>();
            foreach (var patch in topatchlist)
            {
                List<StreamingInfo> list;
                var info = new StreamingInfo();
                if (patch is IExternalData extdata && extdata.m_StreamData.size > 0)
                {
                    info.path = Path.GetFileName(extdata.m_StreamData.path);
                    info.offset = extdata.m_StreamData.offset;
                    info.size = extdata.m_StreamData.size;
                }
                else if (patch is IBuildinData)
                {
                    info.path = patch.assetsFile.fileName;
                    info.offset = patch.reader.byteStart;
                    info.size = patch.reader.byteSize;
                }
                if (!string.IsNullOrEmpty(info.path))
                {
                    if (!patchdic.TryGetValue(info.path, out list))
                    {
                        list = new List<StreamingInfo>();
                    }
                    list.Add(info);
                    patchdic[info.path] = list;
                }
            }

            var stream = new MemoryStream();

            var fileList = bundle.fileList;

            foreach (var file in fileList)
            {
                //file.stream.Seek(0, SeekOrigin.Begin);

                List<StreamingInfo> list;
                if (patchdic.TryGetValue(file.path, out list))
                {
                    var buffer = (file.stream as MemoryStream).GetBuffer();
                    foreach (var streamdata in list)
                    {
                        //file.stream.Position = stream.offset;
                        Array.Clear(buffer, (int)streamdata.offset, (int)streamdata.size);
                    }
                }

                file.stream.Position = 0;
                file.stream.CopyTo(stream);
            }

            stream.Position = 0;
            //stream = new MemoryStream();

            int BLOCK_SIZE = 128 * 1024;
            var compressed = new byte[BLOCK_SIZE];
            var m_BlocksInfo = bundle.m_BlocksInfo;

            var retstream = new MemoryStream();

            var blockidx = 0;
            while (stream.Position < stream.Length)
            {
                var tarsize = Math.Min(BLOCK_SIZE, (int)(stream.Length - stream.Position));
                var realuncompressed = new byte[tarsize];
                var readsize = stream.Read(realuncompressed, 0, tarsize);

                var encodesize = LZ4Codec.Encode(realuncompressed, compressed, LZ4Level.L12_MAX);
                retstream.Write(compressed, 0, encodesize);

                m_BlocksInfo[blockidx].compressedSize = (uint)encodesize;

                blockidx++;
                //BigArrayPool<byte>.Shared.Return(realuncompressed);
            }

            var ret = new byte[retstream.Length];
            retstream.Position = 0;
            retstream.Read(ret);
            return ret;
        }

        private int GetBlockInfo(byte[] compressedBytes)
        {
            var m_Header = bundle.m_Header;
            var m_BlocksInfo = bundle.m_BlocksInfo;
            var m_DirectoryInfo = bundle.m_DirectoryInfo;

            var blocksinfobytes = new byte[m_Header.uncompressedBlocksInfoSize];
            var blocksinfostream = new MemoryStream(blocksinfobytes);
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
            }
            var compressedSize = LZ4Codec.Encode(blocksinfobytes, compressedBytes, LZ4Level.L12_MAX);

            Console.WriteLine(m_Header.compressedBlocksInfoSize);
            foreach (var compresst in (LZ4Level[])Enum.GetValues(typeof(LZ4Level)))
            {
                var size = LZ4Codec.Encode(blocksinfobytes, compressedBytes, compresst);
                Console.WriteLine("true : {0} type {1} now {2} ", m_Header.compressedBlocksInfoSize, compresst, size);
            }
            return compressedSize;
        }
    }
}


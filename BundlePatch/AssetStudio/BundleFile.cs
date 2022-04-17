using K4os.Compression.LZ4;
using System.IO;
using System.Linq;

namespace AssetStudio
{
    public class BundleFile
    {
        public class Header
        {
            public string signature;
            public uint version;
            public string unityVersion;
            public string unityRevision;
            public long size;
            public uint compressedBlocksInfoSize;
            public uint uncompressedBlocksInfoSize;
            public uint flags;
        }

        public class StorageBlock
        {
            public uint compressedSize;
            public uint uncompressedSize;
            public ushort flags;
        }

        public class Node
        {
            public long offset;
            public long size;
            public uint flags;
            public string path;
        }

        public Header m_Header;
        private StorageBlock[] m_BlocksInfo;
        private Node[] m_DirectoryInfo;

        public StreamFile[] fileList;

        public EndianBinaryWriter Write(Stream stream, List<IExternalData> topatchlist)
        {
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

        private byte[] GetBlockData(List<IExternalData> topatchlist)
        {
            Dictionary<string, List<IExternalData>> patchdic = new Dictionary<string, List<IExternalData>>();
            foreach (var patch in topatchlist)
            {
                List<IExternalData> list;
                var filename = Path.GetFileName(patch.m_StreamData.path);
                if (!patchdic.TryGetValue(filename, out list))
                {
                    list = new List<IExternalData>();
                }
                list.Add(patch);
                patchdic[filename] = list;
            }

            var stream = new MemoryStream();

            foreach (var file in fileList)
            {
                //file.stream.Seek(0, SeekOrigin.Begin);

                List<IExternalData> list;
                if (patchdic.TryGetValue(file.path, out list))
                {
                    var buffer = (file.stream as MemoryStream).GetBuffer();
                    foreach (var patch in list)
                    {
                        var streamdata = patch.m_StreamData;
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

        private int GetBlockInfo(byte[] compressedBytes) {

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

        public BundleFile(FileReader reader)
        {
            m_Header = new Header();
            m_Header.signature = reader.ReadStringToNull();
            m_Header.version = reader.ReadUInt32();
            m_Header.unityVersion = reader.ReadStringToNull();
            m_Header.unityRevision = reader.ReadStringToNull();
            switch (m_Header.signature)
            {
                case "UnityArchive":
                    break; //TODO
                case "UnityWeb":
                case "UnityRaw":
                    if (m_Header.version == 6)
                    {
                        goto case "UnityFS";
                    }
                    ReadHeaderAndBlocksInfo(reader);
                    using (var blocksStream = CreateBlocksStream(reader.FullPath))
                    {
                        ReadBlocksAndDirectory(reader, blocksStream);
                        ReadFiles(blocksStream, reader.FullPath);
                    }
                    break;
                case "UnityFS":
                    ReadHeader(reader);
                    ReadBlocksInfoAndDirectory(reader);
                    using (var blocksStream = CreateBlocksStream(reader.FullPath))
                    {
                        ReadBlocks(reader, blocksStream);
                        ReadFiles(blocksStream, reader.FullPath);
                    }
                    break;
            }
        }

        private void ReadHeaderAndBlocksInfo(EndianBinaryReader reader)
        {
            var isCompressed = m_Header.signature == "UnityWeb";
            if (m_Header.version >= 4)
            {
                var hash = reader.ReadBytes(16);
                var crc = reader.ReadUInt32();
            }
            var minimumStreamedBytes = reader.ReadUInt32();
            m_Header.size = reader.ReadUInt32();
            var numberOfLevelsToDownloadBeforeStreaming = reader.ReadUInt32();
            var levelCount = reader.ReadInt32();
            m_BlocksInfo = new StorageBlock[1];
            for (int i = 0; i < levelCount; i++)
            {
                var storageBlock = new StorageBlock()
                {
                    compressedSize = reader.ReadUInt32(),
                    uncompressedSize = reader.ReadUInt32(),
                    flags = (ushort)(isCompressed ? 1 : 0)
                };
                if (i == levelCount - 1)
                {
                    m_BlocksInfo[0] = storageBlock;
                }
            }
            if (m_Header.version >= 2)
            {
                var completeFileSize = reader.ReadUInt32();
            }
            if (m_Header.version >= 3)
            {
                var fileInfoHeaderSize = reader.ReadUInt32();
            }
            reader.Position = m_Header.size;
        }

        private Stream CreateBlocksStream(string path)
        {
            Stream blocksStream;
            var uncompressedSizeSum = m_BlocksInfo.Sum(x => x.uncompressedSize);
            if (uncompressedSizeSum >= int.MaxValue)
            {
                /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, uncompressedSizeSum);
                assetsDataStream = memoryMappedFile.CreateViewStream();*/
                blocksStream = new FileStream(path + ".temp", FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            }
            else
            {
                blocksStream = new MemoryStream((int)uncompressedSizeSum);
            }
            return blocksStream;
        }

        private void ReadBlocksAndDirectory(EndianBinaryReader reader, Stream blocksStream)
        {
            foreach (var blockInfo in m_BlocksInfo)
            {
                var uncompressedBytes = reader.ReadBytes((int)blockInfo.compressedSize);
                if (blockInfo.flags == 1)
                {
                    using (var memoryStream = new MemoryStream(uncompressedBytes))
                    {
                        using (var decompressStream = SevenZipHelper.StreamDecompress(memoryStream))
                        {
                            uncompressedBytes = decompressStream.ToArray();
                        }
                    }
                }
                blocksStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
            }
            blocksStream.Position = 0;
            var blocksReader = new EndianBinaryReader(blocksStream);
            var nodesCount = blocksReader.ReadInt32();
            m_DirectoryInfo = new Node[nodesCount];
            for (int i = 0; i < nodesCount; i++)
            {
                m_DirectoryInfo[i] = new Node
                {
                    path = blocksReader.ReadStringToNull(),
                    offset = blocksReader.ReadUInt32(),
                    size = blocksReader.ReadUInt32()
                };
            }
        }

        public void ReadFiles(Stream blocksStream, string path)
        {
            fileList = new StreamFile[m_DirectoryInfo.Length];
            for (int i = 0; i < m_DirectoryInfo.Length; i++)
            {
                var node = m_DirectoryInfo[i];
                var file = new StreamFile();
                fileList[i] = file;
                file.path = node.path;
                file.fileName = Path.GetFileName(node.path);
                if (node.size >= int.MaxValue)
                {
                    /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, entryinfo_size);
                    file.stream = memoryMappedFile.CreateViewStream();*/
                    var extractPath = path + "_unpacked" + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(extractPath);
                    file.stream = new FileStream(extractPath + file.fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                else
                {
                    file.stream = new MemoryStream((int)node.size);
                }
                blocksStream.Position = node.offset;
                blocksStream.CopyTo(file.stream, node.size);
                file.stream.Position = 0;
            }
        }

        private void ReadHeader(EndianBinaryReader reader)
        {
            m_Header.size = reader.ReadInt64();
            m_Header.compressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.flags = reader.ReadUInt32();
            if (m_Header.signature != "UnityFS")
            {
                reader.ReadByte();
            }
        }

        private void ReadBlocksInfoAndDirectory(EndianBinaryReader reader)
        {
            byte[] blocksInfoBytes;
            if (m_Header.version >= 7)
            {
                reader.AlignStream(16);
            }
            if ((m_Header.flags & 0x80) != 0) //kArchiveBlocksInfoAtTheEnd
            {
                var position = reader.Position;
                reader.Position = reader.BaseStream.Length - m_Header.compressedBlocksInfoSize;
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
                reader.Position = position;
            }
            else //0x40 kArchiveBlocksAndDirectoryInfoCombined
            {
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
            }
            MemoryStream blocksInfoUncompresseddStream;
            var uncompressedSize = m_Header.uncompressedBlocksInfoSize;
            switch (m_Header.flags & 0x3F) //kArchiveCompressionTypeMask
            {
                default: //None
                    {
                        blocksInfoUncompresseddStream = new MemoryStream(blocksInfoBytes);
                        break;
                    }
                case 1: //LZMA
                    {
                        blocksInfoUncompresseddStream = new MemoryStream((int)(uncompressedSize));
                        using (var blocksInfoCompressedStream = new MemoryStream(blocksInfoBytes))
                        {
                            SevenZipHelper.StreamDecompress(blocksInfoCompressedStream, blocksInfoUncompresseddStream, m_Header.compressedBlocksInfoSize, m_Header.uncompressedBlocksInfoSize);
                        }
                        blocksInfoUncompresseddStream.Position = 0;
                        break;
                    }
                case 2: //LZ4
                case 3: //LZ4HC
                    {
                        var uncompressedBytes = new byte[uncompressedSize];
                        var numWrite = LZ4Codec.Decode(blocksInfoBytes, uncompressedBytes);
                        if (numWrite != uncompressedSize)
                        {
                            throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                        }
                        blocksInfoUncompresseddStream = new MemoryStream(uncompressedBytes);
                        break;
                    }
            }
            using (var blocksInfoReader = new EndianBinaryReader(blocksInfoUncompresseddStream))
            {
                var uncompressedDataHash = blocksInfoReader.ReadBytes(16);
                var blocksInfoCount = blocksInfoReader.ReadInt32();
                m_BlocksInfo = new StorageBlock[blocksInfoCount];
                for (int i = 0; i < blocksInfoCount; i++)
                {
                    m_BlocksInfo[i] = new StorageBlock
                    {
                        uncompressedSize = blocksInfoReader.ReadUInt32(),
                        compressedSize = blocksInfoReader.ReadUInt32(),
                        flags = blocksInfoReader.ReadUInt16()
                    };
                }

                var nodesCount = blocksInfoReader.ReadInt32();
                m_DirectoryInfo = new Node[nodesCount];
                for (int i = 0; i < nodesCount; i++)
                {
                    m_DirectoryInfo[i] = new Node
                    {
                        offset = blocksInfoReader.ReadInt64(),
                        size = blocksInfoReader.ReadInt64(),
                        flags = blocksInfoReader.ReadUInt32(),
                        path = blocksInfoReader.ReadStringToNull(),
                    };
                }
            }
        }

        private void ReadBlocks(EndianBinaryReader reader, Stream blocksStream)
        {
            foreach (var blockInfo in m_BlocksInfo)
            {
                switch (blockInfo.flags & 0x3F) //kStorageBlockCompressionTypeMask
                {
                    default: //None
                        {
                            reader.BaseStream.CopyTo(blocksStream, blockInfo.compressedSize);
                            break;
                        }
                    case 1: //LZMA
                        {
                            SevenZipHelper.StreamDecompress(reader.BaseStream, blocksStream, blockInfo.compressedSize, blockInfo.uncompressedSize);
                            break;
                        }
                    case 2: //LZ4
                    case 3: //LZ4HC
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var compressedBytes = BigArrayPool<byte>.Shared.Rent(compressedSize);
                            reader.Read(compressedBytes, 0, compressedSize);
                            var uncompressedSize = (int)blockInfo.uncompressedSize;
                            var uncompressedBytes = BigArrayPool<byte>.Shared.Rent(uncompressedSize);
                            var numWrite = LZ4Codec.Decode(compressedBytes, 0, compressedSize, uncompressedBytes, 0, uncompressedSize);
                            if (numWrite != uncompressedSize)
                            {
                                throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                            }
                            blocksStream.Write(uncompressedBytes, 0, uncompressedSize);
                            BigArrayPool<byte>.Shared.Return(compressedBytes);
                            BigArrayPool<byte>.Shared.Return(uncompressedBytes);
                            break;
                        }
                }
            }
            blocksStream.Position = 0;
        }
    }
}

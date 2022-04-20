using System;
using AssetStudio;
using K4os.Compression.LZ4;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Newtonsoft.Json;

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

    public class PatchBaseInfo
    {
        public class Pathids
        {
            public List<long> pathids = new List<long>();
            public string md5;
            public int offset;
        }

        public string name;
        public List<Pathids> block_pathids = new List<Pathids>();
        public int block_data_offset;
    }

    public class PatchInfo
    {
        public class Patch
        {
            public long offset;
            public uint size;
            public int obj_first;
            public int obj_end;
            public int base_block_idx;
        }
        public List<Patch> patchs = new List<Patch>();
    }

    public enum PatchMode
    {
        CleanBundle,
        MakeBase,
        MakePatch,
    }

    public enum KeyStreamOffset
    {
        BlockInfo,
        BlockData,
    }

    public class PatchMgr 
	{
        public AssetsManager assetsmanager;
        public BundleFile bundle;
        PatchMode patchmode;

        //makebase
        public PatchBaseInfo patchBaseInfo = new PatchBaseInfo();

        //makePatch
        public PatchInfo patchinfo = new PatchInfo();
        public Dictionary<int, int> patchblock_to_base = new Dictionary<int, int>();

        public Dictionary<KeyStreamOffset, long> key_offset = new Dictionary<KeyStreamOffset, long>();

        const int BLOCK_SIZE = 128 * 1024;


        public PatchMgr(string path, PatchMode patchmode)
		{
            assetsmanager = new AssetsManager();
            assetsmanager.LoadFiles(path);

            bundle = assetsmanager.GetBundleFile(path);

            this.patchmode = patchmode;
		}

        static byte[] GetBytes(MemoryStream stream)
        {
            var ret = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(ret);
            return ret;
        }

        public void Write(Stream stream, List<AssetStudio.Object> topatchlist = null)
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

            var fileSize = new List<uint>();
            var filePathId = new List<long>();
            var blockstream = GetBlockData(topatchlist, fileSize, filePathId);
            var blockData = MakeBlockData(blockstream, fileSize, filePathId);

            uint compressedSize = 0;
            uint uncompressedSize = 0;
            var compressedBytes = GetBlockInfo(out uncompressedSize, out compressedSize);

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

            key_offset.Add(KeyStreamOffset.BlockInfo, writer.Position);
            writer.Write(compressedBytes, 0, (int)compressedSize);

            key_offset.Add(KeyStreamOffset.BlockData, writer.Position);
            patchBaseInfo.block_data_offset = (int)writer.Position;
            writer.Write(blockData);
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

        public MemoryStream GetBlockData(List<AssetStudio.Object> patchlist, List<uint> fileSize, List<long> filePathId)
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
            foreach (var list in patchdic.Values)
            {
                list.Sort((a, b) =>
                {
                    return (int)(a.offset - b.offset);
                });
            }
            var stream = new MemoryStream();

            var fileList = bundle.fileList;

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
                        if (patchmode == PatchMode.MakeBase || patchmode == PatchMode.MakePatch)
                        {
                            // 计算每个object的大小
                            // block 0, serialize file Header
                            if (fileStreamLastPos != streamdata.offset)
                            {
                                var size = (uint)streamdata.offset - fileStreamLastPos;
                                fileSize.Add(size);
                                filePathId.Add(0);
                                Console.WriteLine("UnExcepect Streaming Data Offset Get: {0} Expect: {1}", streamdata.offset, fileStreamLastPos);
                                fileStreamLastPos += size;
                            }
                            fileSize.Add(streamdata.size);
                            filePathId.Add(streamdata.obj.m_PathID);

                            fileStreamLastPos += streamdata.size;
                        }
                        else if (patchmode == PatchMode.CleanBundle)
                        {
                            Array.Clear(buffer, (int)streamdata.offset, (int)streamdata.size);
                        }
                    }
                }

                file.stream.Position = 0;
                file.stream.CopyTo(stream);
            }

            // 如果不重组block，就按unity默认的128kb一个
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

            stream.Position = 0;

            return stream;
        }

        private byte[] MakeBlockData(MemoryStream stream, List<uint> fileSize, List<long> filePathId)
        {
            var m_BlocksInfo = bundle.m_BlocksInfo;

            var flags = bundle.m_BlocksInfo[0].flags;
            var blocksinfo = new List<BundleFile.StorageBlock>();
            var patchnodeidx = 0;

            var retstream = new MemoryStream();
            var fileidx = 0;

            // 按文件分组压缩
            while (fileidx < fileSize.Count)
            {
                uint compressedSize = 0;
                int uncompressedSize = 0;
                var blockidx = blocksinfo.Count;

                var blockpathids = new PatchBaseInfo.Pathids();
                patchBaseInfo.block_pathids.Add(blockpathids);

                do
                {
                    uncompressedSize += (int)fileSize[fileidx];

                    if (patchmode == PatchMode.MakeBase)
                    {
                        var pathid = filePathId[fileidx];

                        blockpathids.pathids.Add(pathid);
                    }
                    fileidx++;

                    if (patchmode == PatchMode.MakePatch && patchinfo.patchs.Count > patchnodeidx)
                    {
                        var patchblock_info = patchinfo.patchs[patchnodeidx];
                        // 下一个是patch的开始，打断
                        if (patchblock_info.obj_first == fileidx)
                            break;
                        // 下一个是patch的结束，打断
                        if (patchblock_info.obj_end == fileidx)
                        {
                            patchnodeidx++;
                            patchblock_to_base[blocksinfo.Count] = patchblock_info.base_block_idx;
                            break;
                        }
                    }

                    if (fileidx == 1)
                    {
                        // 第一个block单独
                        break;
                    }

                } while (fileidx < fileSize.Count && uncompressedSize < BLOCK_SIZE);

                var uncompressed = new byte[uncompressedSize];
                var compressed = new byte[uncompressedSize];
                var readsize = stream.Read(uncompressed, 0, uncompressedSize);

                blockpathids.md5 = GetMd5(uncompressed);
                blockpathids.offset = (int)retstream.Position;

                var encodesize = LZ4Codec.Encode(uncompressed, compressed, LZ4Level.L12_MAX);
                retstream.Write(compressed, 0, encodesize);

                blocksinfo.Add(new BundleFile.StorageBlock()
                {
                    compressedSize = (uint)encodesize,
                    uncompressedSize = (uint)uncompressedSize,
                    flags = flags,
                });
         
            }

            bundle.m_BlocksInfo = blocksinfo.ToArray();

            return GetBytes(retstream);
        }

        public static string GetMd5(ReadOnlySpan<byte> bytes)
        {
            var hashBytes = System.Security.Cryptography.MD5.HashData(bytes);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private byte[] GetBlockInfo(out uint uncompressedSize, out uint compressedSize)
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

            uncompressedSize = (uint)blocksinfobytes.Length;
            byte[] compressedBytes = new byte[blocksinfobytes.Length];

            compressedSize = (uint)LZ4Codec.Encode(blocksinfobytes, compressedBytes, LZ4Level.L12_MAX);


            return compressedBytes;
            /*
            foreach (var compresst in (LZ4Level[])Enum.GetValues(typeof(LZ4Level)))
            {
                var size = LZ4Codec.Encode(blocksinfobytes, compressedBytes, compresst);
                Console.WriteLine("true : {0} type {1} now {2} ", m_Header.compressedBlocksInfoSize, compresst, size);
            }
            */
        }

        public static List<int> GetBlockFiles(AssetsManager assetsmanager, BundleFile bundlefile, PatchBaseInfo patchBaseInfo)
        {
            var blocks = new List<int>() { };
            int blocksize = 0;
            foreach (var block in bundlefile.m_BlocksInfo)
            {
                blocks.Add((int)block.uncompressedSize);
                blocksize += (int)block.uncompressedSize;
            }

            foreach (var assetfile in assetsmanager.assetsFileList)
            {
                foreach (var asset in assetfile.Objects)
                {
                    var streamings = GetStreamingDatas(asset);
                    foreach (var stream in streamings)
                    {
                        var offset = stream.offset;
                    }
                }
            }

            return null;
        }

        public List<AssetStudio.Object> GetObjects()
        {
            var list = new List<AssetStudio.Object>();
            foreach (var assetFile in assetsmanager.assetsFileList)
            {
                foreach (var obj in assetFile.Objects)
                {
                    list.Add(obj);
                }
            }
            return list;
        }

        public void CalPatchInfo(string basepath)
        {

            var baseinfojson = File.ReadAllText(basepath + ".json");
            var baseinfo = JsonConvert.DeserializeObject<PatchBaseInfo>(baseinfojson);


            var fileSize = new List<uint>();
            var filePathId = new List<long>();
            var fileOffset = new List<uint>() { 0};
            var patchstreamer = GetBlockData(GetObjects(), fileSize, filePathId);
            var patchbytes = patchstreamer.GetBuffer();
            foreach (var size in fileSize)
            {
                var last = fileOffset[fileOffset.Count - 1];
                fileOffset.Add(size + last);
            }

            var checkidx = 0;
            for (var blockidx = 1; blockidx < baseinfo.block_pathids.Count; ++blockidx)
            {
                var block = baseinfo.block_pathids[blockidx];
                int last = -1;
                int first_pathid_idx = block.pathids.FindIndex((a) => { return a != 0; });
                long first_pathid = block.pathids[first_pathid_idx];
                int first = filePathId.FindIndex((a)=> { return first_pathid == a; }) - first_pathid_idx;

                if (first >= 0) {
                    bool fail = false;
                    var filesize = 0u;
                    // 检查pathid能对上
                    for (var i = 0; i < block.pathids.Count; ++i) {
                        var pathid = filePathId[i + first];
                        filesize += fileSize[i + first];

                        var checkpathid = block.pathids[i];
                        if (pathid != checkpathid && checkpathid != 0)
                        {
                            fail = true;
                            break;
                        }
                    }
                    var offset = (int)fileOffset[first];

                    if (!fail)
                    {
                        //检查md5
                        var md5 = GetMd5(new ReadOnlySpan<byte>(patchbytes, offset, (int)filesize));
                        if (md5 != block.md5) {
                            fail = true;
                        }
                    }
                    if (!fail)
                    {
                        patchinfo.patchs.Add(new PatchInfo.Patch()
                        {
                            offset = offset,
                            size = filesize,
                            obj_first = first,
                            obj_end = first + block.pathids.Count,
                            base_block_idx = blockidx
                        });
                    }
                }
            }
        }
    }
}


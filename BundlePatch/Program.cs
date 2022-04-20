using System;
using AssetStudio;
using BundlePatch;
using CommandLine;
using Newtonsoft.Json;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static Dictionary<long, AssetStudio.Object> PatchObjs = new Dictionary<long, AssetStudio.Object>();

        class Options
        {
            [Value(0)]
            public IEnumerable<string> filePath { get; set; }

            [Option('c', "clean")]
            public bool clean { get; set; }

            [Option('d', "diff")]
            public bool diff { get; set; }

            [Option('b', "base")]
            public bool make_base { get; set; }

            [Option('p', "patch")]
            public bool make_patch { get; set; }
        }

        static void Main(string[] args)
        {
            //"Test/role_monster.j",
            //    "Test/role_monster.patch.j");//,
            // "Test/rd_role_monster.fbx.patch.j",
            //"Test/rd_role_monster.tga.patch.j"
            args = new string[]
            {
                "Test/role_monster.j", "Test/role_monster.j", "-p"
            };
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
               {
                   var filepath = o.filePath.ToArray();

                   if (filepath.Length == 1)
                   {
                       if (o.make_base)
                       {
                           MakeBase(filepath[0]);
                       }
                       else
                       {
                           Dump(filepath[0]);
                       }
                   }
                   else if (filepath.Length > 1)
                   {
                       if (o.make_patch)
                       {
                           MakePatch(filepath[0], filepath[1]);
                       }
                       else if (o.clean)
                       {
                           CleanBundle(filepath[0], filepath.TakeLast(filepath.Length - 1).ToArray());
                       }
                       else
                       {
                           DumpDiff(filepath[0], filepath.TakeLast(filepath.Length - 1).ToArray());
                       }
                   }
               }).WithNotParsed<Options>(e =>
               {

               });

            //Console.WriteLine("Hello World!");


            //var ret = "/Users/luxuia/Documents/Demo/Assets/tex_patched";

            //TestWriteBundleFile("Test/tex", "Test/tex_new");

            //TestWriteBundleFile("Test/role_monster.j",
            //    "Test/role_monster.patch.j");//,
            // "Test/rd_role_monster.fbx.patch.j",
            //"Test/rd_role_monster.tga.patch.j");

            /*
            TestWriteBundleFile("Test/prefab_new");
            TestWriteBundleFile("Test/aoruola_lightmap.j");
            TestWriteBundleFile("Test/aoruola_mmap_other.j");
            TestWriteBundleFile("Test/aoruola_terrain.j");
            TestWriteBundleFile("Test/aoruola_unity.j");
            */
        }

        static void CollectPatchPath(string[] patches)
        {
            foreach (var patch in patches)
            {
                var newpath = GetFilePath(patch);
                var patchassetsmgr = new AssetsManager();
                patchassetsmgr.LoadFiles(newpath);
                foreach (var assetFile in patchassetsmgr.assetsFileList)
                {
                    foreach (var obj in assetFile.Objects)
                    {
                        //if (obj is IExternalData || obj is IBuildinData)
                        PatchObjs.Add(obj.m_PathID, obj);

                    }
                }
            }
        }

        static string GetFilePath(string path)
        {
            if (File.Exists(path)) return path;

            path = ROOT_PATH + path;
            return path;
        }

        static string ROOT_PATH = "../../../";
        static void CleanBundle(string name, params string[] patches)
        {
            var oldpath = GetFilePath( name);

            var assetsmanager = new AssetsManager();
            assetsmanager.LoadFiles(oldpath);

            CollectPatchPath(patches);

            var patchedlist = new List<AssetStudio.Object>();
            foreach (var assetFile in assetsmanager.assetsFileList)
            {
                foreach (var obj in assetFile.Objects)
                {
                    if (PatchObjs.ContainsKey(obj.m_PathID))
                    {
                        patchedlist.Add(obj);
                    }
                }
            }

            var ret = GetFilePath(name + "_patched");
  
            var patchmgr = new PatchMgr(oldpath, PatchMode.CleanBundle);
            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write))
            {
                patchmgr.Write(streamer, patchedlist);
            }

            var testassets = new AssetsManager();
            testassets.LoadFiles(ret);
        }


        static void DumpDiff(string name, params string[] patches)
        {
            var oldpath = GetFilePath(name);

            var assetsmanager = new AssetsManager();
            assetsmanager.LoadFiles(oldpath);

            CollectPatchPath(patches);

            var topatchlist = new List<NamedObject>();
            foreach (var assetFile in assetsmanager.assetsFileList)
            {
                foreach (var obj in assetFile.Objects)
                {
                    if (PatchObjs.ContainsKey(obj.m_PathID))
                    {
                        foreach (var info in PatchMgr.GetStreamingDatas(obj))
                        {
                            //PatchObjs.Add(obj.m_PathID, (NamedObject)obj);
                            Console.WriteLine("    type: {0} name: {1} pathid: {2} file: {3} offset: {4} size: {5}",
                                obj.GetType(), info.name, obj.m_PathID, info.path, info.offset, info.size);
                        }
                    }
                }
            }
        }

        static void MakeBase(string name)
        {
            var path = GetFilePath(name);

            var patchmgr = new PatchMgr(path, PatchMode.MakeBase);

            var ret = path + "_base";
            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write))
            {
                patchmgr.Write(streamer, patchmgr.GetObjects());
            }

            var testassets = new AssetsManager();
            testassets.LoadFiles(ret);

            patchmgr.patchBaseInfo.name = Path.GetFileName(path);

            var json = JsonConvert.SerializeObject(patchmgr.patchBaseInfo);
            File.WriteAllText(ret + ".json", json);
        }

        static void MakePatch(string basepath, string patchpath)
        {
            basepath = GetFilePath(basepath);
            patchpath = GetFilePath(patchpath);

            var patchmgr = new PatchMgr(patchpath, PatchMode.MakePatch);
            patchmgr.CalPatchInfo(basepath + "_base");

            var ret = patchpath + "_patch";
            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write | FileAccess.Read))
            {
                patchmgr.Write(streamer, null);

                streamer.Flush();

                var patch_info_data = new PatchUtil.PatchInfoData();
                patch_info_data.srcFileName = patchpath;
                patch_info_data.dstFileName = patchpath;
                patch_info_data.patchFileName = patchpath;


                var blocksinfo = patchmgr.bundle.m_BlocksInfo;
                var patchblock_set = patchmgr.patchblock_set;

                var block_data_offset = (int)patchmgr.key_offset[KeyStreamOffset.BlockData];

                streamer.Position = 0;

                var patch_data_mem = new MemoryStream();
                var DUMMY_OFFSET = PatchUtil.DummyHeader.Length;
                patch_data_mem.Write(System.Text.ASCIIEncoding.ASCII.GetBytes( PatchUtil.DummyHeader) );
                patch_info_data.infos.Add(
                    new PatchUtil.PatchInfoData.Patch() {
                        offset = DUMMY_OFFSET,
                        size = block_data_offset,
                        is_patch = true
                    });
                streamer.CopyTo(patch_data_mem, block_data_offset);
                
                for (var blockidx = 0; blockidx < blocksinfo.Length; ++blockidx)
                {
                    var blockinfo = blocksinfo[blockidx];

                    var can_reused = patchblock_set.Contains(blockidx);
                    long offset;
                
                    if (can_reused)
                    {
                        offset = streamer.Position;
                        streamer.Seek(blockinfo.compressedSize, SeekOrigin.Current);
                    } else
                    {
                        offset = patch_data_mem.Position;
                        streamer.CopyTo(patch_data_mem, blockinfo.compressedSize);
                    }
                    patch_info_data.infos.Add(
                        new PatchUtil.PatchInfoData.Patch()
                        {
                            offset = DUMMY_OFFSET + (int)offset,
                            size = (int)blockinfo.compressedSize,
                            is_patch = !can_reused
                        });
                }

                using (var patch_file_stream = File.Open(patchpath + "_patch.bytes", FileMode.Create, FileAccess.Write))
                {
                    patch_data_mem.Position = 0;
                    patch_data_mem.CopyTo(patch_file_stream);
                }

                var patch_str = JsonConvert.SerializeObject(patch_info_data, Formatting.Indented);
                var patch_data_path = patchpath + "_patch.json";
                File.WriteAllText(patch_data_path, patch_str);
            }
        }

        static void Dump(string path)
        {

            var newpath = GetFilePath(path);
            var patchassetsmgr = new AssetsManager();
            patchassetsmgr.LoadFiles(newpath);

            foreach (var assetFile in patchassetsmgr.assetsFileList)
            {
                Console.Write("Serialize File {0}", assetFile.fileName);

                Console.WriteLine("Can Patch Info:");
                foreach (var obj in assetFile.Objects)
                {
                    foreach (var info in PatchMgr.GetStreamingDatas(obj))
                    {
                        //PatchObjs.Add(obj.m_PathID, (NamedObject)obj);
                        Console.WriteLine("    type: {0} name: {1} pathid: {2} file: {3} offset: {4} size: {5}",
                            obj.GetType(), info.name, obj.m_PathID, info.path, info.offset, info.size);
                    }
                }

                if (assetFile.m_Externals.Count > 0)
                {
                    Console.WriteLine("External Info:");
                    foreach (var external in assetFile.m_Externals)
                    {
                        Console.WriteLine("filename: {0} pathname: {1}", external.fileName, external.pathName);
                    }
                }
                Console.WriteLine();
            }


        }
    }
}
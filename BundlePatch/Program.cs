using System;
using AssetStudio;
using BundlePatch;
using CommandLine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
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

            [Option('a', "apply")]
            public bool apply { get; set; }
        }

        static void Main(string[] args)
        {
            //"Test/role_monster.j",
            //    "Test/role_monster.patch.j");//,
            // "Test/rd_role_monster.fbx.patch.j",
            //"Test/rd_role_monster.tga.patch.j"
           //args = new string[]
           //{
           //   // "Test/role_monster.j_base", "Test/role_monster.j_patch.bytes", "-a"
           //   @"D:\x19_bundles\android\bundles\map_scene_zhanchang_fbx_materials.j", @"D:/x19_bundles/android/patches/maps_scene_teshu_zhanchang_fbx_materials_zc_terrain_1_control.tga.patch.j", "--clean"
           //   //"Test/prefab", "-b"
           //};
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
               {
                   var filepath = o.filePath.ToArray();

                   if (o.make_base)
                   {
                       foreach (var path in filepath)
                       {
                           MakeBase(path);
                       }
                   }
                   else if (o.make_patch)
                   {
                       MakePatch(filepath[0], filepath[1]);
                   }
                   else if (o.clean)
                   {
                       CleanBundle(filepath[0], filepath.TakeLast(filepath.Length - 1).ToArray());
                   }
                   else if (o.diff)
                   {
                       DumpDiff(filepath[0], filepath.TakeLast(filepath.Length - 1).ToArray());
                   }
                   else if (o.apply)
                   {
                       ApplyPatch(filepath[0], filepath[1]);
                   }
                   else
                   {
                       foreach (var path in filepath)
                       {
                           Dump(path);
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

        static HashSet<long> CollectPatchPath(string[] patches)
        {
            patches = patches.Distinct().ToArray();
            var patchHash = new HashSet<long>();
            foreach (var patch in patches)
            {
                Console.WriteLine("CollectPatchPath : " + patch);
                var newpath = GetFilePath(patch);
                var patchassetsmgr = new AssetsManager();
                patchassetsmgr.LoadFiles(newpath);
                foreach (var assetFile in patchassetsmgr.assetsFileList)
                {
                    foreach (var obj in assetFile.Objects)
                    {
                        if (obj is MonoScript|| obj.m_PathID == 1)
                        {
                        }
                        else { 
                            //if (obj is IExternalData || obj is IBuildinData)
                            patchHash.Add(obj.m_PathID);
                        }
                    }
                }
            }
            return patchHash;
        }

        static string GetFilePath(string path)
        {
            Console.WriteLine("GetFilePath " + path + " Exist: " + File.Exists(path));
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

            var patched = CollectPatchPath(patches);

            var patchedlist = new List<AssetStudio.Object>();
            foreach (var assetFile in assetsmanager.assetsFileList)
            {
                foreach (var obj in assetFile.Objects)
                {
                    if (patched.Contains(obj.m_PathID))
                    {
                        patchedlist.Add(obj);
                    }
                }
            }

            var ret = oldpath + ".clean";
  
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

            var patched = CollectPatchPath(patches);

            var topatchlist = new List<NamedObject>();
            foreach (var assetFile in assetsmanager.assetsFileList)
            {
                foreach (var obj in assetFile.Objects)
                {
                    if (patched.Contains(obj.m_PathID))
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

            var ret = PatchUtil.GetBaseName(path);;
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
            basepath = PatchUtil.GetBaseName(GetFilePath(basepath));
            patchpath = GetFilePath(patchpath);

            var patchmgr = new PatchMgr(patchpath, PatchMode.MakePatch);
            patchmgr.CalPatchInfo(basepath);

            var ret = patchpath + ".patch";
            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write | FileAccess.Read))
            {
                patchmgr.Write(streamer, patchmgr.GetObjects());

                streamer.Flush();

                var patchbytes_name = PatchUtil.GetPatchName(patchpath);

                var patch_info_data = new PatchUtil.PatchInfoData();
                patch_info_data.srcFileName = basepath;
                patch_info_data.dstFileName = patchpath;
                patch_info_data.patchFileName = patchbytes_name;


                var blocksinfo = patchmgr.bundle.m_BlocksInfo;
                var patchblock_to_base = patchmgr.patchblock_to_base;
                var baseblock_info = patchmgr.patchBaseInfo;

                var block_data_offset = (int)patchmgr.key_offset[KeyStreamOffset.BlockData];

                streamer.Position = 0;

                var patch_data_mem = File.Open(patchbytes_name, FileMode.Create, FileAccess.Write);

                var DUMMY_OFFSET = PatchUtil.DummyHeader.Length;
                patch_data_mem.Write(System.Text.ASCIIEncoding.ASCII.GetBytes( PatchUtil.DummyHeader) );
                patch_info_data.infos.Add(
                    new PatchUtil.PatchInfoData.Patch() {
                        offset = DUMMY_OFFSET,
                        size = block_data_offset,
                        is_patch = true
                    });

                streamer.CopyLength(patch_data_mem, block_data_offset);
                //streamer.CopyTo(patch_data_mem, block_data_offset);
                
                for (var blockidx = 0; blockidx < blocksinfo.Length; ++blockidx)
                {
                    var blockinfo = blocksinfo[blockidx];

                    var can_reused = patchblock_to_base.ContainsKey(blockidx);
                    long offset;
                
                    if (can_reused)
                    {
                        var baseblock = baseblock_info.block_pathids[patchblock_to_base[blockidx]];
                        offset = baseblock.offset + baseblock_info.block_data_offset;
                        streamer.Seek(blockinfo.compressedSize, SeekOrigin.Current);
                    } else
                    {
                        offset = patch_data_mem.Position;
                        streamer.CopyLength(patch_data_mem, (int)blockinfo.compressedSize);
                    }
                    patch_info_data.infos.Add(
                        new PatchUtil.PatchInfoData.Patch()
                        {
                            offset = (int)offset,
                            size = (int)blockinfo.compressedSize,
                            is_patch = !can_reused
                        });
                }


                patch_data_mem.Dispose();

                var patch_str = JsonConvert.SerializeObject(patch_info_data, Formatting.Indented);
                var patch_data_path = ret + ".json";
                File.WriteAllText(patch_data_path, patch_str);
            }
        }

        static void ApplyPatch(string basepath, string patchpath)
        {
            basepath = GetFilePath(basepath);
            patchpath = GetFilePath(patchpath);

            var patchjson_path = patchpath.Replace(".bytes", ".json");
            var patchjson_str = File.ReadAllText(patchjson_path);
            var patch_info_data = JsonConvert.DeserializeObject<PatchUtil.PatchInfoData>(patchjson_str);

            var basefile = File.ReadAllBytes(basepath);
            var patchfile = File.ReadAllBytes(patchpath);

            using (var patch_stream = File.OpenWrite(patchpath + ".patch"))
            {
                foreach (var info in patch_info_data.infos)
                {
                    if (info.is_patch)
                    {
                        patch_stream.Write(patchfile, info.offset, info.size);
                    } else
                    {
                        patch_stream.Write(basefile, info.offset, info.size);
                    }
                }

            }
        }

        static void Dump(string path)
        {

            var newpath = GetFilePath(path);
            var patchassetsmgr = new AssetsManager();
            patchassetsmgr.LoadFiles(newpath);

            var builder = new System.Text.StringBuilder();
            foreach (var assetFile in patchassetsmgr.assetsFileList)
            {
                builder.AppendFormat("Serialize File {0}", assetFile.fileName);

                builder.AppendLine("Can Patch Info:");
                foreach (var obj in assetFile.Objects)
                {
                    foreach (var info in PatchMgr.GetStreamingDatas(obj))
                    {
                        //PatchObjs.Add(obj.m_PathID, (NamedObject)obj);
                        builder.AppendLine(string.Format("file: {3} \t pathid: {2} \t type: {0} \t name: {1} \t offset: {4} size: {5}",
                            obj.GetType(), info.name, obj.m_PathID, info.path, info.offset, info.size));
                    }
                }

                if (assetFile.m_Externals.Count > 0)
                {
                    builder.AppendLine("External Info:");
                    foreach (var external in assetFile.m_Externals)
                    {
                        builder.AppendLine(string.Format("filename: {0} pathname: {1}", external.fileName, external.pathName));
                    }
                }
                builder.AppendLine();
            }
            var text = builder.ToString();
            Console.Write(text);

            File.WriteAllText(newpath + ".dump", text);
        }
    }
}
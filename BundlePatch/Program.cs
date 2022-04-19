using System;
using AssetStudio;
using BundlePatch;
using CommandLine;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static Dictionary<long, AssetStudio.Object> PatchObjs = new Dictionary<long, AssetStudio.Object>();

        class Options
        {
            [Value(0)]
            public IEnumerable<string> filePath { get; set; }

            [Option('e', "eraser")]
            public bool eraser { get; set; }

            [Option('d', "diff")]
            public bool diff { get; set; }

            [Option('r', "reshuffle")]
            public bool reshuffle { get; set; }
        }

        static void Main(string[] args)
        {
            //"Test/role_monster.j",
            //    "Test/role_monster.patch.j");//,
            // "Test/rd_role_monster.fbx.patch.j",
            //"Test/rd_role_monster.tga.patch.j"
            args = new string[]
            {
                "Test/tex", "-r"
            };
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
               {
                   var filepath = o.filePath.ToArray();

                   if (filepath.Length == 1)
                   {
                       if (o.reshuffle)
                       {
                           Reshuffle(filepath[0]);
                       }
                       else
                       {
                           Dump(filepath[0]);
                       }
                   }
                   else if (filepath.Length > 1)
                   {
                       if (o.eraser)
                       {
                           EraserBundle(filepath[0], filepath.TakeLast(filepath.Length - 1).ToArray());
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
        static void EraserBundle(string name, params string[] patches)
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
            var reader = new FileReader(oldpath);

            var bundleFile = new BundleFile(reader);
            var patchmgr = new PatchMgr(bundleFile);
            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write))
            {
                patchmgr.Write(streamer, patchedlist, false);
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

        static void Reshuffle(string name)
        {
            var path = GetFilePath(name);
            var reader = new FileReader(path);

            var bundleFile = new BundleFile(reader);
            var patchmgr = new PatchMgr(bundleFile);


            var assetsmanager = new AssetsManager();
            assetsmanager.LoadFiles(path);
            var patchedlist = new List<AssetStudio.Object>();
            foreach (var assetFile in assetsmanager.assetsFileList)
            {
                foreach (var obj in assetFile.Objects)
                {
                    patchedlist.Add(obj);
                }
            }

            var ret = path + "_base";
            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write))
            {
                patchmgr.Write(streamer, patchedlist, true);
            }

            var testassets = new AssetsManager();
            testassets.LoadFiles(ret);
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
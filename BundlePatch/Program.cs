using System;
using AssetStudio;
using BundlePatch;
using CommandLine;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static Dictionary<long, NamedObject> PatchObjs = new Dictionary<long, NamedObject>();

        class Options
        {
            [Value(0)]
            public IEnumerable<string> filePath { get; set; }

            [Option('e', "eraser")]
            public bool eraser { get; set; }

            [Option('d', "diff")]
            public bool diff { get; set; }
        }

        static void Main(string[] args)
        {
            args = new string[]
            {
                "Test/tex", "-d"
            };
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
               {
                   var filepath = o.filePath.ToArray();
                   if (filepath.Length == 1)
                   {
                       Dump(filepath[0]);
                   } else if (filepath.Length > 1)
                   {
                       if (o.eraser)
                       {
                           TestWriteBundleFile(filepath[0], filepath.TakeLast(filepath.Length - 1).ToArray());
                       } else
                       {
                           TestDumpDiff(filepath[0], filepath.TakeLast(filepath.Length - 1).ToArray());
                       }
                   }
               }).WithNotParsed<Options>(e =>
               {

               });

            //Console.WriteLine("Hello World!");

            /*
            //var oldpath = "/Users/luxuia/Documents/Demo/Assets/tex";
            var oldpath = ROOT_PATH + "Test/monster_all.j";

            var assetsmanager = new AssetsManager();
            assetsmanager.LoadFiles(new string[] {oldpath});

            var newpath = ROOT_PATH + "Test/tex_new";
            var patchassetsmgr = new AssetsManager();
            patchassetsmgr.LoadFiles(new string[] { newpath });

 
            var topatchlist = new List<NamedObject>();
            foreach(var assetFile in assetsmanager.assetsFileList)
            {
                foreach ( var obj in assetFile.Objects)
                {
                    if (PatchObjs.ContainsKey(obj.m_PathID))
                    {
                        switch ( obj)
                        {
                            case Texture2D tex2d:
                                Console.WriteLine("tex2d, name {0} pathid: {1} ", tex2d.m_Name, tex2d.m_PathID);
                                topatchlist.Add(tex2d);
                                break;
                            case NamedObject nameobj:
                                Console.WriteLine("tex2d, name {0} pathid: {1} ", nameobj.m_Name, nameobj.m_PathID);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            */

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

            /*
            foreach (var obj in topatchlist)
            {
                switch (obj)
                {
                    case Texture2D tex2d:
                        var filename = Path.GetFileName(tex2d.m_StreamData.path);
                        var resblock = assetsmanager.resourceFileReaders[filename];
                        //var offset = resblock.Position;
                        break;
                    default:
                        break;
                }
            }
            */
        }

        static void CollectPatchPath(string[] patches)
        {
            foreach (var patch in patches)
            {
                var newpath = ROOT_PATH + patch;
                var patchassetsmgr = new AssetsManager();
                patchassetsmgr.LoadFiles(newpath);
                foreach (var assetFile in patchassetsmgr.assetsFileList)
                {
                    foreach (var obj in assetFile.Objects)
                    {
                        if (obj is IExternalData || obj is IBuildinData)
                        {
                            PatchObjs.Add(obj.m_PathID, (NamedObject)obj);
                        }
                    }
                }
            }
        }

        static string ROOT_PATH = "../../../";
        static void TestWriteBundleFile(string name, params string[] patches)
        {
            var oldpath = ROOT_PATH + name;

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
                        var namedobj = obj as NamedObject;
                        switch (obj)
                        {
                            case IExternalData exdata:
                              
                                Console.WriteLine("name {0} pathid: {1} offset {2} size {3} path {4} type {5}",
                                    namedobj.m_Name, namedobj.m_PathID,
                                    exdata.m_StreamData.offset, exdata.m_StreamData.size, exdata.m_StreamData.path,
                                    obj.GetType());
                                topatchlist.Add(namedobj);
                                break;
                            case IBuildinData:
                                Console.WriteLine("name {0} pathid: {1} offset {2} size {3} path {4} type {5}",
                                    namedobj.m_Name, namedobj.m_PathID,
                                    namedobj.reader.byteStart, namedobj.reader.byteSize, namedobj.assetsFile.fileName,
                                    obj.GetType());
                                topatchlist.Add(namedobj);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            var ret = ROOT_PATH + name + "_patched";
            var reader = new FileReader(oldpath);

            var bundleFile = new BundleFile(reader);
            var patchmgr = new PatchMgr(bundleFile);
            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write))
            {
                patchmgr.Write(streamer, topatchlist);
            }

            var testassets = new AssetsManager();
            testassets.LoadFiles(ret);
        }


        static void TestDumpDiff(string name, params string[] patches)
        {
            var oldpath = ROOT_PATH + name;

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
                        var namedobj = obj as NamedObject;
                        switch (obj)
                        {
                            case IExternalData exdata:

                                Console.WriteLine("name: {0} pathid: {1} offset: {2} size: {3} path: {4} type: {5}",
                                    namedobj.m_Name, namedobj.m_PathID,
                                    exdata.m_StreamData.offset, exdata.m_StreamData.size, exdata.m_StreamData.path,
                                    obj.GetType());
                                topatchlist.Add(namedobj);
                                break;
                            case IBuildinData:
                                Console.WriteLine("name: {0} pathid: {1} offset: {2} size: {3} path: {4} type: {5}",
                                    namedobj.m_Name, namedobj.m_PathID,
                                    namedobj.reader.byteStart, namedobj.reader.byteSize, namedobj.assetsFile.fileName,
                                    obj.GetType());
                                topatchlist.Add(namedobj);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        static void Dump(string path)
        {

            var newpath = ROOT_PATH + path;
            var patchassetsmgr = new AssetsManager();
            patchassetsmgr.LoadFiles(newpath);

            foreach (var assetFile in patchassetsmgr.assetsFileList)
            {
                Console.Write("Serialize File {0}", assetFile.fileName);

                Console.WriteLine("Can Patch Info:");
                foreach (var obj in assetFile.Objects)
                {
                    if (obj is IExternalData || obj is IBuildinData)
                    {
                        var info = new StreamingInfo();
                        if (obj is IExternalData extdata && extdata.m_StreamData.size > 0)
                        {
                            info.path = Path.GetFileName(extdata.m_StreamData.path);
                            info.offset = extdata.m_StreamData.offset;
                            info.size = extdata.m_StreamData.size;
                        }
                        else if (obj is IBuildinData)
                        {
                            info.path = obj.assetsFile.fileName;
                            info.offset = obj.reader.byteStart;
                            info.size = obj.reader.byteSize;
                        }

                        //PatchObjs.Add(obj.m_PathID, (NamedObject)obj);
                        Console.WriteLine("    type: {0} name: {1} pathid: {2} file: {3} offset: {4} size: {5}",
                            obj.GetType(), (obj as NamedObject).m_Name, obj.m_PathID, info.path, info.offset, info.size);
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